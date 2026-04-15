using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.WPF.Services;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly CashSessionState _sessionState;

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

    // ═══════════════ SESSION / SETUP MODE ═══════════════
    [ObservableProperty] private bool _isSetupMode;
    [ObservableProperty] private string _sessionBanner = "";

    // ═══════════════ LOGOUT ═══════════════
    public bool LogoutRequested { get; private set; }
    public event Action? RequestClose;

    // ═══════════════════════════════════════════════════════
    //  PERMISSIONS  (bound to Visibility in XAML)
    // ═══════════════════════════════════════════════════════

    public bool CanAccessDashboard => _authService.HasPermission("dashboard");

    // POS & Invoicing require an active cash session — blocked in setup mode
    public bool CanAccessPos => _authService.HasPermission("pos") && _sessionState.IsSessionOpen;
    public bool CanAccessInvoicing => _authService.HasPermission("invoicing") && _sessionState.IsSessionOpen;

    public bool CanAccessClients => _authService.HasPermission("clients");
    public bool CanAccessSalesHistory => _authService.HasPermission("salesHistory");
    public bool CanAccessProducts => _authService.HasPermission("products");
    public bool CanAccessStock => _authService.HasPermission("stock");
    public bool CanAccessTransfers => _authService.HasPermission("transfers");
    public bool CanAccessLoyalty => _authService.HasPermission("loyalty");
    public bool CanAccessReports => _authService.HasPermission("reports") && _sessionState.IsSessionOpen;
    public bool CanAccessSettings => _authService.HasPermission("settings");
    public bool CanAccessUsers => _authService.HasPermission("users");

    // ── Navbar group visibility ──
    public bool CanSeeFichier => CanAccessPos || CanAccessInvoicing;
    public bool CanSeeEditer => CanAccessProducts || CanAccessClients
                                || CanAccessStock || CanAccessReports;
    public bool CanSeeAffichage => CanAccessReports || CanAccessSalesHistory;
    public bool CanSeeOutils => CanAccessSettings || CanAccessUsers;

    // ═══════════════ PAGE CACHE ═══════════════
    private readonly Dictionary<string, object> _pages = new();

    // ═══════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ═══════════════════════════════════════════════════════

    public MainViewModel(IAuthService authService, CashSessionState sessionState)
    {
        _authService = authService;
        _sessionState = sessionState;
        PageTitle = "iKWOOK SFE";

        IsSetupMode = sessionState.IsSetupMode;
        SessionBanner = sessionState.IsSetupMode
            ? "⚙ Mode Configuration — Accès limité aux paramètres"
            : "";

        LoadUserContext();
        NavigateToDefaultPage();
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

        // POS info from session (if a real session is open)
        if (_sessionState.IsSessionOpen && _sessionState.Current != null)
        {
            var s = _sessionState.Current;
            CurrentPosName = s.PointOfSaleName;
            CurrentPosCode = s.PointOfSaleCode ?? "";
            CurrentPosCity = s.PointOfSaleCity ?? "";
        }

        // Company info is always useful; fallback POS only when no session
        _ = LoadCompanyAndFallbackPosAsync(user);
    }

    private async Task LoadCompanyAndFallbackPosAsync(Domain.Entities.User user)
    {
        try
        {
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Fallback POS from assigned list (only if no session & not setup mode)
            if (!_sessionState.IsSessionOpen && !_sessionState.IsSetupMode)
            {
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
            }

            // Company (always loaded)
            var companies = await uow.Companies.GetAllAsync();
            var comp = companies.FirstOrDefault();
            if (comp != null)
            {
                CompanyName = comp.Name;

                // Only use company as POS fallback in normal mode without session
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
        // Setup mode → land on Settings (or Dashboard as last resort)
        if (_sessionState.IsSetupMode)
        {
            if (CanAccessSettings) NavigateToPage("Settings");
            else if (CanAccessUsers) NavigateToPage("Users");
            else NavigateToPage("Dashboard");
            return;
        }

        // Normal session flow
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
                // ── Tableau de bord ──
                "Dashboard" => CreatePage<DashboardPage, DashboardViewModel>(),

                // ── Fichier ──
                "Cash" => CreatePage<PosPage, PosViewModel>(),
                "Invoicing" => CreatePage<InvoicingPage, InvoicingViewModel>(),

                // ── Éditer ──
                "Articles" => CreatePage<ProductsPage, ProductsViewModel>(),
                "Categories" => new PlaceholderPage("Catégories",
                                       "La gestion des catégories de produits sera implémentée ici."),
                "Clients" => CreateClientsPage(),
                "Stock" => CreatePage<StockPage, StockViewModel>(),

                // ── Affichage > Rapports ──
                "ReportX" => new PlaceholderPage("Rapport X",
                                       "Rapport X — État des ventes de la journée en cours."),
                "ReportA" => new PlaceholderPage("Rapport A",
                                       "Rapport A — Récapitulatif des annulations."),
                "ReportZ" => new PlaceholderPage("Rapport Z",
                                       "Rapport Z — Rapport de clôture de journée."),
                "SalesJournal" => CreatePage<SalesHistoryPage, SalesHistoryViewModel>(),
                "ReportHistory" => new PlaceholderPage("Historique des rapports",
                                       "Consultation et filtrage des rapports par période et par type."),

                // ── Outils ──
                "Settings" => CreatePage<SettingsPage, SettingsViewModel>(),
                "PosManagement" => new PlaceholderPage("Gestion des points de vente",
                                       "Configuration et gestion des points de vente."),
                "Users" => new PlaceholderPage("Gestion des utilisateurs",
                                       "Création et gestion des comptes utilisateurs et des rôles."),
                "AuditLog" => new PlaceholderPage("Journal d'audit",
                                       "Rapports MCF, rapports e-MCF et journal des activités utilisateurs."),

                // ── Aide ──
                "UserManual" => new PlaceholderPage("Manuel d'utilisation",
                                       "Le manuel d'utilisation au format PDF sera intégré ici."),

                // ── Backward compat (hidden from new nav) ──
                "Reports" => CreatePage<ReportView, ReportViewModel>(),
                "StockTransfer" => CreatePage<StockTransferPage, StockTransferViewModel>(),

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

    // ═══════════════════════════════════════════════════════
    //  CLÔTURE DU RAPPORT Z  (Phase 3 — placeholder)
    // ═══════════════════════════════════════════════════════

    [RelayCommand]
    private void CloseReportZ()
    {
        if (_sessionState.IsSetupMode)
        {
            System.Windows.MessageBox.Show(
                "La clôture du rapport Z n'est pas disponible en mode configuration.",
                "Mode Configuration",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        // TODO Phase 3: open SessionCloseDialog (same layout as Ouverture de session)
        System.Windows.MessageBox.Show(
            "La fonctionnalité de clôture du rapport Z sera implémentée prochainement.\n\n" +
            "Ce dialogue permettra de saisir les montants de clôture de caisse\n" +
            "par devise (USD, EUR, CNY, CDF).",
            "Clôture du rapport Z",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    // ═══════════════════════════════════════════════════════
    //  À PROPOS
    // ═══════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "iKWOOK SFE v2.0\n" +
            "Système de Facturation d'Entreprise\n\n" +
            "© 2026 · Conforme DGI-RDC\n\n" +
            "Développé par iKWOOK.\nTous droits réservés.",
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
        LogoutRequested = true;
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
        var page = new TPage { DataContext = vm };
        return page;
    }

    private static ClientsPage CreateClientsPage()
    {
        var vm = App.ServiceProvider.GetRequiredService<ClientsViewModel>();
        return new ClientsPage(vm);
    }
}