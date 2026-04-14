// File: SFE.WPF/ViewModels/StockTransferViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Collections.ObjectModel;

namespace SFE.WPF.ViewModels;

public partial class StockTransferViewModel : BaseViewModel
{
    private readonly StockService _stockService;
    private readonly IUnitOfWork _unitOfWork;
    private bool _suppressSearch;
    private bool _updatingPosFilters;

    private CancellationTokenSource? _searchCts;

    public StockTransferViewModel(StockService stockService, IUnitOfWork unitOfWork)
    {
        _stockService = stockService;
        _unitOfWork = unitOfWork;
        PageTitle = "🔄 Transferts inter-POS";

        // ── EVENT SUBSCRIPTION ──
        Subscribe(OnTransferChangedAsync,
            AppEvent.StockTransferCreated,
            AppEvent.StockTransferShipped,
            AppEvent.StockTransferReceived,
            AppEvent.StockTransferCancelled);
    }

    private async Task OnTransferChangedAsync()
    {
        await LoadTransfersAsync(null);
    }

    // ══════════════════════════════════════════════
    //  COLLECTIONS
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<StockTransfer> _transfers = new();

    [ObservableProperty]
    private ObservableCollection<PointOfSale> _pointsOfSale = new();

    [ObservableProperty]
    private ObservableCollection<PointOfSale> _availableFromPos = new();

    [ObservableProperty]
    private ObservableCollection<PointOfSale> _availableToPos = new();

    // ══════════════════════════════════════════════
    //  NOUVEAU TRANSFERT
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private bool _isNewTransferVisible;

    [ObservableProperty]
    private PointOfSale? _fromPos;

    [ObservableProperty]
    private PointOfSale? _toPos;

    [ObservableProperty]
    private string _transferNotes = "";

    [ObservableProperty]
    private string _transferOperator = "Admin";

    [ObservableProperty]
    private ObservableCollection<TransferLineItem> _transferLines = new();

    [ObservableProperty]
    private string _newLineProductSearch = "";

    [ObservableProperty]
    private Product? _newLineProduct;

    [ObservableProperty]
    private string _newLineQuantity = "1";

    [ObservableProperty]
    private ObservableCollection<Product> _productSearchResults = new();

    [ObservableProperty]
    private bool _isProductSearchOpen;

    partial void OnNewLineProductSearchChanged(string value)
    {
        if (_suppressSearch) return;

        if (NewLineProduct != null)
        {
            NewLineProduct = null;
        }

        _ = SearchProductsForLineAsync();
    }

    // ══════════════════════════════════════════════
    //  POS FILTERING — mutual exclusion
    // ══════════════════════════════════════════════

    partial void OnFromPosChanged(PointOfSale? value)
    {
        if (_updatingPosFilters) return;
        _updatingPosFilters = true;

        var currentTo = ToPos;
        AvailableToPos = new ObservableCollection<PointOfSale>(
            PointsOfSale.Where(p => value == null || p.Id != value.Id));

        if (currentTo != null && AvailableToPos.Any(p => p.Id == currentTo.Id))
            ToPos = AvailableToPos.First(p => p.Id == currentTo.Id);
        else
            ToPos = null;

        _updatingPosFilters = false;
    }

    partial void OnToPosChanged(PointOfSale? value)
    {
        if (_updatingPosFilters) return;
        _updatingPosFilters = true;

        var currentFrom = FromPos;
        AvailableFromPos = new ObservableCollection<PointOfSale>(
            PointsOfSale.Where(p => value == null || p.Id != value.Id));

        if (currentFrom != null && AvailableFromPos.Any(p => p.Id == currentFrom.Id))
            FromPos = AvailableFromPos.First(p => p.Id == currentFrom.Id);
        else
            FromPos = null;

        _updatingPosFilters = false;
    }

    private void ResetPosFilters()
    {
        _updatingPosFilters = true;
        AvailableFromPos = new ObservableCollection<PointOfSale>(PointsOfSale);
        AvailableToPos = new ObservableCollection<PointOfSale>(PointsOfSale);
        FromPos = null;
        ToPos = null;
        _updatingPosFilters = false;
    }

