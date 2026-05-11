using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.WPF.Services;
using SFE.WPF.Views.Pages;
using SFE.WPF.Views;
using System.Windows.Threading;
using System.Diagnostics;
using SFE.Application.Events;
using SFE.Domain.Abstractions;

namespace SFE.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly CashSessionState _sessionState;
    private readonly ITimeProvider _timeProvider;   // 🆕

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _currentPageKey = "";

    [ObservableProperty] private string _currentUserName = "";
    [ObservableProperty] private string _currentRoleName = "";
    [ObservableProperty] private string _userInitials = "?";

    [ObservableProperty] private string _currentPosName = "";
    [ObservableProperty] private string _currentPosCode = "";
    [ObservableProperty] private string _currentPosCity = "";
    [ObservableProperty] private string _companyName = "";

    [ObservableProperty] private string _deviceStatus = "";
    [ObservableProperty] private string _deviceStatusShort = "MCF";
    [ObservableProperty] private bool _isDeviceOnline = true;

    [ObservableProperty] private bool _isSetupMode;
    [ObservableProperty] private string _sessionBanner = "";

    // ═══ CLOSE REASON ═══
    public enum CloseReason { None, Logout, ZClose }
    public CloseReason Reason { get; private set; }
    public bool LogoutRequested => Reason != CloseReason.None;
    public event Action? RequestClose;

    // ═══ NOTIFICATIONS ═══
    [ObservableProperty] private bool _showNotificationBanner;
    [ObservableProperty] private string _notificationMessage = "";
    [ObservableProperty] private string _notificationType = "warning";
    [ObservableProperty] private bool _isMcfDisconnectedWarning;
    [ObservableProperty] private string _activeDeviceLabel = "—";

    private readonly DispatcherTimer? _deviceCheckTimer;

    // ═══════════════════════════════════════════════════════
    //  PERMISSIONS
    // ═══════════════════════════════════════════════════════
    public bool CanAccessDashboard => _authService.HasPermission("dashboard");
    public bool CanAccessPos => _authService.HasPermission("pos") && _sessionState.IsSessionOpen;
    public bool CanAccessInvoicing => _authService.HasPermission("invoicing") && _sessionState.IsSessionOpen;
    public bool CanAccessClients => _authService.HasPermission("clients");
    public bool CanAccessSalesHistory => _authService.HasPermission("salesHistory");
    public bool CanAccessProducts => _authService.HasPermission("products");
    public bool CanAccessStock => _authService.HasPermission("stock");
    public bool CanAccessTransfers => _authService.HasPermission("transfers");
    public bool CanAccessLoyalty => _authService.HasPermission("loyalty");

    public bool CanAccessReports => (_authService.HasPermission("reports")
                                  || _authService.HasPermission("closeZ"))
                                  && _sessionState.IsSessionOpen;

    public bool CanAccessReportHistory => _authService.HasPermission("reports");
    public bool CanAccessSettings => _authService.HasPermission("settings");
    public bool CanAccessUsers => _authService.HasPermission("users");
    public bool CanAccessAudit => _authService.HasPermission("audit");

    public bool CanCloseZ => _authService.HasPermission("closeZ")
                          && _sessionState.IsSessionOpen
                          && !_sessionState.IsSetupMode;

    // ═══════════════════════════════════════════════════════
    //  DROPDOWN TOGGLES
    // ═══════════════════════════════════════════════════════
    public bool CanSeeFichier => _authService.HasPermission("pos")
                               || _authService.HasPermission("invoicing");

    public bool CanSeeEditer => _authService.HasPermission("products")
                               || _authService.HasPermission("clients")
                               || _authService.HasPermission("stock")
                               || _authService.HasPermission("reports");

    public bool CanSeeAffichage => _authService.HasPermission("reports")
                                || _authService.HasPermission("closeZ")
                                || _authService.HasPermission("salesHistory");

    public bool CanSeeOutils => _authService.HasPermission("settings")
                               || _authService.HasPermission("users")
                               || _authService.HasPermission("audit");

    private readonly Dictionary<string, object> _pages = new();

    public MainViewModel(
        IAuthService authService,
        CashSessionState sessionState,
        ITimeProvider timeProvider)          // 🆕
    {
        _authService = authService;
        _sessionState = sessionState;
        _timeProvider = timeProvider;        // 🆕
        PageTitle = "iKWOOK SFE";

        IsSetupMode = sessionState.IsSetupMode;
        SessionBanner = sessionState.IsSetupMode
            ? "⚙ Mode Configuration — Accès limité aux paramètres"
            : "";

        LoadUserContext();
        NavigateToDefaultPage();

        AppEventBus.Subscribe(OnAppEvent);

        if (!sessionState.IsSetupMode)
        {
            _ = CheckDeviceStatusAsync();
            _deviceCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _deviceCheckTimer.Tick += async (_, _) => await CheckDeviceStatusAsync();
            _deviceCheckTimer.Start();
        }
    }

    private async Task OnAppEvent(AppEventArgs args)
    {
        if (args.Event == AppEvent.FiscalDeviceStatusChanged)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                async () => await CheckDeviceStatusAsync());
        }
    }

    // ═══════════════════════════════════════════════════════
    //  INITIALISATION
    // ═══════════════════════════════════════════════════════

    private void LoadUserContext()
    {
        var user = _authService.CurrentUser;
        if (user == null) return;

        CurrentUserName = user.FullName;
        CurrentRoleName = user.Role?.Name ?? "";
        UserInitials = _authService.GetUserInitials();

        if (_sessionState.IsSessionOpen && _sessionState.Current != null)
        {
            var s = _sessionState.Current;
            CurrentPosName = s.PointOfSaleName;
            CurrentPosCode = s.PointOfSaleCode ?? "";
            CurrentPosCity = s.PointOfSaleCity ?? "";
        }

        _ = LoadCompanyAndFallbackPosAsync(user);
    }

    private async Task LoadCompanyAndFallbackPosAsync(Domain.Entities.User user)
    {
        try
        {
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();

            if (!_sessionState.IsSessionOpen && !_sessionState.IsSetupMode)
            {
                if (user.PointOfSaleId.HasValue)
                {
                    var pos = user.PointOfSale
                              ?? await uow.PointsOfSale.GetByIdAsync(user.PointOfSaleId.Value);
                    if (pos != null)
                    {
                        CurrentPosName = pos.Name;
                        CurrentPosCode = pos.Code;
                        CurrentPosCity = pos.City;
                    }
                }
            }

            var companies = await uow.Companies.GetAllAsync();
            var comp = companies.FirstOrDefault();
            if (comp != null)
            {
                CompanyName = comp.Name;
                if (string.IsNullOrEmpty(CurrentPosName) && !_sessionState.IsSetupMode)
                {
                    CurrentPosName = comp.Name;
                    CurrentPosCity = comp.City;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainVM] LoadCompanyAndFallbackPos error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  NAVIGATION
    // ═══════════════════════════════════════════════════════

    private void NavigateToDefaultPage()
    {
        if (_sessionState.IsSetupMode)
        {
            if (CanAccessSettings) NavigateToPage("Settings");
            else if (CanAccessUsers) NavigateToPage("Users");
            else NavigateToPage("Dashboard");
            return;
        }

        if (CanAccessDashboard) NavigateToPage("Dashboard");
        else if (CanAccessPos) NavigateToPage("Cash");
        else if (CanAccessInvoicing) NavigateToPage("Invoicing");
        else if (CanAccessReports) NavigateToPage("ReportX");
        else if (CanAccessSalesHistory) NavigateToPage("SalesJournal");
        else NavigateToPage("Dashboard");
    }

    [RelayCommand]
    private void NavigateToPage(string pageKey)
    {
        if (string.IsNullOrEmpty(pageKey)) return;

        if (!_pages.ContainsKey(pageKey))
        {
            _pages[pageKey] = pageKey switch
            {
                "Dashboard" => CreatePage<DashboardPage, DashboardViewModel>(),
                "Cash" => CreatePage<PosPage, PosViewModel>(),
                "Invoicing" => CreatePage<InvoicingPage, InvoicingViewModel>(),
                "Articles" => CreatePage<ProductsPage, ProductsViewModel>(),
                "Categories" => CreatePage<CategoriesPage, CategoriesViewModel>(),
                "Clients" => CreateClientsPage(),
                "Stock" => CreatePage<StockPage, StockViewModel>(),
                "ReportZ" => CreateReportZPage(),
                "ReportX" => CreatePage<ReportXPage, ReportXPageViewModel>(),
                "ReportA" => CreatePage<ReportAPage, ReportAPageViewModel>(),
                "SalesJournal" => CreatePage<SalesHistoryPage, SalesHistoryViewModel>(),
                "ReportHistory" => CreatePage<ReportView, ReportViewModel>(),
                "Settings" => CreatePage<SettingsPage, SettingsViewModel>(),
                "Users" => CreatePage<UsersPage, UsersViewModel>(),
                "StockTransfer" => CreatePage<StockTransferPage, StockTransferViewModel>(),
                "PosManagement" => CreatePage<PosManagementPage, PointOfSaleManagementViewModel>(),

                "AuditLog" => CreatePage<AuditLogPage, AuditLogViewModel>(),
                "UserManual" => new PlaceholderPage("Manuel d'utilisation",
                                    "Le manuel d'utilisation au format PDF sera intégré ici."),
                _ => new PlaceholderPage("Page inconnue", "")
            };
        }

        CurrentPage = _pages[pageKey];
        CurrentPageKey = pageKey;

        if (CurrentPage is System.Windows.FrameworkElement { DataContext: IActivatable activatable })
            _ = activatable.ActivateAsync();
    }

    // ═══════════════════════════════════════════════════════
    //  CLÔTURE Z
    // ═══════════════════════════════════════════════════════

    [RelayCommand]
    private void CloseReportZ()
    {
        if (!_authService.HasPermission("closeZ"))
        {
            System.Windows.MessageBox.Show(
                "Vous n'avez pas l'autorisation de clôturer la session (droit « Clôture Z » requis).",
                "Accès refusé",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (_sessionState.IsSetupMode)
        {
            System.Windows.MessageBox.Show(
                "La clôture du rapport Z n'est pas disponible en mode configuration.",
                "Mode Configuration",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (!_sessionState.IsSessionOpen)
        {
            System.Windows.MessageBox.Show(
                "Aucune session active à clôturer.",
                "Clôture Z",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            "Voulez-vous clôturer la session et générer le rapport Z ?\n\n" +
            "Cette action est irréversible. Toutes les ventes de la session\n" +
            "seront comptabilisées et la session sera fermée.",
            "Clôture de Session",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        var vm = App.ServiceProvider.GetRequiredService<SessionCloseViewModel>();
        var dialog = new Views.Pages.SessionCloseDialog { DataContext = vm };

        var mainWindow = System.Windows.Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current.MainWindow;

        if (mainWindow != null && mainWindow != dialog)
            dialog.Owner = mainWindow;

        var result = dialog.ShowDialog();

        if (result == true)
        {
            Reason = CloseReason.ZClose;
            RequestClose?.Invoke();
        }
    }

    [RelayCommand]
    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "iKWOOK SFE v2.0\nSystème de Facturation d'Entreprise\n\n© 2026 · Conforme DGI-RDC\n\nDéveloppé par iKWOOK.\nTous droits réservés.",
            "À propos de iKWOOK SFE",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    // ═══════════════════════════════════════════════════════
    //  LOGOUT
    // ═══════════════════════════════════════════════════════

    [RelayCommand]
    private void Logout()
    {
        Reason = CloseReason.Logout;
        RequestClose?.Invoke();
    }

    // ═══════════════════════════════════════════════════════
    //  FACTORIES
    // ═══════════════════════════════════════════════════════

    private static TPage CreatePage<TPage, TViewModel>()
        where TPage : System.Windows.FrameworkElement, new()
        where TViewModel : notnull
    {
        var vm = App.ServiceProvider.GetRequiredService<TViewModel>();
        return new TPage { DataContext = vm };
    }

    private static ClientsPage CreateClientsPage()
    {
        var vm = App.ServiceProvider.GetRequiredService<ClientsViewModel>();
        return new ClientsPage(vm);
    }

    private ReportZPage CreateReportZPage()
    {
        var vm = App.ServiceProvider.GetRequiredService<ReportZPageViewModel>();
        vm.SessionClosedByZ += () =>
        {
            Reason = CloseReason.ZClose;
            RequestClose?.Invoke();
        };
        return new ReportZPage { DataContext = vm };
    }

    [RelayCommand]
    private async Task CheckDeviceStatusAsync()
    {
        try
        {
            var fiscalDevice = App.ServiceProvider.GetRequiredService<IFiscalDeviceService>();

            var status = await fiscalDevice.GetStatusAsync();
            IsDeviceOnline = status.Success;

            if (status.Success)
            {
                DeviceStatusShort = status.NIM ?? "MCF";
                DeviceStatus = $"Connecté · {status.NIM}";

                if (fiscalDevice is FiscalDeviceResolver resolver)
                    ActiveDeviceLabel = resolver.ActiveDeviceLabel;

                await CheckDgiConnectionAsync(fiscalDevice);
            }
            else
            {
                DeviceStatusShort = "MCF";
                DeviceStatus = "Dispositif fiscal hors ligne";

                NotificationMessage = "Impossible de contacter le dispositif fiscal. Vérifiez la configuration dans Outils → Paramètres.";
                NotificationType = "error";
                ShowNotificationBanner = true;
                IsMcfDisconnectedWarning = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainVM] Device check error: {ex.Message}");
            IsDeviceOnline = false;
            DeviceStatus = "Dispositif fiscal hors ligne";
            DeviceStatusShort = "MCF";
        }
    }

    private async Task CheckDgiConnectionAsync(IFiscalDeviceService fiscalDevice)
    {
        try
        {
            var serverStatus = await fiscalDevice.GetServerConnectionStatusAsync();

            if (!serverStatus.Success)
            {
                ShowNotificationBanner = false;
                IsMcfDisconnectedWarning = false;
                return;
            }

            if (serverStatus.IsOverSevenDays)
            {
                // 🆕 Utilise ITimeProvider au lieu de DateTime.Now
                var daysSince = serverStatus.LastServerConnection.HasValue
                    ? (_timeProvider.LocalNow - serverStatus.LastServerConnection.Value).Days
                    : 7;

                NotificationMessage = $"⚠ Le MCF n'a pas communiqué avec le serveur DGI depuis {daysSince} jour(s). " +
                                      "Vérifiez la connexion réseau du dispositif (DGI §1.6.1 — blocage après 7 jours).";
                NotificationType = daysSince >= 7 ? "error" : "warning";
                ShowNotificationBanner = true;
                IsMcfDisconnectedWarning = true;
            }
            else if (serverStatus.ConnectionStatus == "DIS")
            {
                NotificationMessage = "Le MCF n'est pas connecté au réseau. Les factures seront transmises au rétablissement.";
                NotificationType = "info";
                ShowNotificationBanner = true;
                IsMcfDisconnectedWarning = false;
            }
            else
            {
                ShowNotificationBanner = false;
                IsMcfDisconnectedWarning = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainVM] DGI connection check error: {ex.Message}");
            ShowNotificationBanner = false;
        }
    }

    [RelayCommand]
    private void DismissNotification()
    {
        ShowNotificationBanner = false;
    }

    [RelayCommand]
    private async Task RestartMcfSync()
    {
        try
        {
            var fiscalDevice = App.ServiceProvider.GetRequiredService<IFiscalDeviceService>();
            var result = await fiscalDevice.GetServerConnectionStatusAsync();
            if (result.Success)
            {
                NotificationMessage = "✓ Synchronisation relancée.";
                NotificationType = "info";
                await Task.Delay(3000);
                await CheckDeviceStatusAsync();
            }
        }
        catch (Exception ex)
        {
            NotificationMessage = $"Erreur: {ex.Message}";
            NotificationType = "error";
        }
    }
}