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
using SFE.WPF.Views.Pages;
using SFE.WPF.Views;

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
        // The loop allows returning to login after logout.
        while (true)
        {
            var authService = ServiceProvider.GetRequiredService<IAuthService>();

            // ── Show Login ──
            var loginVm = new LoginViewModel(authService);
            var loginWindow = new LoginWindow { DataContext = loginVm };

            loginVm.LoginSucceeded += () => loginWindow.DialogResult = true;

            bool? loginResult = loginWindow.ShowDialog();

            if (loginResult != true)
            {
                // User closed login window without logging in → exit app
                Shutdown();
                return;
            }

            // ── Show Main Window ──
            var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainVm);
            mainWindow.ShowDialog();   // blocks until window closes

            if (!mainVm.LogoutRequested)
            {
                // User closed window via [X] → exit app
                Shutdown();
                return;
            }

            // LogoutRequested == true → clear session, loop back to login
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

        // ★ FIX: Cache=Shared + WAL interceptor
        var walInterceptor = new SqliteWalInterceptor();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Cache=Shared")
                   .AddInterceptors(walInterceptor),
            ServiceLifetime.Transient);

        // ═══ Repositories & Unit of Work ═══
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        // ═══ AUTH (Singleton — holds current user state) ═══
        services.AddSingleton<IAuthService, AuthService>();

        // ═══ Services Application ═══
        services.AddTransient<SettingsService>();
        services.AddTransient<DashboardService>();
        services.AddTransient<InvoiceService>();
        services.AddTransient<ProductService>();
        services.AddTransient<ClientService>();
        services.AddTransient<StockService>();
        services.AddTransient<PointOfSaleService>();
        services.AddTransient<ReportService>();

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