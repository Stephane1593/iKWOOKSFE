using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Infrastructure.Persistence;
using SFE.Infrastructure.EMcf;
using SFE.WPF.ViewModels;
using SFE.WPF.Services;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;

namespace SFE.WPF;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // ══════════════ BUILD DI ══════════════
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // ══════════════ SEED DATABASE ══════════════
        var context = ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeeder.SeedAsync(context);

        // ══════════════ LOGIN LOOP ══════════════
        while (true)
        {
            var authService = ServiceProvider.GetRequiredService<IAuthService>();
            var sessionState = ServiceProvider.GetRequiredService<CashSessionState>();

            // ── 1. Show Login ──
            var loginVm = new LoginViewModel(authService);
            var loginWindow = new LoginWindow { DataContext = loginVm };

            loginVm.LoginSucceeded += () => loginWindow.DialogResult = true;

            bool? loginResult = loginWindow.ShowDialog();
            if (loginResult != true)
            {
                Shutdown();
                return;
            }

            // ── 2. Show Session Opening Dialog ──
            var uow = ServiceProvider.GetRequiredService<IUnitOfWork>();
            var settingsService = ServiceProvider.GetRequiredService<SettingsService>();
            var sessionVm = new SessionOpenViewModel(uow, authService, settingsService);
            var sessionDialog = new SessionOpenDialog { DataContext = sessionVm };

            // Handle both normal confirm and IT Tech bypass
            sessionVm.SessionConfirmed += () => sessionDialog.DialogResult = true;
            sessionVm.SessionBypassed += () => sessionDialog.DialogResult = true;

            bool? sessionResult = sessionDialog.ShowDialog();
            if (sessionResult != true)
            {
                // User cancelled session → logout and loop back to login
                authService.Logout();
                sessionState.Close();
                continue;
            }

            // ── 3. Determine session mode ──
            if (sessionVm.IsBypass)
            {
                // IT Tech bypass — no cash session, enter setup mode
                sessionState.EnterSetupMode(authService.CurrentUser!.FullName);
            }
            else
            {
                // Normal flow — store full session info
                sessionState.Open(sessionVm.Result!);
            }

            // ── 4. Show Main Window ──
            var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainVm);

            mainWindow.ShowDialog();

            if (!mainVm.LogoutRequested)
            {
                Shutdown();
                return;
            }

            // Logout → clear session, loop back to login
            sessionState.Close();
            authService.Logout();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ═══ Base de données ═══
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SFE");
        Directory.CreateDirectory(appDataPath);
        var dbPath = Path.Combine(appDataPath, "sfe.db");

        var walInterceptor = new SqliteWalInterceptor();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Cache=Shared")
                   .AddInterceptors(walInterceptor),
            ServiceLifetime.Transient);

        // ═══ Repositories & Unit of Work ═══
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        // ═══ AUTH (Singleton — holds current user state) ═══
        services.AddSingleton<IAuthService, AuthService>();

        // ═══ Session State (Singleton — holds current cash session) ═══
        services.AddSingleton<CashSessionState>();

        // ═══ Services Application ═══
        services.AddTransient<SettingsService>();
        services.AddTransient<DashboardService>();
        services.AddTransient<InvoiceService>();
        services.AddTransient<ProductService>();
        services.AddTransient<ClientService>();
        services.AddTransient<StockService>();
        services.AddTransient<PointOfSaleService>();
        services.AddTransient<ReportService>();
        services.AddSingleton<CustomerDisplayService>();
        services.AddTransient<UserService>();
        services.AddTransient<CategoryService>();

        // ═══ Fiscal Device ═══
        services.AddSingleton<FiscalDeviceResolver>();
        services.AddSingleton<IFiscalDeviceService>(sp =>
            sp.GetRequiredService<FiscalDeviceResolver>());

        // ═══ ViewModels ═══
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<InvoicingViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<ClientsViewModel>();
        services.AddTransient<StockViewModel>();
        services.AddTransient<StockTransferViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SalesHistoryViewModel>();
        services.AddTransient<PointOfSaleManagementViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<InvoiceDocumentView>();
        services.AddTransient<SessionCloseViewModel>();
        services.AddTransient<ReportZPageViewModel>();
        services.AddTransient<ReportXPageViewModel>();
        services.AddTransient<ReportAPageViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<CategoriesViewModel>();


        // ═══ Fenêtres & Pages ═══
        services.AddTransient<MainWindow>();
        services.AddTransient<ClientsPage>();
        services.AddTransient<ReportView>();
        services.AddTransient<InvoiceDocumentView>();
    }

    private static async Task InitializeDatabaseAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeeder.SeedAsync(context);
    }
}