    // ══════════════════════════════════════════════
    //  DÉTAIL
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private bool _showDetail;

    [ObservableProperty]
    private StockTransfer? _selectedTransfer;

    // ══════════════════════════════════════════════
    //  STATS
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _inTransitCount;

    [ObservableProperty]
    private int _completedCount;

    // ══════════════════════════════════════════════
    //  COMPUTED
    // ══════════════════════════════════════════════

    public bool CanAddLine => NewLineProduct != null
        && decimal.TryParse(NewLineQuantity, out var q) && q > 0;

    partial void OnNewLineProductChanged(Product? value)
        => OnPropertyChanged(nameof(CanAddLine));

    partial void OnNewLineQuantityChanged(string value)
        => OnPropertyChanged(nameof(CanAddLine));

    public int TotalLineCount => TransferLines.Count;

    // ══════════════════════════════════════════════
    //  COMMANDES — CHARGEMENT
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var posList = await _unitOfWork.PointsOfSale.GetActiveAsync();
            PointsOfSale = new ObservableCollection<PointOfSale>(posList);
            ResetPosFilters();
            await LoadTransfersAsync(null);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void FilterByStatus(string? status)
    {
        TransferStatus? s = status switch
        {
            "Pending" => TransferStatus.Pending,
            "InTransit" => TransferStatus.InTransit,
            "Received" => TransferStatus.Received,
            _ => null
        };
        _ = LoadTransfersAsync(s);
    }

    private async Task LoadTransfersAsync(TransferStatus? status)
    {
        List<StockTransfer> transfers;

        if (status.HasValue)
        {
            transfers = await _unitOfWork.StockTransfers.GetByStatusAsync(status.Value);
        }
        else
        {
            var all = new List<StockTransfer>();
            foreach (var s in Enum.GetValues<TransferStatus>())
            {
                var batch = await _unitOfWork.StockTransfers.GetByStatusAsync(s);
                all.AddRange(batch);
            }
            transfers = all.OrderByDescending(t => t.CreatedAt).ToList();
        }

        Transfers = new ObservableCollection<StockTransfer>(transfers);
        PendingCount = transfers.Count(t => t.Status == TransferStatus.Pending);
        InTransitCount = transfers.Count(t => t.Status == TransferStatus.InTransit);
        CompletedCount = transfers.Count(t => t.Status == TransferStatus.Received
                                            || t.Status == TransferStatus.PartiallyReceived);
    }

    // ══════════════════════════════════════════════
    //  COMMANDES — NOUVEAU TRANSFERT
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void ShowNewTransfer()
    {
        ClearStatus();
        ResetPosFilters();
        IsNewTransferVisible = true;
    }

    [RelayCommand]
    private void CancelNewTransfer()
    {
        _searchCts?.Cancel();

        IsNewTransferVisible = false;
        ClearStatus();
        TransferLines.Clear();
        TransferNotes = "";
        ResetPosFilters();
        ClearProductSearch();
    }

