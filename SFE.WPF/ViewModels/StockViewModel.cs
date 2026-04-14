// File: SFE.WPF/ViewModels/StockViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Collections.ObjectModel;

namespace SFE.WPF.ViewModels;

public partial class StockViewModel : BaseViewModel
{
    private readonly StockService _stockService;
    private readonly IUnitOfWork _unitOfWork;

    public StockViewModel(StockService stockService, IUnitOfWork unitOfWork)
    {
        _stockService = stockService;
        _unitOfWork = unitOfWork;
        PageTitle = "📦 Gestion du stock";

        MovementTypes = new ObservableCollection<StockMovementType>(
            Enum.GetValues<StockMovementType>()
                .Where(t => t != StockMovementType.Sale
                         && t != StockMovementType.CreditReturn
                         && t != StockMovementType.TransferIn
                         && t != StockMovementType.TransferOut));

        // ── EVENT SUBSCRIPTION ──
        Subscribe(OnStockOrProductChangedAsync,
            AppEvent.StockUpdated,
            AppEvent.ProductCreated,
            AppEvent.ProductUpdated,
            AppEvent.ProductDeleted);
    }

    private async Task OnStockOrProductChangedAsync()
    {
        await LoadPosStockAsync();
    }

    // ══════════════════════════════════════════════════════════
    //  PROPRIÉTÉS OBSERVABLES
    // ══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<PointOfSale> _pointsOfSale = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPos))]
    private PointOfSale? _selectedPos;

    public bool HasSelectedPos => SelectedPos != null;

    [ObservableProperty]
    private ObservableCollection<PosStockItem> _stockItems = new();

    [ObservableProperty]
    private ObservableCollection<PosStockItem> _filteredStockItems = new();

    [ObservableProperty]
    private int _totalProducts;

    [ObservableProperty]
    private int _lowStockCount;

    [ObservableProperty]
    private int _outOfStockCount;

    [ObservableProperty]
    private decimal _totalStockValue;

    [ObservableProperty]
    private string _searchText = "";

    partial void OnSearchTextChanged(string value)
        => ApplyFilter(ActiveFilter);

    [ObservableProperty]
    private string _activeFilter = "all";

    // ══════════════════════════════════════════════════════════
    //  FORMULAIRE MOUVEMENT
    // ══════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isMovementFormVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MovementFormTitle))]
    [NotifyPropertyChangedFor(nameof(IsAdjustmentMode))]
    [NotifyPropertyChangedFor(nameof(MovementQuantityLabel))]
    private StockMovementType _movementType;

    public ObservableCollection<StockMovementType> MovementTypes { get; }

    public string MovementFormTitle => MovementType switch
    {
        StockMovementType.Entry => "📥 Entrée de stock",
        StockMovementType.Exit => "📤 Sortie de stock",
        StockMovementType.Adjustment => "🔧 Ajustement",
        StockMovementType.PhysicalCount => "📋 Inventaire physique",
        StockMovementType.Initial => "🏁 Stock initial",
        _ => "Mouvement de stock"
    };

    public bool IsAdjustmentMode => MovementType == StockMovementType.Adjustment
                                  || MovementType == StockMovementType.PhysicalCount;

    public string MovementQuantityLabel => IsAdjustmentMode ? "Nouvelle quantité" : "Quantité";

    [ObservableProperty]
    private PosStockItem? _movementProduct;

    [ObservableProperty]
    private string _movementQuantity = "";

    [ObservableProperty]
    private string _movementNotes = "";

    [ObservableProperty]
    private string _movementReference = "";

    [ObservableProperty]
    private string _movementOperator = "Admin";

    [ObservableProperty]
    private string _productSearchText = "";

    partial void OnProductSearchTextChanged(string value)
        => _ = SearchProductsAsync();

    [ObservableProperty]
    private ObservableCollection<Product> _productSearchResults = new();

    [ObservableProperty]
    private bool _isProductSearchOpen;

    // ══════════════════════════════════════════════════════════
    //  DÉTAIL STOCK MULTI-POS
    // ══════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _showProductDetail;

    [ObservableProperty]
    private ObservableCollection<PosStockDetailItem> _productPosStocks = new();

    [ObservableProperty]
    private string _detailProductName = "";

    [ObservableProperty]
    private decimal _detailTotalStock;

    // ══════════════════════════════════════════════════════════
    //  HISTORIQUE
    // ══════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _showHistory;

    [ObservableProperty]
    private ObservableCollection<StockMovement> _historyItems = new();

    [ObservableProperty]
    private string _historyTitle = "";

    // ══════════════════════════════════════════════════════════
    //  COMMANDES — CHARGEMENT
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var posList = await _unitOfWork.PointsOfSale.GetActiveAsync();
            PointsOfSale = new ObservableCollection<PointOfSale>(posList);

