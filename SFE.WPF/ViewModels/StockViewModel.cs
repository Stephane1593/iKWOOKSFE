using System.Globalization;
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
    private readonly IAuthService _authService;

    // ── Concurrency guards (single-threaded UI, but async reentrancy is real) ──
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    // ── Debounce tokens ──
    private CancellationTokenSource? _productSearchCts;
    private CancellationTokenSource? _filterCts;

    // Reentrancy guard for OnSelectedPosChanged — prevents the command
    // and the setter both triggering LoadPosStockAsync.
    private bool _suppressPosChange;

    public StockViewModel(
        StockService stockService,
        IUnitOfWork unitOfWork,
        IAuthService authService)
    {
        _stockService = stockService;
        _unitOfWork = unitOfWork;
        _authService = authService;
        PageTitle = "📦 Gestion du stock";

        MovementOperator = _authService.CurrentUser?.FullName
                        ?? _authService.CurrentUser?.Username
                        ?? "Système";

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
        // Product CRUD can change names/categories/units of items we display —
        // so reload both the POS list (in case a new POS was added) and the stock.
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

    /// <summary>
    /// Fires when the combobox selection changes OR when we rebind the reference
    /// after a reload (by Id). In both cases we want to refresh the stock view.
    /// </summary>
    partial void OnSelectedPosChanged(PointOfSale? value)
    {
        if (_suppressPosChange) return;
        _ = LoadPosStockAsync();
    }

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
        => DebouncedApplyFilter();

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
    private string _movementOperator = "";

    [ObservableProperty]
    private string _productSearchText = "";

    partial void OnProductSearchTextChanged(string value)
        => DebouncedSearchProducts();

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
            var previousPosId = SelectedPos?.Id;

            PointsOfSale = new ObservableCollection<PointOfSale>(posList);

            // Rebind SelectedPos by Id (reference may have changed after reload).
            PointOfSale? target = null;
            if (previousPosId.HasValue)
                target = posList.FirstOrDefault(p => p.Id == previousPosId.Value);
            target ??= posList.FirstOrDefault();

            if (target != null)
            {
                // Only change the reference if it's actually different (avoids
                // spurious OnSelectedPosChanged → LoadPosStockAsync on every refresh).
                if (!ReferenceEquals(target, SelectedPos))
                {
                    SelectedPos = target;                // triggers OnSelectedPosChanged → LoadPosStockAsync
                }
                else
                {
                    // Same reference but underlying data may have changed; reload manually.
                    await LoadPosStockAsync();
                }
            }
            else
            {
                SelectedPos = null;
                StockItems.Clear();
                FilteredStockItems.Clear();
                TotalProducts = LowStockCount = OutOfStockCount = 0;
                TotalStockValue = 0;
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// <summary>
    /// Kept for XAML compatibility. Setting SelectedPos alone is enough
    /// (OnSelectedPosChanged triggers the reload), but if the view binds
    /// a command we keep this path working.
    /// </summary>
    [RelayCommand]
    private Task SelectPosAsync(PointOfSale pos)
    {
        if (pos == null || ReferenceEquals(pos, SelectedPos))
            return Task.CompletedTask;

        SelectedPos = pos;   // triggers OnSelectedPosChanged → LoadPosStockAsync
        return Task.CompletedTask;
    }

    private async Task LoadPosStockAsync()
    {
        if (SelectedPos == null) return;

        // Prevent overlapping reloads (event burst + manual refresh + selection change).
        if (!await _loadLock.WaitAsync(0))
            return;

        IsBusy = true;
        var posId = SelectedPos.Id;

        try
        {
            var stocks = await _stockService.GetPosStocksAsync(posId);

            // If user changed POS while we were loading, bail out — newer call will win.
            if (SelectedPos?.Id != posId) return;

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
                LastMovementAt = s.LastMovementAt.LocalDateTime
            }).ToList();

            StockItems = new ObservableCollection<PosStockItem>(items);

            TotalProducts = items.Count;
            LowStockCount = items.Count(i => i.IsLowStock && !i.IsOutOfStock);
            OutOfStockCount = items.Count(i => i.IsOutOfStock);
            TotalStockValue = items.Sum(i => i.Quantity * i.UnitPriceTtcCdf);

            ApplyFilter(ActiveFilter);
        }
        finally
        {
            IsBusy = false;
            _loadLock.Release();
        }
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

    private void DebouncedApplyFilter()
    {
        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) return;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => ApplyFilter(ActiveFilter));
            }
            catch (TaskCanceledException) { /* expected */ }
        });
    }

    private void ApplyFilter(string filter)
    {
        ActiveFilter = filter;
        IEnumerable<PosStockItem> filtered = StockItems;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            filtered = filtered.Where(i =>
                (i.ProductName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (i.ProductCode?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
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

        // Refresh operator in case session changed.
        MovementOperator = _authService.CurrentUser?.FullName
                        ?? _authService.CurrentUser?.Username
                        ?? "Système";

        IsMovementFormVisible = true;
        ClearStatus();
    }

    private void DebouncedSearchProducts()
    {
        _productSearchCts?.Cancel();
        _productSearchCts = new CancellationTokenSource();
        var token = _productSearchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    async () => await SearchProductsAsync(token));
            }
            catch (TaskCanceledException) { /* expected */ }
        });
    }

    private async Task SearchProductsAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ProductSearchText) || ProductSearchText.Length < 2)
        {
            IsProductSearchOpen = false;
            ProductSearchResults.Clear();
            return;
        }

        try
        {
            var results = await _unitOfWork.Products.SearchAsync(ProductSearchText, 15);
            if (token.IsCancellationRequested) return;

            ProductSearchResults = new ObservableCollection<Product>(results);
            IsProductSearchOpen = results.Count > 0;
        }
        catch
        {
            // Silent: search typos shouldn't break the form.
            IsProductSearchOpen = false;
        }
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

        // Cancel any pending debounced search so the setter below doesn't retrigger it.
        _productSearchCts?.Cancel();
        ProductSearchText = product.DisplayText;
    }

    /// <summary>
    /// Culture-tolerant decimal parse: accepts "12,5" and "12.5" regardless
    /// of the thread's current culture (FR-CD uses "," as separator).
    /// </summary>
    private static bool TryParseQuantity(string input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var s = input.Trim();

        // Try current culture first (respects user's locale).
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return true;

        // Fallback: normalize decimal separator and parse as Invariant.
        var normalized = s.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    [RelayCommand]
    private async Task SubmitMovementAsync()
    {
        if (SelectedPos == null || MovementProduct == null)
        {
            ShowErrorMessage("Sélectionnez un produit.");
            return;
        }

        if (!TryParseQuantity(MovementQuantity, out var qty))
        {
            ShowErrorMessage("Quantité invalide.");
            return;
        }

        // For non-adjustment movements, reject zero/negative.
        // For adjustment/inventory, zero is a legit value (set stock to 0).
        if (!IsAdjustmentMode && qty <= 0)
        {
            ShowErrorMessage("La quantité doit être strictement positive.");
            return;
        }
        if (IsAdjustmentMode && qty < 0)
        {
            ShowErrorMessage("La quantité ne peut pas être négative.");
            return;
        }

        IsBusy = true;
        try
        {
            StockOperationResult result = MovementType switch
            {
                StockMovementType.Entry => await _stockService.AddStockEntryAsync(
                    MovementProduct.ProductId, SelectedPos.Id, qty,
                    MovementOperator, MovementNotes, MovementReference),

                StockMovementType.Exit => await _stockService.AddStockExitAsync(
                    MovementProduct.ProductId, SelectedPos.Id, qty,
                    MovementOperator, MovementNotes, MovementReference),

                StockMovementType.Adjustment => await _stockService.AdjustStockAsync(
                    MovementProduct.ProductId, SelectedPos.Id, qty,
                    MovementOperator, MovementNotes),

                StockMovementType.PhysicalCount => await _stockService.SetPhysicalCountAsync(
                    MovementProduct.ProductId, SelectedPos.Id, qty,
                    MovementOperator, MovementNotes),

                StockMovementType.Initial => await _stockService.SetInitialStockAsync(
                    MovementProduct.ProductId, SelectedPos.Id, qty,
                    MovementOperator, MovementNotes),

                _ => StockOperationResult.Fail("Type non supporté.")
            };

            if (result.Success)
            {
                IsMovementFormVisible = false;

                // No explicit LoadPosStockAsync() here — the StockService now publishes
                // StockUpdated *after* commit, and OnStockOrProductChangedAsync will
                // trigger the reload. This removes the double-load we had before.

                _ = ShowSuccessAsync(
                    $"✅ {MovementFormTitle} — Nouveau stock: {result.NewQuantity.ToString("G", CultureInfo.CurrentCulture)}");
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
    private async Task ViewProductStockAsync(PosStockItem? item)
    {
        if (item == null) return;

        try
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
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur chargement détail: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CloseDetail() => ShowProductDetail = false;

    // ══════════════════════════════════════════════════════════
    //  COMMANDES — HISTORIQUE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ViewHistoryAsync(PosStockItem? item)
    {
        if (item == null || SelectedPos == null) return;

        try
        {
            var movements = await _stockService.GetMovementHistoryAsync(
                item.ProductId, SelectedPos.Id, 100);

            HistoryTitle = $"Historique — {item.ProductName} @ {SelectedPos.Code}";
            HistoryItems = new ObservableCollection<StockMovement>(movements);
            ShowHistory = true;
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur chargement historique: {ex.Message}");
        }
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
    public DateTimeOffset LastMovementAt { get; set; }

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