    private async Task SearchProductsForLineAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(NewLineProductSearch) || NewLineProductSearch.Length < 2)
        {
            ProductSearchResults.Clear();
            IsProductSearchOpen = false;
            return;
        }

        try
        {
            await Task.Delay(300, token);

            if (token.IsCancellationRequested) return;

            var results = await _unitOfWork.Products.SearchAsync(NewLineProductSearch, 10);

            if (!token.IsCancellationRequested)
            {
                ProductSearchResults = new ObservableCollection<Product>(results);
                IsProductSearchOpen = results.Count > 0;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    }

    [RelayCommand]
    private void SelectProduct(Product product)
    {
        _searchCts?.Cancel();

        NewLineProduct = product;

        _suppressSearch = true;
        NewLineProductSearch = $"{product.Code} — {product.Name}";
        _suppressSearch = false;

        IsProductSearchOpen = false;
    }

    [RelayCommand]
    private void AddLine()
    {
        if (NewLineProduct == null) return;
        if (!decimal.TryParse(NewLineQuantity, out var qty) || qty <= 0) return;

        var existing = TransferLines.FirstOrDefault(l => l.ProductId == NewLineProduct.Id);
        if (existing != null)
        {
            existing.Quantity += qty;
            var idx = TransferLines.IndexOf(existing);
            TransferLines.RemoveAt(idx);
            TransferLines.Insert(idx, existing);
        }
        else
        {
            TransferLines.Add(new TransferLineItem
            {
                ProductId = NewLineProduct.Id,
                ProductCode = NewLineProduct.Code,
                ProductName = NewLineProduct.Name,
                Unit = NewLineProduct.Unit,
                Quantity = qty
            });
        }

        ClearProductSearch();
        OnPropertyChanged(nameof(TotalLineCount));
    }

    [RelayCommand]
    private void RemoveLine(TransferLineItem line)
    {
        TransferLines.Remove(line);
        OnPropertyChanged(nameof(TotalLineCount));
    }

    private void ClearProductSearch()
    {
        _searchCts?.Cancel();
        _suppressSearch = true;
        NewLineProduct = null;
        NewLineProductSearch = "";
        NewLineQuantity = "1";
        ProductSearchResults.Clear();
        IsProductSearchOpen = false;
        _suppressSearch = false;
    }

    [RelayCommand]
    private async Task CreateAndShipAsync()
    {
        _searchCts?.Cancel();
        IsProductSearchOpen = false;

        ClearStatus();

        if (FromPos == null || ToPos == null)
        {
            ShowErrorMessage("Sélectionnez les POS source et destination.");
            return;
        }
        if (FromPos.Id == ToPos.Id)
        {
            ShowErrorMessage("Les POS source et destination doivent être différents.");
            return;
        }
        if (TransferLines.Count == 0)
        {
            ShowErrorMessage("Ajoutez au moins un produit.");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Delay(100);

            var lines = TransferLines
                .Select(l => (l.ProductId, l.Quantity))
                .ToList();

            var transfer = await _stockService.CreateTransferAsync(
                FromPos.Id, ToPos.Id, TransferOperator, lines, TransferNotes);

            var shipResult = await _stockService.ShipTransferAsync(transfer.Id, TransferOperator);

            if (shipResult.Success)
            {
                CancelNewTransfer();
                // NOTE: LoadTransfersAsync will also be triggered by the
                // StockTransferShipped event, but the explicit call gives
                // immediate feedback before the event handler runs.
                await LoadTransfersAsync(null);
                _ = ShowSuccessAsync($"✅ Transfert {transfer.TransferNumber} créé et expédié !");
            }
            else
            {
                ShowErrorMessage(shipResult.ErrorMessage ?? "Erreur lors de l'expédition.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════
    //  COMMANDES — ACTIONS TRANSFERT
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task ReceiveTransferAsync(StockTransfer transfer)
    {
        IsBusy = true;
        try
        {
            var result = await _stockService.ReceiveTransferAsync(transfer.Id, TransferOperator);
            if (result.Success)
            {
                await LoadTransfersAsync(null);
                _ = ShowSuccessAsync(result.Message);
            }
            else ShowErrorMessage(result.ErrorMessage ?? "Erreur.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelTransferAsync(StockTransfer transfer)
    {
        IsBusy = true;
        try
        {
            var result = await _stockService.CancelTransferAsync(
                transfer.Id, TransferOperator, "Annulé par l'opérateur");
            if (result.Success)
            {
                await LoadTransfersAsync(null);
                _ = ShowSuccessAsync(result.Message);
            }
            else ShowErrorMessage(result.ErrorMessage ?? "Erreur.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ViewDetailAsync(StockTransfer transfer)
    {
        SelectedTransfer = await _unitOfWork.StockTransfers.GetWithLinesAsync(transfer.Id);
        ShowDetail = true;
    }

    [RelayCommand]
    private void CloseDetail() => ShowDetail = false;

    // ══════════════════════════════════════════════
    //  DISPOSE
    // ══════════════════════════════════════════════

    public override void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        base.Dispose();
    }
}

public class TransferLineItem
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "pce";
    public decimal Quantity { get; set; }
    public string QuantityDisplay => $"{Quantity:G} {Unit}";
}