            if (SelectedPos == null && posList.Count > 0)
                await SelectPosAsync(posList[0]);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SelectPosAsync(PointOfSale pos)
    {
        SelectedPos = pos;
        await LoadPosStockAsync();
    }

    private async Task LoadPosStockAsync()
    {
        if (SelectedPos == null) return;
        IsBusy = true;

        try
        {
            var stocks = await _stockService.GetPosStocksAsync(SelectedPos.Id);

            var items = stocks.Select(s => new PosStockItem
            {
                PosStockId = s.Id,
                ProductId = s.ProductId,
                ProductCode = s.Product?.Code ?? "",
                ProductName = s.Product?.Name ?? "",
                CategoryName = s.Product?.Category?.Name ?? "",
                CategoryIcon = s.Product?.Category?.Icon ?? "📦",
                Quantity = s.Quantity,
                MinStockLevel = s.EffectiveMinStock,
                MaxStockLevel = s.MaxStockLevel,
                Unit = s.Product?.Unit ?? "pce",
                UnitPriceTtcCdf = s.Product?.UnitPriceTtcCdf ?? 0,
                TrackStock = s.Product?.TrackStock ?? false,
                IsLowStock = s.IsLowStock,
                IsOutOfStock = s.IsOutOfStock,
                StatusColor = s.StockStatusColor,
                StatusText = s.StockStatusDisplay,
                LastMovementAt = s.LastMovementAt
            }).ToList();

            StockItems = new ObservableCollection<PosStockItem>(items);

            TotalProducts = items.Count;
            LowStockCount = items.Count(i => i.IsLowStock && !i.IsOutOfStock);
            OutOfStockCount = items.Count(i => i.IsOutOfStock);
            TotalStockValue = items.Sum(i => i.Quantity * i.UnitPriceTtcCdf);

            ApplyFilter(ActiveFilter);
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════════════════
    //  COMMANDES — FILTRES
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void FilterAll() => ApplyFilter("all");

    [RelayCommand]
    private void FilterLowStock() => ApplyFilter("low");

    [RelayCommand]
    private void FilterOutOfStock() => ApplyFilter("out");

    private void ApplyFilter(string filter)
    {
        ActiveFilter = filter;
        IEnumerable<PosStockItem> filtered = StockItems;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.ToLower();
            filtered = filtered.Where(i =>
                i.ProductName.ToLower().Contains(q) ||
                i.ProductCode.ToLower().Contains(q));
        }

        filtered = filter switch
        {
            "low" => filtered.Where(i => i.IsLowStock && !i.IsOutOfStock),
            "out" => filtered.Where(i => i.IsOutOfStock),
            _ => filtered
        };

        FilteredStockItems = new ObservableCollection<PosStockItem>(filtered.ToList());
    }

    // ══════════════════════════════════════════════════════════
    //  COMMANDES — FORMULAIRE MOUVEMENT
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowEntryForm() => ShowMovementForm(StockMovementType.Entry);

    [RelayCommand]
    private void ShowExitForm() => ShowMovementForm(StockMovementType.Exit);

    [RelayCommand]
    private void ShowAdjustmentForm() => ShowMovementForm(StockMovementType.Adjustment);

    [RelayCommand]
    private void ShowInventoryForm() => ShowMovementForm(StockMovementType.PhysicalCount);

    [RelayCommand]
    private void CancelMovement() => IsMovementFormVisible = false;

    private void ShowMovementForm(StockMovementType type)
    {
        MovementType = type;
        MovementQuantity = "";
        MovementNotes = "";
        MovementReference = "";
        ProductSearchText = "";
        MovementProduct = null;
        IsMovementFormVisible = true;
        ClearStatus();
    }

    private async Task SearchProductsAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductSearchText) || ProductSearchText.Length < 2)
        {
            IsProductSearchOpen = false;
            return;
        }

        var results = await _unitOfWork.Products.SearchAsync(ProductSearchText, 15);
        ProductSearchResults = new ObservableCollection<Product>(results);
        IsProductSearchOpen = results.Count > 0;
    }

    [RelayCommand]
    private void SelectProductForMovement(Product? product)
    {
        if (product == null || SelectedPos == null) return;

        var existing = StockItems.FirstOrDefault(s => s.ProductId == product.Id);
        MovementProduct = existing ?? new PosStockItem
        {
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            Unit = product.Unit,
            Quantity = 0,
            TrackStock = product.TrackStock
        };

        IsProductSearchOpen = false;
        ProductSearchText = product.DisplayText;
    }

    [RelayCommand]
    private async Task SubmitMovementAsync()
    {
        if (SelectedPos == null || MovementProduct == null)
        {
            ShowErrorMessage("Sélectionnez un produit.");
            return;
        }

        if (!decimal.TryParse(MovementQuantity, out var qty) || qty <= 0)
        {
            ShowErrorMessage("Quantité invalide.");
            return;
        }

        IsBusy = true;
        try
        {
            StockOperationResult result;

            switch (MovementType)
            {
                case StockMovementType.Entry:
                    result = await _stockService.AddStockEntryAsync(
                        MovementProduct.ProductId, SelectedPos.Id, qty,
                        MovementOperator, MovementNotes, MovementReference);
                    break;

                case StockMovementType.Exit:
                    result = await _stockService.AddStockExitAsync(
                        MovementProduct.ProductId, SelectedPos.Id, qty,
                        MovementOperator, MovementNotes, MovementReference);
                    break;

                case StockMovementType.Adjustment:
                    result = await _stockService.AdjustStockAsync(
                        MovementProduct.ProductId, SelectedPos.Id, qty,
                        MovementOperator, MovementNotes);
                    break;

                case StockMovementType.PhysicalCount:
                    result = await _stockService.SetPhysicalCountAsync(
                        MovementProduct.ProductId, SelectedPos.Id, qty,
                        MovementOperator, MovementNotes);
                    break;

                case StockMovementType.Initial:
                    result = await _stockService.SetInitialStockAsync(
                        MovementProduct.ProductId, SelectedPos.Id, qty,
                        MovementOperator, MovementNotes);
                    break;

                default:
                    result = StockOperationResult.Fail("Type non supporté.");
                    break;
            }

            if (result.Success)
            {
                IsMovementFormVisible = false;
                // NOTE: LoadPosStockAsync will also be triggered by the StockUpdated event,
                // but the explicit call here gives immediate feedback.
                await LoadPosStockAsync();
                _ = ShowSuccessAsync($"✅ {MovementFormTitle} — Nouveau stock: {result.NewQuantity:G}");
            }
            else
            {
                ShowErrorMessage(result.ErrorMessage ?? "Erreur inconnue.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════════════════
    //  COMMANDES — DÉTAIL PRODUIT (stock multi-POS)
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ViewProductStockAsync(PosStockItem item)
    {
        var stocks = await _stockService.GetProductStocksAsync(item.ProductId);

        DetailProductName = item.ProductName;
        DetailTotalStock = stocks.Sum(s => s.Quantity);
        ProductPosStocks = new ObservableCollection<PosStockDetailItem>(
            stocks.Select(s => new PosStockDetailItem
            {
                PosCode = s.PointOfSale?.Code ?? "",
                PosName = s.PointOfSale?.Name ?? "",
                Quantity = s.Quantity,
                MinStock = s.EffectiveMinStock,
                StatusColor = s.StockStatusColor,
                StatusText = s.StockStatusDisplay,
                IsCurrentPos = s.PointOfSaleId == SelectedPos?.Id
            }));

        ShowProductDetail = true;
    }

    [RelayCommand]
    private void CloseDetail() => ShowProductDetail = false;

    // ══════════════════════════════════════════════════════════
    //  COMMANDES — HISTORIQUE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ViewHistoryAsync(PosStockItem item)
    {
        if (SelectedPos == null) return;

        var movements = await _stockService.GetMovementHistoryAsync(
            item.ProductId, SelectedPos.Id, 100);

        HistoryTitle = $"Historique — {item.ProductName} @ {SelectedPos.Code}";
        HistoryItems = new ObservableCollection<StockMovement>(movements);
        ShowHistory = true;
    }

    [RelayCommand]
    private void CloseHistory() => ShowHistory = false;
}

// ══════════════════════════════════════════════════════════
//  DTOs d'affichage (unchanged)
// ══════════════════════════════════════════════════════════

public class PosStockItem
{
    public int PosStockId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string CategoryIcon { get; set; } = "📦";
    public decimal Quantity { get; set; }
    public decimal MinStockLevel { get; set; }
    public decimal? MaxStockLevel { get; set; }
    public string Unit { get; set; } = "pce";
    public decimal UnitPriceTtcCdf { get; set; }
    public bool TrackStock { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
    public string StatusColor { get; set; } = "#10B981";
    public string StatusText { get; set; } = "OK";
    public DateTime LastMovementAt { get; set; }

    public string QuantityDisplay => $"{Quantity:G} {Unit}";
    public string ValueDisplay => $"{(Quantity * UnitPriceTtcCdf):N0} CDF";
    public string LastMoveDisplay => LastMovementAt.ToString("dd/MM HH:mm");
}

public class PosStockDetailItem
{
    public string PosCode { get; set; } = "";
    public string PosName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal MinStock { get; set; }
    public string StatusColor { get; set; } = "#10B981";
    public string StatusText { get; set; } = "OK";
    public bool IsCurrentPos { get; set; }
}