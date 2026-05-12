using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Infrastructure.Persistence;
using SFE.Infrastructure.Persistence.Interceptors; // ← add if SyncTrackingInterceptor lives here
using SFE.Infrastructure.EMcf;
using SFE.WPF.ViewModels;
using SFE.WPF.Services;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;
using QuestPDF.Infrastructure;
using System.Text;
using SFE.Domain.Abstractions;

namespace SFE.WPF;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        QuestPDF.Settings.License = LicenseType.Community;

        var context = ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeeder.SeedAsync(context);

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

            // ── 2. Determine session flow ──
            var sessionAction = ResolveSessionAction(authService, sessionState);

            switch (sessionAction)
            {
                case SessionAction.JoinExisting:
                    {
                        var session = sessionState.Current!;
                        MessageBox.Show(
                            $"Une session de caisse est déjà ouverte.\n\n" +
                            $"Point de vente : {session.PointOfSaleCode} — {session.PointOfSaleName}\n" +
                            $"Ouverte le : {session.OpenedAt:dd/MM/yyyy HH:mm}\n" +
                            $"Par : {session.OperatorName}\n\n" +
                            $"Vous reprenez cette session en tant que {authService.CurrentUser!.FullName}.\n" +
                            $"Pour la clôturer, effectuez un Rapport Z.",
                            "Reprise de session",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        sessionState.Current!.OperatorName = authService.CurrentUser!.FullName;
                        break;
                    }

                case SessionAction.BlockedWrongPos:
                    {
                        var session = sessionState.Current!;
                        var user = authService.CurrentUser!;
                        var userPosId = user.PointOfSaleId;

                        string userPosDisplay = "un autre point de vente";
                        if (userPosId.HasValue)
                        {
                            try
                            {
                                var uow = ServiceProvider.GetRequiredService<IUnitOfWork>();
                                var userPos = await uow.PointsOfSale.GetByIdAsync(userPosId.Value);
                                if (userPos != null)
                                    userPosDisplay = $"{userPos.Code} — {userPos.Name}";
                            }
                            catch { /* fallback */ }
                        }

                        MessageBox.Show(
                            $"Impossible de vous connecter.\n\n" +
                            $"Une session est déjà ouverte sur :\n" +
                            $"   📍 {session.PointOfSaleCode} — {session.PointOfSaleName}\n" +
                            $"   🕐 Depuis le {session.OpenedAt:dd/MM/yyyy à HH:mm}\n" +
                            $"   👤 Par : {session.OperatorName}\n\n" +
                            $"Vous êtes assigné(e) à :\n" +
                            $"   📍 {userPosDisplay}\n\n" +
                            $"La session en cours doit d'abord être clôturée par un Rapport Z\n" +
                            $"avant qu'un autre point de vente puisse être utilisé sur cette machine.",
                            "Session en cours sur un autre POS",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        authService.Logout();
                        continue;
                    }

                case SessionAction.BlockedNoPos:
                    {
                        var session = sessionState.Current!;

                        MessageBox.Show(
                            $"Impossible de vous connecter.\n\n" +
                            $"Une session est ouverte sur {session.PointOfSaleCode} — {session.PointOfSaleName}\n" +
                            $"mais aucun point de vente ne vous est assigné.\n\n" +
                            $"Contactez votre administrateur.",
                            "Accès refusé",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        authService.Logout();
                        continue;
                    }

                case SessionAction.BypassToSetup:
                    {
                        var session = sessionState.Current!;

                        var choice = MessageBox.Show(
                            $"Une session est ouverte sur {session.PointOfSaleCode} — {session.PointOfSaleName}.\n\n" +
                            $"En tant que technicien, vous pouvez :\n" +
                            $"• Accéder au mode configuration (paramètres, utilisateurs)\n" +
                            $"• La caisse et la facturation restent indisponibles\n\n" +
                            $"Continuer en mode configuration ?",
                            "Mode Technicien",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (choice != MessageBoxResult.Yes)
                        {
                            authService.Logout();
                            continue;
                        }

                        sessionState.EnterSetupMode(authService.CurrentUser!.FullName);
                        break;
                    }

                case SessionAction.ShowDialog:
                    {
                        if (sessionState.IsSetupMode)
                            sessionState.Close();

                        var uow = ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var settingsService = ServiceProvider.GetRequiredService<SettingsService>();
                        var timeProvider = ServiceProvider.GetRequiredService<ITimeProvider>();
                        var sessionVm = new SessionOpenViewModel(uow, authService, settingsService, timeProvider);
                        var sessionDialog = new SessionOpenDialog { DataContext = sessionVm };

                        sessionVm.SessionConfirmed += () => sessionDialog.DialogResult = true;
                        sessionVm.SessionBypassed += () => sessionDialog.DialogResult = true;

                        bool? sessionResult = sessionDialog.ShowDialog();
                        if (sessionResult != true)
                        {
                            authService.Logout();
                            continue;
                        }

                        if (sessionVm.IsBypass)
                            sessionState.EnterSetupMode(authService.CurrentUser!.FullName);
                        else
                            sessionState.Open(sessionVm.Result!);

                        break;
                    }
            }

            // ── 3. Show Main Window ──
            var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainVm);
            mainWindow.ShowDialog();

            // ── 4. Handle close reason ──
            if (mainVm.Reason == MainViewModel.CloseReason.None)
            {
                Shutdown();
                return;
            }

            if (mainVm.Reason == MainViewModel.CloseReason.ZClose)
            {
                sessionState.Close();
            }
            else if (mainVm.Reason == MainViewModel.CloseReason.Logout)
            {
                if (sessionState.IsSetupMode)
                    sessionState.ExitSetupMode();
            }

            authService.Logout();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  SESSION ACTION RESOLVER
    // ═══════════════════════════════════════════════════════

    private enum SessionAction
    {
        ShowDialog,
        JoinExisting,
        BlockedWrongPos,
        BlockedNoPos,
        BypassToSetup
    }

    private static SessionAction ResolveSessionAction(
        IAuthService authService, CashSessionState sessionState)
    {
        if (!sessionState.IsSessionOpen)
            return SessionAction.ShowDialog;

        var user = authService.CurrentUser;
        if (user == null)
            return SessionAction.ShowDialog;

        var openPosId = sessionState.Current!.PointOfSaleId;
        var userPosId = user.PointOfSaleId;
        bool canBypass = authService.HasPermission("bypassPosCheck");

        if (userPosId.HasValue && userPosId.Value == openPosId)
            return SessionAction.JoinExisting;

        if (userPosId.HasValue && userPosId.Value != openPosId)
        {
            if (canBypass) return SessionAction.BypassToSetup;
            return SessionAction.BlockedWrongPos;
        }

        if (!userPosId.HasValue)
        {
            if (canBypass) return SessionAction.BypassToSetup;
            return SessionAction.BlockedNoPos;
        }

        return SessionAction.ShowDialog;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ═══ Base de données ═══
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SFE");
        Directory.CreateDirectory(appDataPath);
        var dbPath = Path.Combine(appDataPath, "sfe.db");
        var connString = $"Data Source={dbPath};Cache=Shared";

        // Stateless interceptor — safe as singleton
        var walInterceptor = new SqliteWalInterceptor();

        // ─── Sync tracking interceptor (stateful, needs DI) ───
        services.AddScoped<SyncTrackingInterceptor>();

        services.AddDbContext<AppDbContext>((sp, opt) =>
        {
            opt.UseSqlite(connString)
               .AddInterceptors(
                   walInterceptor,
                   sp.GetRequiredService<SyncTrackingInterceptor>());
        }, ServiceLifetime.Transient);

        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

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
        services.AddScoped<IInvoiceAdvanceService, InvoiceAdvanceService>();
        services.AddSingleton<TenantContext>();
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());

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

        // ── Audit ──
        services.AddSingleton<IAuditWriter, AuditWriter>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddTransient<AuditLogViewModel>();

        // ═══ Fenêtres & Pages ═══
        services.AddTransient<MainWindow>();
        services.AddTransient<ClientsPage>();
        services.AddTransient<ReportView>();
        services.AddTransient<InvoiceDocumentView>();
        services.AddTransient<PosManagementPage>();
    }

    private static async Task InitializeDatabaseAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeeder.SeedAsync(context);
    }
}