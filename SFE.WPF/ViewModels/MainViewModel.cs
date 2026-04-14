// File: SFE.WPF/ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavigationItem? _selectedNavItem;

    [ObservableProperty]
    private bool _isSidebarCollapsed = false;

    [ObservableProperty]
    private string _currentUserName = "Admin";

    [ObservableProperty]
    private string _currentPosName = "POS-001 Principal";

    [ObservableProperty]
    private string _deviceStatus = "";

    [ObservableProperty]
    private bool _isDeviceOnline = true;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new()
    {
        new NavigationItem { Label = "Tableau de bord", IconGlyph = "\uE80F", PageKey = "Dashboard" },
        new NavigationItem { Label = "Caisse",          IconGlyph = "\uE8C7", PageKey = "Cash" },
        new NavigationItem { Label = "Facturation",     IconGlyph = "\uE8A5", PageKey = "Invoicing" },
        new NavigationItem { Label = "Produits",        IconGlyph = "\uE719", PageKey = "Products" },
        new NavigationItem { Label = "Clients",         IconGlyph = "\uE77B", PageKey = "Clients" },
        new NavigationItem { Label = "Stock",           IconGlyph = "\uE74C", PageKey = "Stock" },
        new NavigationItem { Label = "Transferts",      IconGlyph = "\uE895", PageKey = "StockTransfer" },
        new NavigationItem { Label = "Fidélité",        IconGlyph = "\uEB51", PageKey = "Loyalty" },
        new NavigationItem { Label = "Rapports",        IconGlyph = "\uE9F9", PageKey = "Reports" },
        new NavigationItem { Label = "Journal",         IconGlyph = "\uE8A5", PageKey = "SalesHistory" },
    };

    public ObservableCollection<NavigationItem> BottomNavigationItems { get; } = new()
    {
        new NavigationItem { Label = "Paramètres", IconGlyph = "\uE713", PageKey = "Settings" },
    };

    private readonly Dictionary<string, object> _pages = new();

    public MainViewModel()
    {
        PageTitle = "GECOM2025";
        NavigateToPage("Cash");
    }

    [RelayCommand]
    private void NavigateToPage(string pageKey)
    {
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
                "Loyalty" => new PlaceholderPage("Fidélité", "Le programme de fidélité sera implémenté ici."),
                _ => new PlaceholderPage("Page inconnue", "")
            };
        }

        CurrentPage = _pages[pageKey];

        // 🆕 Activate cached pages that implement IActivatable
        if (CurrentPage is System.Windows.FrameworkElement { DataContext: IActivatable activatable })
        {
            _ = activatable.ActivateAsync();
        }

        SelectedNavItem = NavigationItems.FirstOrDefault(n => n.PageKey == pageKey)
                       ?? BottomNavigationItems.FirstOrDefault(n => n.PageKey == pageKey);
    }

    partial void OnSelectedNavItemChanged(NavigationItem? value)
    {
        if (value != null)
        {
            NavigateToPage(value.PageKey);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FACTORY GÉNÉRIQUE
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Crée une page WPF et lui assigne son ViewModel résolu par DI.
    /// TPage doit avoir un constructeur sans paramètre.
    /// </summary>
    private static TPage CreatePage<TPage, TViewModel>()
        where TPage : System.Windows.FrameworkElement, new()
        where TViewModel : notnull
    {
        var vm = App.ServiceProvider.GetRequiredService<TViewModel>();
        var page = new TPage { DataContext = vm };
        return page;
    }

    /// <summary>
    /// Cas spécial : ClientsPage prend le VM dans son constructeur.
    /// </summary>
    private static ClientsPage CreateClientsPage()
    {
        var vm = App.ServiceProvider.GetRequiredService<ClientsViewModel>();
        return new ClientsPage(vm);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }
}