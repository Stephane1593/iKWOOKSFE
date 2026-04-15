using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    // ═══════════════ CURRENT PAGE ═══════════════
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _currentPageKey = "";

    // ═══════════════ USER INFO ═══════════════
    [ObservableProperty] private string _currentUserName = "";
    [ObservableProperty] private string _currentRoleName = "";
    [ObservableProperty] private string _userInitials = "?";

    // ═══════════════ POS / COMPANY INFO ═══════════════
    [ObservableProperty] private string _currentPosName = "";
    [ObservableProperty] private string _currentPosCode = "";
    [ObservableProperty] private string _currentPosCity = "";
    [ObservableProperty] private string _companyName = "";

    // ═══════════════ DEVICE STATUS ═══════════════
    [ObservableProperty] private string _deviceStatus = "";
    [ObservableProperty] private string _deviceStatusShort = "MCF";
    [ObservableProperty] private bool _isDeviceOnline = true;

    // ═══════════════ LOGOUT ═══════════════
    public bool LogoutRequested { get; private set; }
    public event Action? RequestClose;

    // ═══════════════ PERMISSIONS (bound to Visibility in XAML) ═══════════════
    public bool CanAccessDashboard => _authService.HasPermission("dashboard");
    public bool CanAccessPos => _authService.HasPermission("pos");
    public bool CanAccessInvoicing => _authService.HasPermission("invoicing");
    public bool CanAccessClients => _authService.HasPermission("clients");
    public bool CanAccessSalesHistory => _authService.HasPermission("salesHistory");
    public bool CanAccessProducts => _authService.HasPermission("products");
    public bool CanAccessStock => _authService.HasPermission("stock");
    public bool CanAccessTransfers => _authService.HasPermission("transfers");
    public bool CanAccessLoyalty => _authService.HasPermission("loyalty");
    public bool CanAccessReports => _authService.HasPermission("reports");
    public bool CanAccessSettings => _authService.HasPermission("settings");
    public bool CanAccessUsers => _authService.HasPermission("users");

    // Group-level visibility (dropdown shows if ≥ 1 child visible)
    public bool CanSeeVentes => CanAccessPos || CanAccessInvoicing || CanAccessClients || CanAccessSalesHistory;
    public bool CanSeeGestion => CanAccessProducts || CanAccessStock || CanAccessTransfers || CanAccessLoyalty;
    public bool CanSeeRapports => CanAccessReports;
    public bool CanSeeAdmin => CanAccessSettings || CanAccessUsers;

    // ═══════════════ PAGE CACHE ═══════════════
    private readonly Dictionary<string, object> _pages = new();

    // ═══════════════ CONSTRUCTOR ═══════════════
    public MainViewModel(IAuthService authService)
    {
        _authService = authService;
        PageTitle = "iKWOOK SFE";

        LoadUserContext();
        NavigateToDefaultPage();
    }

    // ═══════════════════════════════════════════
    //  INITIALISATION
    // ═══════════════════════════════════════════

    private void LoadUserContext()
    {
        var user = _authService.CurrentUser;
        if (user == null) return;

        CurrentUserName = user.FullName;
        CurrentRoleName = user.Role?.Name ?? "";
        UserInitials = _authService.GetUserInitials();

        // Load POS + company info (fire-and-forget on UI thread)
        _ = LoadPosAndCompanyAsync(user);
    }

    private async Task LoadPosAndCompanyAsync(Domain.Entities.User user)
    {
        try
        {
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // POS
            var posIds = JsonSerializer.Deserialize<int[]>(user.AssignedPosIds ?? "[]") ?? [];
            if (posIds.Length > 0)
            {
                var pos = await uow.PointsOfSale.GetByIdAsync(posIds[0]);
                if (pos != null)
                {
                    CurrentPosName = pos.Name;
                    CurrentPosCode = pos.Code;
                    CurrentPosCity = pos.City;
                }
            }

            // Company
            var companies = await uow.Companies.GetAllAsync();
            var comp = companies.FirstOrDefault();
            if (comp != null)
            {
                CompanyName = comp.Name;
                if (string.IsNullOrEmpty(CurrentPosName))
                {
                    CurrentPosName = comp.Name;
                    CurrentPosCity = comp.City;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainVM] LoadPosAndCompany error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════
    //  NAVIGATION
    // ═══════════════════════════════════════════

    private void NavigateToDefaultPage()
    {
        if (CanAccessDashboard) NavigateToPage("Dashboard");
        else if (CanAccessPos) NavigateToPage("Cash");
        else if (CanAccessInvoicing) NavigateToPage("Invoicing");
        else if (CanAccessReports) NavigateToPage("Reports");
        else if (CanAccessSalesHistory) NavigateToPage("SalesHistory");
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
                "Products" => CreatePage<ProductsPage, ProductsViewModel>(),
                "Clients" => CreateClientsPage(),
                "Stock" => CreatePage<StockPage, StockViewModel>(),
                "StockTransfer" => CreatePage<StockTransferPage, StockTransferViewModel>(),
                "Settings" => CreatePage<SettingsPage, SettingsViewModel>(),
                "SalesHistory" => CreatePage<SalesHistoryPage, SalesHistoryViewModel>(),
                "Reports" => CreatePage<ReportView, ReportViewModel>(),
                "Loyalty" => new PlaceholderPage("Fidélité",
                                       "Le programme de fidélité sera implémenté ici."),
                "Users" => new PlaceholderPage("Gestion des utilisateurs",
                                       "La gestion des utilisateurs sera implémentée ici."),
                _ => new PlaceholderPage("Page inconnue", "")
            };
        }

        CurrentPage = _pages[pageKey];
        CurrentPageKey = pageKey;

        // Activate cached pages that implement IActivatable
        if (CurrentPage is System.Windows.FrameworkElement { DataContext: IActivatable activatable })
        {
            _ = activatable.ActivateAsync();
        }
    }

    // ═══════════════════════════════════════════
    //  LOGOUT
    // ═══════════════════════════════════════════

    [RelayCommand]
    private void Logout()
    {
        LogoutRequested = true;
        RequestClose?.Invoke();
    }

    // ═══════════════════════════════════════════
    //  FACTORIES
    // ═══════════════════════════════════════════

    private static TPage CreatePage<TPage, TViewModel>()
        where TPage : System.Windows.FrameworkElement, new()
        where TViewModel : notnull
    {
        var vm = App.ServiceProvider.GetRequiredService<TViewModel>();
        var page = new TPage { DataContext = vm };
        return page;
    }

    private static ClientsPage CreateClientsPage()
    {
        var vm = App.ServiceProvider.GetRequiredService<ClientsViewModel>();
        return new ClientsPage(vm);
    }
}