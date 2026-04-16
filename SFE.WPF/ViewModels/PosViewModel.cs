using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Messages;

namespace SFE.WPF.ViewModels;

public partial class PosViewModel : BaseViewModel,
    IRecipient<PriceModeChangedMessage>,
    IRecipient<DiscountBeforeTaxChangedMessage>,
    IActivatable
{
    private readonly InvoiceService _invoiceService;
    private readonly ProductService _productService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DispatcherTimer _clockTimer;
    private bool _isFirstActivation = true;

    // ══════ OPERATORS ══════
    public ObservableCollection<string> AvailableOperators { get; } = new();

    // ══════ PARAMÈTRE GLOBAL ══════
    private bool _discountBeforeTax = true;

    // ══════ CATALOGUE ══════
    public ObservableCollection<ProductCategory> Categories { get; } = new();
    public ObservableCollection<Product> DisplayProducts { get; } = new();
    public ObservableCollection<Product> SearchResults { get; } = new();

    [ObservableProperty] private ProductCategory? _selectedCategory;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _showSearchResults;
    [ObservableProperty] private bool _showFavoritesOnly = true;

    // ══════ PANIER ══════
    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();
    [ObservableProperty] private CartItemViewModel? _selectedCartItem;
    [ObservableProperty] private int _cartItemCount;

    // ══════ TOTAUX ══════
    [ObservableProperty] private decimal _totalHTBeforeDiscount;
    [ObservableProperty] private decimal _totalDiscount;
    [ObservableProperty] private decimal _totalHT;
    [ObservableProperty] private decimal _totalTVA;
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private decimal _totalSpecificTax;
    [ObservableProperty] private int _totalArticles;

    /// <summary>Total principal affiché : HT en mode HT, TTC en mode TTC.</summary>
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private string _grandTotalLabel = "TOTAL TTC";

    public string PriceModeDisplay => PriceMode == PriceMode.TTC ? "Prix TTC" : "Prix HT";
    public bool IsHtMode => PriceMode == PriceMode.HT;
    public bool HasAnyDiscount => TotalDiscount > 0;

    // ══════ REMISE POS ══════
    [ObservableProperty] private bool _showDiscountPanel;
    [ObservableProperty] private string _customDiscountValue = "";
    [ObservableProperty] private bool _isPercentDiscount = true;

    // ══════ PAIEMENT ══════
    [ObservableProperty] private PaymentType _selectedPaymentType = PaymentType.Especes;
    [ObservableProperty] private string _receivedAmount = "";
    [ObservableProperty] private decimal _changeAmount;
    [ObservableProperty] private bool _showChange;

    // ══════ CONFIG ══════
    [ObservableProperty] private PriceMode _priceMode = PriceMode.TTC;
    [ObservableProperty] private InvoiceType _invoiceType = InvoiceType.FV;
    [ObservableProperty] private string _operatorName = "Admin";
    [ObservableProperty] private string _isf = "";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentDate = "";

    // ══════ NORMALISATION ══════
    [ObservableProperty] private bool _isNormalized;
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private string _nim = "";
    [ObservableProperty] private string _counters = "";
    [ObservableProperty] private string _qrCodeContent = "";

    // ══════ STATUS ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private bool _showReceiptOverlay;

    // ══════ STATS DU JOUR ══════
    [ObservableProperty] private int _todaySalesCount;
    [ObservableProperty] private decimal _todaySalesTotal;

    // ══════ ENUMS ══════
    public PaymentType[] PaymentTypes { get; } = Enum.GetValues<PaymentType>();
    public InvoiceType[] InvoiceTypes { get; } =
        new[] { InvoiceType.FV, InvoiceType.FT, InvoiceType.EV, InvoiceType.ET };

    // ══════ MISE EN ATTENTE ══════
    public ObservableCollection<HeldTransactionViewModel> HeldTransactions { get; } = new();
    [ObservableProperty] private int _heldCount;
    [ObservableProperty] private bool _showHeldPanel;
    [ObservableProperty] private string _holdReason = "";
    [ObservableProperty] private bool _showHoldDialog;

    // ══════ POINT OF SALE ══════
    public ObservableCollection<PointOfSale> AvailablePointsOfSale { get; } = new();
    [ObservableProperty] private PointOfSale? _selectedPointOfSale;
    [ObservableProperty] private bool _hasMultiplePos;
    [ObservableProperty] private string _selectedPosInfo = "";

    // ══════════════════════════════════════════════════════════
    //  CONSTRUCTEUR
    // ══════════════════════════════════════════════════════════

    public PosViewModel(InvoiceService invoiceService, ProductService productService, IUnitOfWork unitOfWork)
    {
        _invoiceService = invoiceService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        PageTitle = "Caisse";

        WeakReferenceMessenger.Default.Register<PriceModeChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<DiscountBeforeTaxChangedMessage>(this);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        _ = InitializeAsync();
    }


    partial void OnSelectedPointOfSaleChanged(PointOfSale? value)
    {
        if (value == null)
        {
            SelectedPosInfo = "";
            return;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(value.City)) parts.Add(value.City);
        parts.Add(value.DeviceType.ToString());
        if (!string.IsNullOrWhiteSpace(value.EmcfNIM)) parts.Add($"NIM: {value.EmcfNIM}");
        if (value.ManagesStock) parts.Add("📦 Stock");

        SelectedPosInfo = string.Join(" · ", parts);
    }

    // ══════════════════════════════════════════════════════════
    //  MESSAGES
    // ══════════════════════════════════════════════════════════

    public void Receive(PriceModeChangedMessage message) => PriceMode = message.Value;

    public void Receive(DiscountBeforeTaxChangedMessage message)
    {
        _discountBeforeTax = message.Value;
        if (!IsNormalized) RecalculateAllItems();
    }

    partial void OnPriceModeChanged(PriceMode value)
    {
        if (IsNormalized) return;

        OnPropertyChanged(nameof(PriceModeDisplay));
        OnPropertyChanged(nameof(IsHtMode));
        RecalculateAllItems();
    }

    private void RecalculateAllItems()
    {
        foreach (var item in CartItems)
            item.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals();
    }

    // ══════════════════════════════════════════════════════════
    //  INITIALISATION
    // ══════════════════════════════════════════════════════════

    private void UpdateClock()
    {
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        CurrentDate = DateTime.Now.ToString("dddd dd MMMM yyyy",
            new System.Globalization.CultureInfo("fr-FR"));
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null)
            {
                PriceMode = company.DefaultPriceMode;
                var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);

                // ── Load all active Points of Sale ──
                AvailablePointsOfSale.Clear();
                if (companyWithPos?.PointsOfSale != null)
                {
                    var activePosList = companyWithPos.PointsOfSale
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.Code)
                        .ToList();

                    foreach (var pos in activePosList)
                        AvailablePointsOfSale.Add(pos);

                    HasMultiplePos = activePosList.Count > 1;
                    SelectedPointOfSale = activePosList.FirstOrDefault();
                }

                Isf = company.ISF;
            }

            // Charger le paramètre remise
            try
            {
                var appSettings = await _unitOfWork.AppSettings.GetCurrentAsync();
                _discountBeforeTax = appSettings?.DiscountBeforeTax ?? true;
            }
            catch { _discountBeforeTax = true; }

            var cats = await _productService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            await LoadDisplayProductsAsync();
            await RefreshDailyStatsAsync();
            await GenerateNewNumber();
            // ── Load operators ──
            await LoadOperatorsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    private async Task LoadOperatorsAsync()
    {
        try
        {
            var operators = await _unitOfWork.Invoices.GetDistinctOperatorNamesAsync();

            AvailableOperators.Clear();
            foreach (var name in operators.OrderBy(n => n))
                AvailableOperators.Add(name);

            // If current OperatorName not in list, add it
            if (!string.IsNullOrEmpty(OperatorName) && !AvailableOperators.Contains(OperatorName))
                AvailableOperators.Insert(0, OperatorName);

            // Default to first if not set
            if (string.IsNullOrEmpty(OperatorName) && AvailableOperators.Count > 0)
                OperatorName = AvailableOperators[0];
        }
        catch { }
    }

    private async Task GenerateNewNumber() =>
        InvoiceNumber = await _invoiceService.GenerateInvoiceNumberAsync(InvoiceType);

    private async Task RefreshDailyStatsAsync()
    {
        try
        {
            TodaySalesCount = await _unitOfWork.Invoices.GetTodayCountAsync();
            TodaySalesTotal = await _unitOfWork.Invoices.GetTodayTotalAsync();
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════
    //  CATALOGUE
    // ══════════════════════════════════════════════════════════

    private async Task LoadDisplayProductsAsync()
    {
        List<Product> products;

        if (ShowFavoritesOnly && SelectedCategory == null)
            products = await _unitOfWork.Products.GetFavoritesAsync();
        else if (SelectedCategory != null)
            products = await _unitOfWork.Products.GetByCategoryAsync(SelectedCategory.Id);
        else
            products = await _productService.GetAllActiveAsync();

        DisplayProducts.Clear();
        foreach (var p in products) DisplayProducts.Add(p);
    }

    partial void OnSelectedCategoryChanged(ProductCategory? value)
    {
        ShowFavoritesOnly = false;
        _ = LoadDisplayProductsAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = PerformSearchAsync(value);

    private async Task PerformSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            SearchResults.Clear();
            ShowSearchResults = false;
            return;
        }

        var results = await _productService.SearchAsync(query, 10);
        SearchResults.Clear();
        foreach (var r in results) SearchResults.Add(r);
        ShowSearchResults = SearchResults.Count > 0;
    }

    [RelayCommand]
    private void ShowFavorites()
    {
        SelectedCategory = null;
        ShowFavoritesOnly = true;
        _ = LoadDisplayProductsAsync();
    }

    [RelayCommand]
    private void ShowAllProducts()
    {
        SelectedCategory = null;
        ShowFavoritesOnly = false;
        _ = LoadDisplayProductsAsync();
    }

    [RelayCommand]
    private void SelectCategory(ProductCategory? category)
    {
        if (category == null) return;
        SelectedCategory = category;
    }

    // ══════════════════════════════════════════════════════════
    //  PANIER — Ajout / Quantité
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddToCart(Product? product)
    {
        if (product == null || IsNormalized) return;

        ClearStatus();
        ShowSearchResults = false;
        SearchText = "";

        var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += 1;
            existing.Recalculate(PriceMode, _discountBeforeTax);
        }
        else
        {
            // 🆕 Calcul dual price depuis le UnitPrice du produit
            var taxRate = TaxCalculator.GetDefaultRate(product.TaxGroup);
            var (ht, ttc) = TaxCalculator.EnsureDualPrices(
                product.UnitPrice, PriceMode, taxRate);

            var item = new CartItemViewModel
            {
                ProductId = product.Id,
                Code = product.Code,
                Name = product.Name,
                ItemType = product.ItemType,
                TaxGroup = product.TaxGroup,
                UnitPriceHT = ht,
                UnitPriceTTC = ttc,
                Unit = product.Unit,
                Quantity = 1,
                StockQuantity = product.StockQuantity,
                TrackStock = product.TrackStock,
                // Taxe spécifique héritée du produit
                HasSpecificTax = product.HasSpecificTax,
                SpecificTaxRate = product.SpecificTaxType == SpecificTaxType.Percentage
    ? product.SpecificTaxValue : 0m,
                TaxSpecificValue = product.HasSpecificTax
    ? product.SpecificTaxValue.ToString("G") : "",
            };
            item.Recalculate(PriceMode, _discountBeforeTax);
            CartItems.Add(item);
        }

        RecalculateTotals();
    }

    [RelayCommand]
    private void IncrementQuantity(CartItemViewModel? item)
    {
        if (item == null || IsNormalized) return;
        item.Quantity += 1;
        item.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals();
    }

    [RelayCommand]
    private void DecrementQuantity(CartItemViewModel? item)
    {
        if (item == null || IsNormalized) return;
        if (item.Quantity <= 1) { CartItems.Remove(item); SelectedCartItem = null; }
        else { item.Quantity -= 1; item.Recalculate(PriceMode, _discountBeforeTax); }
        RecalculateTotals();
    }

    [RelayCommand]
    private void RemoveFromCart(CartItemViewModel? item)
    {
        if (item == null || IsNormalized) return;
        CartItems.Remove(item);
        if (SelectedCartItem == item) SelectedCartItem = null;
        ShowDiscountPanel = false;
        RecalculateTotals();
    }

    [RelayCommand]
    private void ClearCart()
    {
        if (IsNormalized) return;
        CartItems.Clear();
        SelectedCartItem = null;
        ShowDiscountPanel = false;
        RecalculateTotals();
        ClearStatus();
    }

    // ══════════════════════════════════════════════════════════
    //  REMISE POS
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleDiscountPanel()
    {
        if (SelectedCartItem == null || IsNormalized)
        {
            ShowDiscountPanel = false;
            return;
        }
        ShowDiscountPanel = !ShowDiscountPanel;
        CustomDiscountValue = "";
        IsPercentDiscount = true;
    }

    /// <summary>Remise rapide : 5%, 10%, 15%, 20%.</summary>
    [RelayCommand]
    private void ApplyQuickDiscount(string percentStr)
    {
        if (SelectedCartItem == null || IsNormalized) return;
        if (!decimal.TryParse(percentStr, out var pct) || pct <= 0) return;

        SelectedCartItem.DiscountType = DiscountType.Percentage;
        SelectedCartItem.DiscountValue = pct;
        SelectedCartItem.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals();
        ShowDiscountPanel = false;
    }

    /// <summary>Remise personnalisée (% ou montant fixe).</summary>
    [RelayCommand]
    private void ApplyCustomDiscount()
    {
        if (SelectedCartItem == null || IsNormalized) return;
        if (!decimal.TryParse(CustomDiscountValue, out var val) || val <= 0)
        {
            StatusMessage = "Entrez une valeur de remise valide.";
            ShowError = true;
            return;
        }

        SelectedCartItem.DiscountType = IsPercentDiscount
            ? DiscountType.Percentage
            : DiscountType.FixedAmount;
        SelectedCartItem.DiscountValue = val;
        SelectedCartItem.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals();

        ShowDiscountPanel = false;
        CustomDiscountValue = "";
        ClearStatus();
    }

    /// <summary>Supprime la remise de l'article sélectionné.</summary>
    [RelayCommand]
    private void RemoveItemDiscount()
    {
        if (SelectedCartItem == null || IsNormalized) return;

        SelectedCartItem.DiscountType = DiscountType.None;
        SelectedCartItem.DiscountValue = 0;
        SelectedCartItem.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals();
        ShowDiscountPanel = false;
    }

    // ══════════════════════════════════════════════════════════
    //  TOTAUX
    // ══════════════════════════════════════════════════════════

    private void RecalculateTotals()
    {
        TotalHTBeforeDiscount = CartItems.Sum(c => c.AmountHTBeforeDiscount);
        TotalDiscount = CartItems.Sum(c => c.DiscountAmount);
        TotalHT = CartItems.Sum(c => c.AmountHT);
        TotalTVA = CartItems.Sum(c => c.AmountTVA);
        TotalTTC = CartItems.Sum(c => c.AmountTTC);
        TotalSpecificTax = CartItems.Sum(c => c.TaxSpecificAmount);

        // Grand Total : affiché prominemment, dépend du mode
        GrandTotal = PriceMode == PriceMode.TTC ? TotalTTC : TotalHT;
        GrandTotalLabel = PriceMode == PriceMode.TTC ? "TOTAL TTC" : "TOTAL HT";

        TotalArticles = CartItems.Sum(c => (int)c.Quantity);
        CartItemCount = CartItems.Count;

        OnPropertyChanged(nameof(HasAnyDiscount));
        UpdateChange();
    }

    // ══════════════════════════════════════════════════════════
    //  PAIEMENT (toujours basé sur TotalTTC)
    // ══════════════════════════════════════════════════════════

    partial void OnReceivedAmountChanged(string value) => UpdateChange();

    private void UpdateChange()
    {
        if (decimal.TryParse(ReceivedAmount, out var received) && received > TotalTTC)
        {
            ChangeAmount = received - TotalTTC;
            ShowChange = true;
        }
        else
        {
            ChangeAmount = 0;
            ShowChange = false;
        }
    }

    [RelayCommand]
    private void SetExactAmount() => ReceivedAmount = TotalTTC.ToString("F0");

    [RelayCommand]
    private void SetRoundedAmount(string amountStr)
    {
        if (decimal.TryParse(amountStr, out var amount))
            ReceivedAmount = amount.ToString("F0");
    }

    // ══════════════════════════════════════════════════════════
    //  NORMALISATION
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ProcessSale()
    {
        if (IsNormalized || CartItems.Count == 0) return;

        // ── Validate POS ──
        if (SelectedPointOfSale == null)
        {
            StatusMessage = "Veuillez sélectionner un point de vente.";
            ShowError = true;
            return;
        }

        ClearStatus();
        IsBusy = true;
        StatusMessage = "Normalisation en cours...";

        try
        {
            decimal paidAmount = TotalTTC;
            if (decimal.TryParse(ReceivedAmount, out var received) && received >= TotalTTC)
                paidAmount = received;
            else if (string.IsNullOrWhiteSpace(ReceivedAmount))
                paidAmount = TotalTTC;
            else
            {
                StatusMessage = "Le montant reçu est insuffisant.";
                ShowError = true;
                IsBusy = false;
                return;
            }

            var invoice = BuildInvoice(paidAmount);
            var result = await _invoiceService.NormalizeInvoiceAsync(invoice);

            if (result.Success)
            {
                IsNormalized = true;
                CodeDEFDGI = result.CodeDEFDGI;
                QrCodeContent = result.QRCodeContent;

                var saved = await _unitOfWork.Invoices.GetWithDetailsAsync(result.InvoiceId);
                if (saved != null)
                {
                    Nim = saved.NIM;
                    Counters = saved.Counters;
                }

                ShowReceiptOverlay = true;
                StatusMessage = $"✓ Vente normalisée — {result.CodeDEFDGI}";
                ShowSuccess = true;

                await RefreshDailyStatsAsync();
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Erreur inconnue.";
                ShowError = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task NewSale()
    {
        CartItems.Clear();
        SelectedCartItem = null;
        ShowDiscountPanel = false;
        ReceivedAmount = "";
        ChangeAmount = 0;
        ShowChange = false;
        IsNormalized = false;
        ShowReceiptOverlay = false;
        CodeDEFDGI = "";
        Nim = "";
        Counters = "";
        QrCodeContent = "";

        GrandTotal = 0;
        GrandTotalLabel = PriceMode == PriceMode.TTC ? "TOTAL TTC" : "TOTAL HT";

        RecalculateTotals();
        ClearStatus();

        // POS: keep selection across sales (operator stays on same POS)
        // Update ISF from selected POS in case it changed
        Isf = Isf;

        await GenerateNewNumber();
        ShowFavoritesOnly = true;
        SelectedCategory = null;
        await LoadDisplayProductsAsync();
    }
    private void ClearStatus()
    {
        StatusMessage = "";
        ShowSuccess = false;
        ShowError = false;
    }

    // ══════════════════════════════════════════════════════════
    //  BUILD INVOICE
    // ══════════════════════════════════════════════════════════

    private Invoice BuildInvoice(decimal paidAmount)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = InvoiceNumber,
            Type = InvoiceType,
            PriceMode = PriceMode,
            DiscountBeforeTax = _discountBeforeTax,
            ISF = Isf,
            ClientType = ClientType.PP,
            OperatorName = OperatorName,
            OperatorId = "01",
            TotalHTBeforeDiscount = TotalHTBeforeDiscount,
            TotalDiscount = TotalDiscount,
            TotalHT = TotalHT,
            TotalTVA = TotalTVA,
            TotalTTC = TotalTTC,
            TotalSpecificTax = TotalSpecificTax,
            PointOfSaleId = SelectedPointOfSale?.Id ?? 1
        };

        int lineNum = 1;
        foreach (var item in CartItems)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = lineNum++,
                Code = item.Code,
                Name = item.Name,
                ItemType = item.ItemType,
                TaxGroup = item.TaxGroup,
                TaxRate = item.TaxRate,
                UnitPriceHT = item.UnitPriceHT,
                UnitPriceTTC = item.UnitPriceTTC,
                Quantity = item.Quantity,
                Unit = item.Unit,
                DiscountType = item.DiscountType,
                DiscountValue = item.DiscountValue,
                DiscountAmount = item.DiscountAmount,
                AmountHTBeforeDiscount = item.AmountHTBeforeDiscount,
                HasSpecificTax = item.HasSpecificTax,
                SpecificTaxName = item.SpecificTaxName,
                SpecificTaxRate = item.SpecificTaxRate,
                TaxSpecificValue = item.TaxSpecificValue,
                TaxApplicationMode = item.TaxApplicationMode,
                TaxSpecificAmount = item.TaxSpecificAmount,
                AmountHT = item.AmountHT,
                AmountTVA = item.AmountTVA,
                AmountTTC = item.AmountTTC
            });
        }

        invoice.Payments.Add(new InvoicePayment
        {
            PaymentType = SelectedPaymentType,
            Amount = paidAmount,
            CurrencyCode = "CDF",
            CurrencyRate = 1m
        });

        return invoice;
    }



    partial void OnInvoiceTypeChanged(InvoiceType value) => _ = GenerateNewNumber();

    // ══════════════════════════════════════════════════════════
    //  HOLD / RECALL
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void RequestHold()
    {
        if (CartItems.Count == 0 || IsNormalized) return;
        if (CartItems.Count <= 2) { HoldReason = ""; HoldCurrentSale(); return; }
        HoldReason = "";
        ShowHoldDialog = true;
    }

    [RelayCommand]
    private void HoldCurrentSale()
    {
        if (CartItems.Count == 0 || IsNormalized) return;
        ShowHoldDialog = false;

        var held = new HeldTransactionViewModel
        {
            Label = CartItems.Count == 1
                ? CartItems[0].Name
                : $"{CartItems[0].Name} +{CartItems.Count - 1}",
            Reason = string.IsNullOrWhiteSpace(HoldReason) ? "" : HoldReason.Trim(),
            HeldAt = DateTime.Now,
            TotalTTC = TotalTTC,
            ItemCount = TotalArticles,
            OperatorName = OperatorName,
            InvoiceNumber = InvoiceNumber,
            InvoiceType = InvoiceType,
            PaymentType = SelectedPaymentType,
            ReceivedAmount = ReceivedAmount,
            PriceMode = PriceMode,
            DiscountBeforeTax = _discountBeforeTax
        };

        foreach (var item in CartItems)
        {
            held.Items.Add(new CartItemSnapshot
            {
                ProductId = item.ProductId,
                Code = item.Code,
                Name = item.Name,
                ItemType = item.ItemType,
                TaxGroup = item.TaxGroup,
                TaxRate = item.TaxRate,
                UnitPriceHT = item.UnitPriceHT,
                UnitPriceTTC = item.UnitPriceTTC,
                Unit = item.Unit,
                Quantity = item.Quantity,
                DiscountType = item.DiscountType,
                DiscountValue = item.DiscountValue,
                DiscountAmount = item.DiscountAmount,
                AmountHTBeforeDiscount = item.AmountHTBeforeDiscount,
                HasSpecificTax = item.HasSpecificTax,
                SpecificTaxName = item.SpecificTaxName,
                SpecificTaxRate = item.SpecificTaxRate,
                TaxSpecificValue = item.TaxSpecificValue,
                TaxApplicationMode = item.TaxApplicationMode,
                TaxSpecificAmount = item.TaxSpecificAmount,
                AmountHT = item.AmountHT,
                AmountTVA = item.AmountTVA,
                AmountTTC = item.AmountTTC,
                StockQuantity = item.StockQuantity,
                TrackStock = item.TrackStock
            });
        }

        HeldTransactions.Add(held);
        HeldCount = HeldTransactions.Count;

        CartItems.Clear();
        SelectedCartItem = null;
        ShowDiscountPanel = false;
        ReceivedAmount = "";
        ChangeAmount = 0;
        ShowChange = false;
        RecalculateTotals();

        _ = GenerateNewNumber();

        StatusMessage = $"⏸ Panier mis en attente ({held.Id}) — {held.ItemCount} article(s)";
        ShowSuccess = true;
        HoldReason = "";
    }

    [RelayCommand]
    private void RecallHeldSale(HeldTransactionViewModel? held)
    {
        if (held == null || IsNormalized) return;

        if (CartItems.Count > 0)
        {
            HoldReason = "(auto-hold avant rappel)";
            HoldCurrentSale();
        }

        CartItems.Clear();
        foreach (var snapshot in held.Items)
        {
            var item = new CartItemViewModel
            {
                ProductId = snapshot.ProductId,
                Code = snapshot.Code,
                Name = snapshot.Name,
                ItemType = snapshot.ItemType,
                TaxGroup = snapshot.TaxGroup,
                TaxRate = snapshot.TaxRate,
                UnitPriceHT = snapshot.UnitPriceHT,
                UnitPriceTTC = snapshot.UnitPriceTTC,
                Unit = snapshot.Unit,
                Quantity = snapshot.Quantity,
                DiscountType = snapshot.DiscountType,
                DiscountValue = snapshot.DiscountValue,
                DiscountAmount = snapshot.DiscountAmount,
                AmountHTBeforeDiscount = snapshot.AmountHTBeforeDiscount,
                HasSpecificTax = snapshot.HasSpecificTax,
                SpecificTaxName = snapshot.SpecificTaxName,
                SpecificTaxRate = snapshot.SpecificTaxRate,
                TaxSpecificValue = snapshot.TaxSpecificValue,
                TaxApplicationMode = snapshot.TaxApplicationMode,
                TaxSpecificAmount = snapshot.TaxSpecificAmount,
                AmountHT = snapshot.AmountHT,
                AmountTVA = snapshot.AmountTVA,
                AmountTTC = snapshot.AmountTTC,
                StockQuantity = snapshot.StockQuantity,
                TrackStock = snapshot.TrackStock
            };
            CartItems.Add(item);
        }

        InvoiceNumber = held.InvoiceNumber;
        InvoiceType = held.InvoiceType;
        SelectedPaymentType = held.PaymentType;
        ReceivedAmount = held.ReceivedAmount;

        // Si le mode ou le DiscountBeforeTax a changé, recalculer
        if (held.PriceMode != PriceMode || held.DiscountBeforeTax != _discountBeforeTax)
        {
            foreach (var item in CartItems)
                item.Recalculate(PriceMode, _discountBeforeTax);
        }

        RecalculateTotals();

        HeldTransactions.Remove(held);
        HeldCount = HeldTransactions.Count;
        ShowHeldPanel = HeldTransactions.Count > 0 && ShowHeldPanel;

        StatusMessage = $"▶ Panier rappelé ({held.Id}) — {held.ItemCount} article(s)";
        ShowSuccess = true;
    }

    [RelayCommand]
    private void DeleteHeldSale(HeldTransactionViewModel? held)
    {
        if (held == null) return;
        HeldTransactions.Remove(held);
        HeldCount = HeldTransactions.Count;
        if (HeldTransactions.Count == 0) ShowHeldPanel = false;
        StatusMessage = $"🗑 Panier {held.Id} supprimé";
        ShowSuccess = true;
    }

    [RelayCommand]
    private void ToggleHeldPanel()
    {
        if (HeldTransactions.Count == 0) { ShowHeldPanel = false; return; }
        ShowHeldPanel = !ShowHeldPanel;
    }

    [RelayCommand]
    private void CancelHold()
    {
        ShowHoldDialog = false;
        HoldReason = "";
    }

    /// <summary>Called each time the user navigates to the Caisse page.</summary>
    public async Task ActivateAsync()
    {
        // Skip first call — InitializeAsync already ran
        if (_isFirstActivation)
        {
            _isFirstActivation = false;
            return;
        }

        try
        {
            // ── Refresh POS list ──
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null)
            {
                var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);
                if (companyWithPos?.PointsOfSale != null)
                {
                    var activePosList = companyWithPos.PointsOfSale
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.Code)
                        .ToList();

                    var previousId = SelectedPointOfSale?.Id;

                    AvailablePointsOfSale.Clear();
                    foreach (var pos in activePosList)
                        AvailablePointsOfSale.Add(pos);

                    HasMultiplePos = activePosList.Count > 1;
                    SelectedPointOfSale = activePosList.FirstOrDefault(p => p.Id == previousId)
                                          ?? activePosList.FirstOrDefault();
                    Isf = Isf;
                }
            }

            // ── Refresh daily stats ──
            await RefreshDailyStatsAsync();

            // ── Refresh products (may have been edited) ──
            await LoadDisplayProductsAsync();

            // ── Refresh operators ──
            await LoadOperatorsAsync();
        }
        catch { }
    }
}