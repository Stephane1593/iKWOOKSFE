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
using QuestPDF.Infrastructure;
using System.Text;
using SFE.Domain.Abstractions;
using SFE.Infrastructure.Persistence.Repositories;
using SFE.Api;

namespace SFE.WPF;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private SfeApiHost? _api;
    private PaymentReconciliationService? _reconciler;
    private CancellationTokenSource? _reconcilerCts;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // ── Start the local HTTP + mDNS host ──
        _api = new SfeApiHost(ServiceProvider);
        await _api.StartAsync();

        // ── Start the payment reconciler (BackgroundService, started manually
        //    because WPF isn't running under an IHost) ──
        _reconciler = ServiceProvider.GetRequiredService<PaymentReconciliationService>();
        _reconcilerCts = new CancellationTokenSource();
        await _reconciler.StartAsync(_reconcilerCts.Token);

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
                        // Same POS — shift handover
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

                        // Update the operator name on the session
                        sessionState.Current!.OperatorName = authService.CurrentUser!.FullName;
                        break;
                    }

                case SessionAction.BlockedWrongPos:
                    {
                        // User is assigned to a different POS than the open session
                        var session = sessionState.Current!;
                        var user = authService.CurrentUser!;
                        var userPosId = user.PointOfSaleId;

                        // Try to get the user's POS name for the message
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
                            catch { /* fallback to generic message */ }
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
                        continue; // back to login
                    }

                case SessionAction.BlockedNoPos:
                    {
                        // User has no POS assigned AND can't bypass — blocked
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
                        // IT Tech with active session on different POS — enter setup mode
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

                        // Don't touch the existing session — just layer setup mode
                        sessionState.EnterSetupMode(authService.CurrentUser!.FullName);
                        break;
                    }

                case SessionAction.ShowDialog:
                    {
                        // No session open — normal flow
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
                // Z-close: clear everything
                sessionState.Close();
            }
            else if (mainVm.Reason == MainViewModel.CloseReason.Logout)
            {
                // Regular logout
                if (sessionState.IsSetupMode)
                {
                    // IT Tech was in setup mode — just exit setup, preserve session
                    sessionState.ExitSetupMode();
                }
                // If a normal session is open, it stays open for next user
            }

            authService.Logout();
        }

    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_reconciler is not null)
        {
            _reconcilerCts?.Cancel();
            try { await _reconciler.StopAsync(CancellationToken.None); } catch { /* shutting down */ }
            _reconcilerCts?.Dispose();
        }
        if (_api is not null) await _api.StopAsync();
        base.OnExit(e);
    }

    // ═══════════════════════════════════════════════════════
    //  SESSION ACTION RESOLVER
    // ═══════════════════════════════════════════════════════

    private enum SessionAction
    {
        ShowDialog,       // No session — show normal dialog
        JoinExisting,     // Same POS — join session
        BlockedWrongPos,  // Different POS — can't work
        BlockedNoPos,     // No POS assigned, can't bypass
        BypassToSetup     // IT Tech — setup mode
    }

    private static SessionAction ResolveSessionAction(
        IAuthService authService, CashSessionState sessionState)
    {
        // No session open → normal flow
        if (!sessionState.IsSessionOpen)
            return SessionAction.ShowDialog;

        var user = authService.CurrentUser;
        if (user == null)
            return SessionAction.ShowDialog;

        var openPosId = sessionState.Current!.PointOfSaleId;
        var userPosId = user.PointOfSaleId;
        bool canBypass = authService.HasPermission("bypassPosCheck");

        // ── Case 1: User assigned to the SAME POS → join ──
        if (userPosId.HasValue && userPosId.Value == openPosId)
            return SessionAction.JoinExisting;

        // ── Case 2: User assigned to DIFFERENT POS ──
        if (userPosId.HasValue && userPosId.Value != openPosId)
        {
            // IT Tech with bypass → can enter setup mode
            if (canBypass)
                return SessionAction.BypassToSetup;

            // Regular user → blocked
            return SessionAction.BlockedWrongPos;
        }

        // ── Case 3: User has NO POS assigned ──
        if (!userPosId.HasValue)
        {
            // IT Tech → setup mode
            if (canBypass)
                return SessionAction.BypassToSetup;

            // Regular user without POS → blocked
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

        var walInterceptor = new SqliteWalInterceptor();

        services.AddLogging(); // safe to call even if you never wire a provider
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Cache=Shared")
                   .AddInterceptors(walInterceptor),
            ServiceLifetime.Transient);

        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        // ═══ Repositories & Unit of Work ═══
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

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



        // --- payment skeleton ---
        // Program.cs / App.xaml.cs — DI composition
        services.AddSingleton<MockPaymentProvider>(_ => new MockPaymentProvider
        {
            Mode = MockPaymentProvider.Behaviour.ExternallyDriven
        });
        services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<MockPaymentProvider>());
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<PaymentService>();
        services.AddScoped<IPendingOrderProvider, InvoicePendingOrderProvider>();
        services.AddSingleton<InMemoryPendingOrderStore>();
        // Reconciler config + registration. It's a BackgroundService, so we register
        // it as a singleton and start/stop it by hand in Application_Startup / OnExit
        // (WPF has no IHost, so AddHostedService would be a no-op).
        services.Configure<PaymentReconciliationOptions>(o =>
        {
            o.PollInterval = TimeSpan.FromSeconds(2);
            o.StuckAfter = TimeSpan.FromSeconds(2);
            o.MaxAttempts = 30;
        });
        services.AddSingleton<PaymentReconciliationService>();


        
        services.AddSingleton(sp =>
        {
            // TODO: replace this with a secret saved in SettingsService or pairing config.
            // Both the caisse and Sunmi must use the same secret to verify QR signatures.
            var secretText = "ikwookQrcodebaby";
            var secret = Encoding.UTF8.GetBytes(secretText);

            return new OfflineQrService(
                pairingSecret: secret,
                caisseId: Environment.MachineName);
        });
        services.AddScoped<OfflineQrResolver>();   // scoped: it uses IInvoiceRepository (DbContext)
        // ═══ ViewModels ═══
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<InvoicingViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<ClientsViewModel>();
        services.AddTransient<StockViewModel>();
        services.AddTransient<StockTransferViewModel>();
        services.AddSingleton<SettingsViewModel>();
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