using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Management;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Helpers;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Messages;
using SFE.WPF.Services;
using SFE.WPF.Helpers;

namespace SFE.WPF.ViewModels;

public partial class PosViewModel : BaseViewModel,
    IRecipient<PriceModeChangedMessage>,
    IRecipient<DiscountBeforeTaxChangedMessage>,
    IActivatable
{
    private readonly InvoiceService _invoiceService;
    private readonly ProductService _productService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ClientService _clientService;
    private readonly IFiscalDeviceService _fiscalDevice;
    private readonly CustomerDisplayService _customerDisplay;
    private readonly IAuthService _auth;                          // 🆕
    private readonly DispatcherTimer _clockTimer;
    private bool _isFirstActivation = true;

    private Company? _currentCompany;
    private PointOfSale? _currentPos;
    private decimal _currentExchangeRate;

    [ObservableProperty] private string _thermalPrinterName = "";
    [ObservableProperty] private bool _hasThermalPrinter;
    [ObservableProperty] private bool _autoPrintReceipt = true;

    public ObservableCollection<string> AvailableOperators { get; } = new();
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
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private string _grandTotalLabel = "TOTAL TTC";

    public string PriceModeDisplay => PriceMode == PriceMode.TTC ? "Prix TTC" : "Prix HT";
    public bool IsHtMode => PriceMode == PriceMode.HT;
    public bool HasAnyDiscount => TotalDiscount > 0;
    public bool HasAnySpecificTax => TotalSpecificTax > 0;

    // ══════ REMISE POS ══════
    [ObservableProperty] private bool _showDiscountPanel;
    [ObservableProperty] private string _customDiscountValue = "";
    [ObservableProperty] private bool _isPercentDiscount = true;

    // ══════ PAIEMENT ══════
    [ObservableProperty] private PaymentType _selectedPaymentType = PaymentType.Especes;
    [ObservableProperty] private string _receivedAmount = "";
    [ObservableProperty] private decimal _changeAmount;
    [ObservableProperty] private bool _showChange;
    public ObservableCollection<PaymentDisplayItem> PaymentItems { get; } = new();
    [ObservableProperty] private decimal _totalPaid;
    [ObservableProperty] private decimal _remaining;
    [ObservableProperty] private string _paymentAmount = "";
    [ObservableProperty] private bool _showSplitPayment;

    // ══════ CONFIG ══════
    [ObservableProperty] private PriceMode _priceMode = PriceMode.TTC;
    [ObservableProperty] private InvoiceType _invoiceType = InvoiceType.FV;
    [ObservableProperty] private string _operatorName = "Opérateur";
    [ObservableProperty] private string _isf = "";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentDate = "";

    // ══════ CLIENT ══════
    [ObservableProperty] private ClientType _selectedClientType = ClientType.PP;
    [ObservableProperty] private string _clientNIF = "";
    [ObservableProperty] private string _clientName = "";
    [ObservableProperty] private string _clientAddress = "";
    [ObservableProperty] private string _clientPhone = "";
    [ObservableProperty] private string _clientEmail = "";
    [ObservableProperty] private string _clientRCCM = "";
    [ObservableProperty] private bool _showClientPanel;
    [ObservableProperty] private int? _selectedClientId;
    [ObservableProperty] private string _clientSearchText = "";
    [ObservableProperty] private bool _isClientSearchOpen;
    public ObservableCollection<Client> ClientSearchResults { get; } = new();
    public bool IsClientNifRequired => SelectedClientType is ClientType.PM or ClientType.PC or ClientType.PL;
    public bool IsClientNameRequired => SelectedClientType != ClientType.PP;
    public string ClientTypeMention => ClientService.GetTypeMention(SelectedClientType);
    public bool HasClientSelected => SelectedClientId.HasValue || !string.IsNullOrWhiteSpace(ClientName);
    public ClientType[] ClientTypes { get; } = Enum.GetValues<ClientType>();

    // ══════ AVOIR ══════
    [ObservableProperty] private bool _isCreditNote;
    [ObservableProperty] private CreditNoteNature _selectedCreditNoteNature = CreditNoteNature.COR;
    [ObservableProperty] private string _originalReference = "";
    [ObservableProperty] private bool _isOriginalLoaded;
    [ObservableProperty] private string _originalInvoiceSummary = "";
    [ObservableProperty] private bool _isLoadingOriginal;
    [ObservableProperty] private bool _showCreditNotePanel;
    private Invoice? _loadedOriginalInvoice;
    private Dictionary<string, decimal> _cumulativeRefunded = new();
    public ObservableCollection<CreditNoteLineSelection> CreditNoteSelections { get; } = new();
    public bool IsRRR => IsCreditNote && SelectedCreditNoteNature == CreditNoteNature.RRR;
    public bool RequiresOriginalLookup => IsCreditNote && !IsRRR;
    public CreditNoteNature[] CreditNoteNatures { get; } = Enum.GetValues<CreditNoteNature>();

    // ══════ ACOMPTE ══════
    [ObservableProperty] private bool _isAdvanceInvoice;
    [ObservableProperty] private string _advanceGroupId = "";
    [ObservableProperty] private decimal _advancesTotalPaid;
    [ObservableProperty] private bool _showAdvancePanel;
    public ObservableCollection<AdvanceInvoiceSummary> PreviousAdvances { get; } = new();

    // ══════ DEVISE ══════
    [ObservableProperty] private Currency _selectedCurrency = Currency.CDF;
    [ObservableProperty] private decimal _exchangeRate = 2800m;
    [ObservableProperty] private decimal _totalInAlternateCurrency;
    [ObservableProperty] private string _alternateCurrencyLabel = "USD";

    // ══════ COMMENTAIRES ══════
    [ObservableProperty] private string _commentA = "";
    [ObservableProperty] private string _commentB = "";
    [ObservableProperty] private string _commentC = "";
    [ObservableProperty] private string _commentD = "";
    [ObservableProperty] private string _commentE = "";
    [ObservableProperty] private string _commentF = "";
    [ObservableProperty] private string _commentG = "";
    [ObservableProperty] private string _commentH = "";
    [ObservableProperty] private bool _showCommentPanel;

    public bool IsCommentARequired =>
        SelectedClientType == ClientType.AO || CartItems.Any(l => l.TaxGroup == TaxGroup.D);
    public string CommentALabel => SelectedClientType == ClientType.AO
        ? "Réf. certificat d'exonération *"
        : CartItems.Any(l => l.TaxGroup == TaxGroup.D)
            ? "Réf. document de dérogation DGI *" : "Commentaire (optionnel)";

    public ObservableCollection<TaxGroupSummary> TaxGroupSummaries { get; } = new();

    // ══════ NORMALISATION ══════
    [ObservableProperty] private bool _isNormalized;
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private string _nim = "";
    [ObservableProperty] private string _counters = "";
    [ObservableProperty] private string _qrCodeContent = "";
    [ObservableProperty] private string _deviceDateTime = "";

    // ══════ STATUS ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private bool _showReceiptOverlay;

    // ══════ STATS ══════
    [ObservableProperty] private int _todaySalesCount;
    [ObservableProperty] private decimal _todaySalesTotal;

    // ══════ FACTURES EN ATTENTE ══════
    [ObservableProperty] private int _pendingInvoiceCount;
    [ObservableProperty] private string _cancelUid = "";
    [ObservableProperty] private bool _isCheckingPending;
    [ObservableProperty] private string _pendingStatusMessage = "";
    [ObservableProperty] private bool _showPendingSuccess;
    [ObservableProperty] private bool _showPendingError;
    [ObservableProperty] private bool _showPendingPanel;
    public ObservableCollection<PendingInvoiceItem> PendingInvoices { get; } = new();
    public bool HasPendingInvoices => PendingInvoiceCount > 0;

    // ══════ ENUMS ══════
    public PaymentType[] PaymentTypes { get; } = Enum.GetValues<PaymentType>();
    public InvoiceType[] InvoiceTypes { get; } = Enum.GetValues<InvoiceType>();

    // ══════ HELD ══════
    public ObservableCollection<HeldTransactionViewModel> HeldTransactions { get; } = new();
    [ObservableProperty] private int _heldCount;
    [ObservableProperty] private bool _showHeldPanel;
    [ObservableProperty] private string _holdReason = "";
    [ObservableProperty] private bool _showHoldDialog;

    // ══════ POS ══════
    public ObservableCollection<PointOfSale> AvailablePointsOfSale { get; } = new();
    [ObservableProperty] private PointOfSale? _selectedPointOfSale;
    [ObservableProperty] private bool _hasMultiplePos;
    [ObservableProperty] private string _selectedPosInfo = "";

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);

    // ══════════════════════════════════════════════════════════
    //  CONSTRUCTEUR — 🆕 IAuthService injected
    // ══════════════════════════════════════════════════════════

    public PosViewModel(
        InvoiceService invoiceService, ProductService productService,
        IUnitOfWork unitOfWork, CustomerDisplayService customerDisplay,
        ClientService clientService, IFiscalDeviceService fiscalDevice,
        IAuthService auth)                                                // 🆕
    {
        _invoiceService = invoiceService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _customerDisplay = customerDisplay;
        _clientService = clientService;
        _fiscalDevice = fiscalDevice;
        _auth = auth;                                                     // 🆕
        PageTitle = "Caisse";

        // 🆕 Set operator name from logged-in user
        OperatorName = _auth.CurrentUser?.FullName ?? "Opérateur";

        WeakReferenceMessenger.Default.Register<PriceModeChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<DiscountBeforeTaxChangedMessage>(this);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        _ = InitializeAsync();
    }

    // ══════════════════════════════════════════════════════════
    //  PARTIAL CHANGE HANDLERS
    // ══════════════════════════════════════════════════════════

    partial void OnSelectedPointOfSaleChanged(PointOfSale? value)
    {
        _currentPos = value;
        if (value == null) { SelectedPosInfo = ""; return; }
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(value.City)) parts.Add(value.City);
        parts.Add(value.DeviceType.ToString());
        if (!string.IsNullOrWhiteSpace(value.EmcfNIM)) parts.Add($"NIM: {value.EmcfNIM}");
        if (value.ManagesStock) parts.Add("📦 Stock");
        SelectedPosInfo = string.Join(" · ", parts);
        LoadPrinterSettings();
    }

    partial void OnPriceModeChanged(PriceMode value)
    {
        if (IsNormalized) return;
        OnPropertyChanged(nameof(PriceModeDisplay));
        OnPropertyChanged(nameof(IsHtMode));
        RecalculateAllItems();
    }

    partial void OnInvoiceTypeChanged(InvoiceType value)
    {
        IsCreditNote = value == InvoiceType.FA || value == InvoiceType.EA;
        IsAdvanceInvoice = value == InvoiceType.FT || value == InvoiceType.ET;
        if (!IsCreditNote)
        {
            IsOriginalLoaded = false; OriginalInvoiceSummary = "";
            _loadedOriginalInvoice = null; CreditNoteSelections.Clear(); ShowCreditNotePanel = false;
        }
        OnPropertyChanged(nameof(IsRRR));
        OnPropertyChanged(nameof(RequiresOriginalLookup));
        _ = GenerateNewNumber();
    }

    partial void OnSelectedCreditNoteNatureChanged(CreditNoteNature value)
    {
        OnPropertyChanged(nameof(IsRRR));
        OnPropertyChanged(nameof(RequiresOriginalLookup));
        if (IsRRR) { OriginalReference = "RRR"; IsOriginalLoaded = false; _loadedOriginalInvoice = null; CreditNoteSelections.Clear(); }
        else if (OriginalReference == "RRR") OriginalReference = "";
    }

    partial void OnSelectedClientTypeChanged(ClientType value)
    {
        OnPropertyChanged(nameof(IsClientNifRequired));
        OnPropertyChanged(nameof(IsClientNameRequired));
        OnPropertyChanged(nameof(ClientTypeMention));
        OnPropertyChanged(nameof(IsCommentARequired));
        OnPropertyChanged(nameof(CommentALabel));
    }

    partial void OnReceivedAmountChanged(string value) => UpdateChange();
    partial void OnSelectedCurrencyChanged(Currency value) => UpdateAlternateCurrency();
    partial void OnExchangeRateChanged(decimal value) => UpdateAlternateCurrency();
    partial void OnClientSearchTextChanged(string value) => _ = SearchClientsAsync(value);

    // ══════════════════════════════════════════════════════════
    //  MESSAGES
    // ══════════════════════════════════════════════════════════

    public void Receive(PriceModeChangedMessage message) => PriceMode = message.Value;
    public void Receive(DiscountBeforeTaxChangedMessage message)
    {
        _discountBeforeTax = message.Value;
        if (!IsNormalized) RecalculateAllItems();
    }

    private void RecalculateAllItems()
    {
        foreach (var item in CartItems) item.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals();
    }

    // ══════════════════════════════════════════════════════════
    //  INITIALISATION — 🆕 POS selection + product fallback
    // ══════════════════════════════════════════════════════════

    private void UpdateClock()
    {
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        CurrentDate = DateTime.Now.ToString("dddd dd MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            // ── 1. Company & POS ──
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null)
            {
                PriceMode = company.DefaultPriceMode;
                _currentCompany = company;
                Isf = company.ISF ?? "";

                var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);
                AvailablePointsOfSale.Clear();
                if (companyWithPos?.PointsOfSale != null)
                {
                    var activePosList = companyWithPos.PointsOfSale
                        .Where(p => p.IsActive).OrderBy(p => p.Code).ToList();
                    foreach (var pos in activePosList) AvailablePointsOfSale.Add(pos);
                    HasMultiplePos = activePosList.Count > 1;

                    // 🆕 Select user-assigned POS → first available
                    SelectedPointOfSale = PosSelectionHelper.SelectBestPos(
                        activePosList, _auth.CurrentUser?.PointOfSaleId);
                }
            }

            // ── 2. App settings (independent) ──
            try
            {
                var appSettings = await _unitOfWork.AppSettings.GetCurrentAsync();
                if (appSettings != null)
                {
                    _discountBeforeTax = appSettings.DiscountBeforeTax;
                    SelectedCurrency = appSettings.DefaultCurrency;
                    ExchangeRate = appSettings.CurrentExchangeRate;
                    _currentExchangeRate = appSettings.CurrentExchangeRate;
                }
            }
            catch { _discountBeforeTax = true; }

            // ── 3. Catalogue — 🆕 Isolated try-catch ──
            try
            {
                var cats = await _productService.GetCategoriesAsync();
                Categories.Clear();
                foreach (var c in cats) Categories.Add(c);
                await LoadDisplayProductsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur chargement produits : {ex.Message}";
                ShowError = true;
            }

            // ── 4. Non-critical tasks ──
            try { await RefreshDailyStatsAsync(); } catch { }
            try { await GenerateNewNumber(); } catch { }
            try { await LoadOperatorsAsync(); } catch { }
            try { DetectThermalPrinter(); } catch { }
            try
            {
                if (_currentCompany != null)
                    _customerDisplay.Open(_currentCompany.Name);
            }
            catch { }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    private void LoadPrinterSettings()
    {
        if (SelectedPointOfSale == null) { DetectThermalPrinter(); return; }
        var pos = SelectedPointOfSale;

        if (!string.IsNullOrWhiteSpace(pos.ThermalPrinterName))
        { ThermalPrinterName = pos.ThermalPrinterName; HasThermalPrinter = true; }
        else DetectThermalPrinter();

        AutoPrintReceipt = pos.AutoPrintReceipt;

        try
        {
            if (pos.EnableCustomerDisplay && _currentCompany != null)
                _customerDisplay.Open(_currentCompany.Name);
        }
        catch { }
    }

    private void DetectThermalPrinter()
    {
        var keywords = new[] { "OPTIMA", "POS", "Thermal", "Receipt", "58", "80" };
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Printer");
            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "";
                foreach (var kw in keywords)
                    if (name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    { ThermalPrinterName = name; HasThermalPrinter = true; return; }
            }
        }
        catch { }
        var buffer = new StringBuilder(256); int size = buffer.Capacity;
        if (GetDefaultPrinter(buffer, ref size) && buffer.Length > 0)
        { ThermalPrinterName = buffer.ToString(); HasThermalPrinter = true; }
        else { ThermalPrinterName = ""; HasThermalPrinter = false; }
    }

    private async Task LoadOperatorsAsync()
    {
        try
        {
            var operators = await _unitOfWork.Invoices.GetDistinctOperatorNamesAsync();
            AvailableOperators.Clear();
            foreach (var name in operators.OrderBy(n => n)) AvailableOperators.Add(name);
            if (!string.IsNullOrEmpty(OperatorName) && !AvailableOperators.Contains(OperatorName))
                AvailableOperators.Insert(0, OperatorName);
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
    //  CATALOGUE — 🆕 Fallback when no favorites
    // ══════════════════════════════════════════════════════════

    private async Task LoadDisplayProductsAsync()
    {
        List<Product> products;

        if (ShowFavoritesOnly && SelectedCategory == null)
        {
            products = await _unitOfWork.Products.GetFavoritesAsync();

            // 🆕 Fallback: if no favorites, show all active products
            if (products.Count == 0)
            {
                ShowFavoritesOnly = false;
                products = await _productService.GetAllActiveAsync();
            }
        }
        else if (SelectedCategory != null)
        {
            products = await _unitOfWork.Products.GetByCategoryAsync(SelectedCategory.Id);
        }
        else
        {
            products = await _productService.GetAllActiveAsync();
        }

        DisplayProducts.Clear();
        foreach (var p in products) DisplayProducts.Add(p);
    }

    partial void OnSelectedCategoryChanged(ProductCategory? value)
    { ShowFavoritesOnly = false; _ = LoadDisplayProductsAsync(); }

    partial void OnSearchTextChanged(string value) => _ = PerformSearchAsync(value);

    private async Task PerformSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        { SearchResults.Clear(); ShowSearchResults = false; return; }
        var results = await _productService.SearchAsync(query, 10);
        SearchResults.Clear();
        foreach (var r in results) SearchResults.Add(r);
        ShowSearchResults = SearchResults.Count > 0;
    }

    [RelayCommand]
    private void ShowFavorites()
    { SelectedCategory = null; ShowFavoritesOnly = true; _ = LoadDisplayProductsAsync(); }

    [RelayCommand]
    private void ShowAllProducts()
    { SelectedCategory = null; ShowFavoritesOnly = false; _ = LoadDisplayProductsAsync(); }

    [RelayCommand]
    private void SelectCategory(ProductCategory? category)
    { if (category != null) SelectedCategory = category; }

    // ══════════════════════════════════════════════════════════
    //  PANIER
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddToCart(Product? product)
    {
        if (product == null || IsNormalized) return;
        ClearStatus();
        ShowSearchResults = false;
        SearchText = "";

        // ── FIX Bug 8 : validation type d'article ↔ groupe de taxation ──
        if (!TaxCalculator.IsItemTypeValidForGroup(product.ItemType, product.TaxGroup))
        {
            bool isLOrN = product.TaxGroup == TaxGroup.L || product.TaxGroup == TaxGroup.N;
            StatusMessage = isLOrN
                ? $"« {product.Name} » : les groupes L/N exigent le type TAX."
                : $"« {product.Name} » : BIE/SER interdit dans les groupes L/N.";
            ShowError = true;
            return;
        }

        var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += 1;
            existing.Recalculate(PriceMode, _discountBeforeTax);

            // FIX Bug 9 : vérifier montant positif
            if (existing.AmountTTC <= 0m)
            {
                existing.Quantity -= 1;
                existing.Recalculate(PriceMode, _discountBeforeTax);
                StatusMessage = "Montant TTC résultant invalide (≤ 0).";
                ShowError = true;
                return;
            }
        }
        else
        {
            decimal ht, ttc;
            if (SelectedCurrency == Currency.CDF)
            {
                ht = product.UnitPriceHtCdf;
                ttc = product.UnitPriceTtcCdf;
            }
            else
            {
                ht = product.UnitPriceHtUsd;
                ttc = product.UnitPriceTtcUsd;
            }

            // ── FIX Bug 10 : champs typés au lieu de legacy ──
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

                // V6 : typed TS fields
                SpecificTaxType = product.SpecificTaxType,
                SpecificTaxValue = product.SpecificTaxValue,
                SpecificTaxName = product.HasSpecificTax
                                  ? $"TS {product.SpecificTaxDisplay}"
                                  : "",
                TaxApplicationMode = product.TaxSpecificMode == TaxSpecificMode.OnTotal
                    ? TaxApplicationMode.OnTotal
                    : TaxApplicationMode.PerArticle,
            };
            item.Recalculate(PriceMode, _discountBeforeTax);

            // FIX Bug 9 : vérifier montant positif
            if (item.AmountTTC <= 0m)
            {
                StatusMessage = $"« {product.Name} » : montant TTC résultant ≤ 0 (spec DGI art. 20-21).";
                ShowError = true;
                return;
            }

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
        CartItems.Clear(); SelectedCartItem = null;
        ShowDiscountPanel = false;
        RecalculateTotals(); ClearStatus();
    }

    // ══════════════════════════════════════════════════════════
    //  REMISE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleDiscountPanel()
    {
        if (SelectedCartItem == null || IsNormalized) { ShowDiscountPanel = false; return; }
        bool wasOpen = ShowDiscountPanel;
        CloseAllOverlays();
        if (!wasOpen) { ShowDiscountPanel = true; CustomDiscountValue = ""; IsPercentDiscount = true; }
    }

    [RelayCommand]
    private void ApplyQuickDiscount(string percentStr)
    {
        if (SelectedCartItem == null || IsNormalized) return;
        if (!decimal.TryParse(percentStr, out var pct) || pct <= 0) return;
        SelectedCartItem.DiscountType = DiscountType.Percentage;
        SelectedCartItem.DiscountValue = pct;
        SelectedCartItem.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals(); ShowDiscountPanel = false;
    }

    [RelayCommand]
    private void ApplyCustomDiscount()
    {
        if (SelectedCartItem == null || IsNormalized) return;
        if (!decimal.TryParse(CustomDiscountValue, out var val) || val <= 0)
        { StatusMessage = "Entrez une valeur de remise valide."; ShowError = true; return; }
        SelectedCartItem.DiscountType = IsPercentDiscount ? DiscountType.Percentage : DiscountType.FixedAmount;
        SelectedCartItem.DiscountValue = val;
        SelectedCartItem.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals(); ShowDiscountPanel = false; CustomDiscountValue = ""; ClearStatus();
    }

    [RelayCommand]
    private void RemoveItemDiscount()
    {
        if (SelectedCartItem == null || IsNormalized) return;
        SelectedCartItem.DiscountType = DiscountType.None;
        SelectedCartItem.DiscountValue = 0;
        SelectedCartItem.Recalculate(PriceMode, _discountBeforeTax);
        RecalculateTotals(); ShowDiscountPanel = false;
    }

    // ══════════════════════════════════════════════════════════
    //  CLIENT
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleClientPanel()
    {
        bool wasOpen = ShowClientPanel;
        CloseAllOverlays();
        if (!wasOpen) ShowClientPanel = true;
    }

    private async Task SearchClientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        { ClientSearchResults.Clear(); IsClientSearchOpen = false; return; }
        try
        {
            var results = await _clientService.SearchAsync(query, 8);
            ClientSearchResults.Clear();
            foreach (var c in results) ClientSearchResults.Add(c);
            IsClientSearchOpen = ClientSearchResults.Count > 0;
        }
        catch { IsClientSearchOpen = false; }
    }

    [RelayCommand]
    private void SelectClient(Client? client)
    {
        if (client == null) return;
        SelectedClientId = client.Id; SelectedClientType = client.Type;
        ClientNIF = client.NIF ?? ""; ClientName = client.Name;
        ClientAddress = client.Address ?? ""; ClientPhone = client.Phone ?? "";
        ClientEmail = client.Email ?? ""; ClientRCCM = client.RCCM ?? "";
        ClientSearchText = ""; ClientSearchResults.Clear(); IsClientSearchOpen = false;
        OnPropertyChanged(nameof(HasClientSelected));
    }

    [RelayCommand]
    private void ClearClient()
    {
        SelectedClientId = null; SelectedClientType = ClientType.PP;
        ClientNIF = ""; ClientName = ""; ClientAddress = "";
        ClientPhone = ""; ClientEmail = ""; ClientRCCM = "";
        ClientSearchText = "";
        OnPropertyChanged(nameof(HasClientSelected));
    }

    [RelayCommand]
    private async Task SaveClientInline()
    {
        ShowError = false; ShowSuccess = false;
        var client = new Client
        {
            Type = SelectedClientType,
            NIF = string.IsNullOrWhiteSpace(ClientNIF) ? null : ClientNIF.Trim(),
            Name = ClientName.Trim(),
            Address = string.IsNullOrWhiteSpace(ClientAddress) ? null : ClientAddress.Trim(),
            Phone = string.IsNullOrWhiteSpace(ClientPhone) ? null : ClientPhone.Trim(),
            Email = string.IsNullOrWhiteSpace(ClientEmail) ? null : ClientEmail.Trim(),
            RCCM = string.IsNullOrWhiteSpace(ClientRCCM) ? null : ClientRCCM.Trim(),
        };
        var result = await _clientService.CreateAsync(client);
        if (result.IsValid)
        { SelectedClientId = result.Client!.Id; StatusMessage = $"✓ Client « {client.Name} » enregistré."; ShowSuccess = true; OnPropertyChanged(nameof(HasClientSelected)); }
        else { StatusMessage = result.ErrorMessage; ShowError = true; }
    }

    // ══════════════════════════════════════════════════════════
    //  AVOIR
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleCreditNotePanel()
    {
        if (!IsCreditNote) return;
        bool wasOpen = ShowCreditNotePanel;
        CloseAllOverlays();
        if (!wasOpen) ShowCreditNotePanel = true;
    }

    [RelayCommand]
    private async Task LookupOriginalInvoice()
    {
        if (string.IsNullOrWhiteSpace(OriginalReference) || IsRRR) return;
        ShowError = false; ShowSuccess = false; IsLoadingOriginal = true;
        try
        {
            var original = await _invoiceService.LookupOriginalInvoiceAsync(OriginalReference.Trim());
            if (original == null)
            {
                StatusMessage = $"Facture introuvable pour « {OriginalReference} ».";
                ShowError = true; IsOriginalLoaded = false; _loadedOriginalInvoice = null;
                CreditNoteSelections.Clear(); return;
            }
            _loadedOriginalInvoice = original;
            _cumulativeRefunded = await _invoiceService.GetCumulativeRefundedQuantitiesAsync(OriginalReference.Trim());
            OriginalInvoiceSummary =
                $"{original.InvoiceNumber} — {original.ClientName} — {original.TotalTTC:N0} CDF — {original.CreatedAt:dd/MM/yyyy}";
            CreditNoteSelections.Clear();
            foreach (var line in original.Lines.OrderBy(l => l.LineNumber))
            {
                decimal alreadyRefunded = _cumulativeRefunded.GetValueOrDefault(line.Code, 0m);
                decimal maxQty = line.Quantity - alreadyRefunded;
                if (maxQty <= 0) continue;
                CreditNoteSelections.Add(new CreditNoteLineSelection
                {
                    OriginalLine = line,
                    OriginalQuantity = line.Quantity,
                    AlreadyRefunded = alreadyRefunded,
                    MaxQuantity = maxQty,
                    SelectedQuantity = maxQty,
                    IsSelected = false
                });
            }
            IsOriginalLoaded = true;
            StatusMessage = CreditNoteSelections.Count == 0
                ? "Tous les articles ont déjà été remboursés."
                : $"✓ Facture chargée — {CreditNoteSelections.Count} article(s) disponible(s)";
            if (CreditNoteSelections.Count == 0) ShowError = true; else ShowSuccess = true;
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; ShowError = true; }
        finally { IsLoadingOriginal = false; }
    }

    [RelayCommand]
    private void AddCreditNoteLine(CreditNoteLineSelection? selection)
    {
        if (selection == null || !selection.IsSelected) return;
        if (selection.SelectedQuantity <= 0 || selection.SelectedQuantity > selection.MaxQuantity)
        {
            StatusMessage = $"Quantité invalide. Max: {selection.MaxQuantity:G}";
            ShowError = true;
            return;
        }
        ShowError = false;

        var ol = selection.OriginalLine;

        // ── V6 : déterminer le type TS depuis les champs typés de la ligne originale ──
        // Si la ligne originale utilise encore les champs legacy, on parse :
        SpecificTaxType tsType = ol.SpecificTaxType;
        decimal tsValue = ol.SpecificTaxValue;

        // Fallback legacy si les champs typés ne sont pas renseignés
        if (tsType == SpecificTaxType.None && ol.HasSpecificTax)
        {
            var (parsedType, parsedValue) = TaxCalculator.ParseLegacySpecificTax(ol.TaxSpecificValue);
            tsType = parsedType;
            tsValue = parsedValue;
        }

        var item = new CartItemViewModel
        {
            ProductId = 0,
            Code = ol.Code,
            Name = ol.Name,
            ItemType = ol.ItemType,
            TaxGroup = ol.TaxGroup,
            UnitPriceHT = ol.UnitPriceHT,
            UnitPriceTTC = ol.UnitPriceTTC,
            Unit = ol.Unit,
            Quantity = selection.SelectedQuantity,
            DiscountType = ol.DiscountType,
            DiscountValue = ol.DiscountValue,

            // V6 : champs typés
            SpecificTaxType = tsType,
            SpecificTaxValue = tsValue,
            SpecificTaxName = ol.SpecificTaxName ?? "",
            TaxApplicationMode = ol.TaxApplicationMode,
        };
        item.Recalculate(PriceMode, _discountBeforeTax);

        CartItems.Add(item);
        RecalculateTotals();

        selection.MaxQuantity -= selection.SelectedQuantity;
        selection.AlreadyRefunded += selection.SelectedQuantity;
        selection.IsSelected = false;
        selection.SelectedQuantity = selection.MaxQuantity;

        if (selection.MaxQuantity <= 0)
            CreditNoteSelections.Remove(selection);
    }

    [RelayCommand]
    private void AddAllSelectedCreditNoteLines()
    {
        foreach (var s in CreditNoteSelections.Where(s => s.IsSelected).ToList())
            AddCreditNoteLine(s);
    }

    // ══════════════════════════════════════════════════════════
    //  ACOMPTE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleAdvancePanel()
    {
        if (!IsAdvanceInvoice) return;
        bool wasOpen = ShowAdvancePanel;
        CloseAllOverlays();
        if (!wasOpen) ShowAdvancePanel = true;
    }

    [RelayCommand]
    private void CreateNewAdvanceGroup()
    { AdvanceGroupId = _invoiceService.GenerateAdvanceGroupId(); PreviousAdvances.Clear(); AdvancesTotalPaid = 0; }

    [RelayCommand]
    private async Task LoadAdvanceGroup()
    {
        if (string.IsNullOrWhiteSpace(AdvanceGroupId)) return;
        try
        {
            var advances = await _invoiceService.GetAdvancesForGroupAsync(AdvanceGroupId);
            PreviousAdvances.Clear();
            foreach (var adv in advances)
                PreviousAdvances.Add(new AdvanceInvoiceSummary
                { InvoiceNumber = adv.InvoiceNumber, Date = adv.CreatedAt, Amount = adv.TotalTTC, CodeDEFDGI = adv.CodeDEFDGI });
            AdvancesTotalPaid = advances.Sum(a => a.TotalTTC);
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; ShowError = true; }
    }

    // ══════════════════════════════════════════════════════════
    //  COMMENTAIRES
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleCommentPanel()
    {
        bool wasOpen = ShowCommentPanel;
        CloseAllOverlays();
        if (!wasOpen) ShowCommentPanel = true;
    }

    // ══════════════════════════════════════════════════════════
    //  TOTAUX
    // ══════════════════════════════════════════════════════════
    private void RecalculateTotals()
    {
        // ═══════════════════════════════════════════════════════
        //  1. Remettre les lignes OnTotal à leur état de base
        // ═══════════════════════════════════════════════════════
        foreach (var item in CartItems)
        {
            if (item.TaxApplicationMode == TaxApplicationMode.OnTotal)
            {
                item.Recalculate(PriceMode, _discountBeforeTax);
            }
        }

        // V10: Pass 1.5 REMOVED — CartItemViewModel.Recalculate now
        // uses the fixed CalculateLineFull which produces correct
        // values for PerArticle + TS% at the source using Ceil2.

        // ═══════════════════════════════════════════════════════
        //  2. Distribuer la TS OnTotal et recalculer TVA / TTC
        //     V10: TS% → Ceil2 two-step + reverse
        // ═══════════════════════════════════════════════════════
        var onTotalGroups = CartItems
            .Where(l => l.TaxApplicationMode == TaxApplicationMode.OnTotal
                      && l.SpecificTaxType != SpecificTaxType.None
                      && l.SpecificTaxValue > 0)
            .GroupBy(l => new { l.SpecificTaxType, l.SpecificTaxValue });

        foreach (var grp in onTotalGroups)
        {
            var lines = grp.ToList();

            if (grp.Key.SpecificTaxType == SpecificTaxType.Percentage)
            {
                decimal tsRate = grp.Key.SpecificTaxValue / 100m;

                foreach (var line in lines)
                {
                    decimal goodsHT = line.AmountHT;
                    decimal vatRate = line.TaxRate / 100m;

                    decimal ts = TaxCalculator.Ceil2(goodsHT * tsRate);
                    decimal ht = goodsHT + ts;
                    decimal tva = TaxCalculator.R2(ht * vatRate);
                    decimal ttc = ht + tva;

                    line.TaxSpecificAmount = ts;
                    line.AmountHT = ht;
                    line.AmountTVA = tva;
                    line.AmountTTC = ttc;
                }
            }
            else
            {
                decimal groupHT = lines.Sum(l => l.AmountHT);
                decimal groupQty = lines.Sum(l => l.Quantity);

                decimal tsForGroup = TaxCalculator.ComputeOnTotalSpecificTax(
                    grp.Key.SpecificTaxType,
                    grp.Key.SpecificTaxValue,
                    groupHT,
                    groupQty);

                decimal distributed = 0m;

                for (int i = 0; i < lines.Count; i++)
                {
                    decimal share;
                    if (i < lines.Count - 1)
                    {
                        share = groupHT > 0
                            ? TaxCalculator.R2(tsForGroup * lines[i].AmountHT / groupHT)
                            : TaxCalculator.R2(tsForGroup / lines.Count);
                        distributed += share;
                    }
                    else
                    {
                        share = tsForGroup - distributed;
                    }

                    decimal originalGoodsHT = lines[i].AmountHT;
                    lines[i].TaxSpecificAmount = share;

                    decimal newBase = originalGoodsHT + share;
                    decimal newTTC = TaxCalculator.R2(newBase * (1m + lines[i].TaxRate / 100m));
                    decimal newTVA = newTTC - newBase;

                    lines[i].AmountHT = newBase;
                    lines[i].AmountTVA = newTVA;
                    lines[i].AmountTTC = newTTC;

                    if (lines[i].AmountHT + lines[i].AmountTVA != lines[i].AmountTTC)
                        lines[i].AmountTVA = lines[i].AmountTTC - lines[i].AmountHT;
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  3. Totaux
        // ═══════════════════════════════════════════════════════
        TotalHTBeforeDiscount = CartItems.Sum(c => c.AmountHTBeforeDiscount);
        TotalDiscount = CartItems.Sum(c => c.DiscountAmount);
        TotalHT = CartItems.Sum(c => c.AmountHT);
        TotalTVA = CartItems.Sum(c => c.AmountTVA);
        TotalTTC = CartItems.Sum(c => c.AmountTTC);
        TotalSpecificTax = CartItems.Sum(c => c.TaxSpecificAmount);
        GrandTotal = PriceMode == PriceMode.TTC ? TotalTTC : TotalHT;
        GrandTotalLabel = PriceMode == PriceMode.TTC ? "TOTAL TTC" : "TOTAL HT";
        TotalArticles = CartItems.Sum(c => (int)c.Quantity);
        CartItemCount = CartItems.Count;

        OnPropertyChanged(nameof(HasAnyDiscount));
        OnPropertyChanged(nameof(HasAnySpecificTax));
        OnPropertyChanged(nameof(IsCommentARequired));
        OnPropertyChanged(nameof(CommentALabel));

        TaxGroupSummaries.Clear();
        foreach (var g in CartItems.GroupBy(l => l.TaxGroup).OrderBy(g => g.Key))
        {
            TaxGroupSummaries.Add(new TaxGroupSummary
            {
                Group = g.Key,
                Label = $"{(char)('A' + (int)g.Key)} - {TaxCalculator.GetGroupLabel(g.Key)}",
                Rate = g.First().TaxRate,
                TotalHT = g.Sum(l => l.AmountHT),
                TotalTVA = g.Sum(l => l.AmountTVA),
                TotalTTC = g.Sum(l => l.AmountTTC)
            });
        }

        UpdateAlternateCurrency();
        RecalculatePayments();
        UpdateChange();
        _customerDisplay.UpdateCart(CartItems, GrandTotal, GrandTotalLabel, TotalArticles);
    }

    // ══════════════════════════════════════════════════════════
    //  DEVISE
    // ══════════════════════════════════════════════════════════

    private void UpdateAlternateCurrency()
    {
        if (SelectedCurrency == Currency.CDF)
        { AlternateCurrencyLabel = "USD"; TotalInAlternateCurrency = ExchangeRate > 0 ? Math.Round(TotalTTC / ExchangeRate, 2) : 0; }
        else
        { AlternateCurrencyLabel = "CDF"; TotalInAlternateCurrency = Math.Round(TotalTTC * ExchangeRate, 2); }
    }

    // ══════════════════════════════════════════════════════════
    //  PAIEMENT
    // ══════════════════════════════════════════════════════════

    private void UpdateChange()
    {
        if (PaymentItems.Count > 0)
        { ChangeAmount = Remaining < 0 ? Math.Abs(Remaining) : 0; ShowChange = ChangeAmount > 0; }
        else if (decimal.TryParse(ReceivedAmount, out var received) && received > TotalTTC)
        { ChangeAmount = received - TotalTTC; ShowChange = true; }
        else { ChangeAmount = 0; ShowChange = false; }
    }

    [RelayCommand] private void SetExactAmount() => ReceivedAmount = TotalTTC.ToString("F0");
    [RelayCommand]
    private void SetRoundedAmount(string amountStr)
    { if (decimal.TryParse(amountStr, out var amount)) ReceivedAmount = amount.ToString("F0"); }

    [RelayCommand]
    private void ToggleSplitPayment()
    { ShowSplitPayment = !ShowSplitPayment; if (!ShowSplitPayment) { PaymentItems.Clear(); RecalculatePayments(); } }

    [RelayCommand]
    private void AddPayment()
    {
        decimal amount;
        if (string.IsNullOrWhiteSpace(PaymentAmount)) amount = Remaining;
        else if (!decimal.TryParse(PaymentAmount, out amount) || amount <= 0)
        { StatusMessage = "Montant invalide."; ShowError = true; return; }
        if (amount <= 0) return;
        PaymentItems.Add(new PaymentDisplayItem
        { PaymentType = SelectedPaymentType, Amount = amount, Label = GetPaymentLabel(SelectedPaymentType) });
        PaymentAmount = ""; RecalculatePayments();
    }

    [RelayCommand]
    private void RemovePayment(PaymentDisplayItem? item)
    { if (item == null || IsNormalized) return; PaymentItems.Remove(item); RecalculatePayments(); }

    private void RecalculatePayments()
    { TotalPaid = PaymentItems.Sum(p => p.Amount); Remaining = TotalTTC - TotalPaid; UpdateChange(); }

    // ══════════════════════════════════════════════════════════
    //  PROCESS SALE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ProcessSale()
    {
        if (IsNormalized || CartItems.Count == 0) return;
        if (SelectedPointOfSale == null)
        { StatusMessage = "Veuillez sélectionner un point de vente."; ShowError = true; return; }
        ClearStatus(); IsBusy = true; StatusMessage = "Normalisation en cours...";
        try
        {
            decimal paidAmount = TotalTTC;
            if (PaymentItems.Count > 0)
            {
                if (Remaining > 0.01m)
                { PaymentItems.Add(new PaymentDisplayItem { PaymentType = PaymentType.Especes, Amount = Remaining, Label = "Espèces" }); RecalculatePayments(); }
                paidAmount = TotalPaid;
            }
            else if (decimal.TryParse(ReceivedAmount, out var received) && received >= TotalTTC) paidAmount = received;
            else if (string.IsNullOrWhiteSpace(ReceivedAmount)) paidAmount = TotalTTC;
            else { StatusMessage = "Le montant reçu est insuffisant."; ShowError = true; IsBusy = false; return; }

            var invoice = BuildInvoice(paidAmount);
            var result = await _invoiceService.NormalizeInvoiceAsync(invoice);

            if (result.Success)
            {
                IsNormalized = true; CodeDEFDGI = result.CodeDEFDGI; QrCodeContent = result.QRCodeContent;
                var saved = await _unitOfWork.Invoices.GetWithDetailsAsync(result.InvoiceId);
                if (saved != null)
                {
                    Nim = saved.NIM; Counters = saved.Counters; DeviceDateTime = saved.DeviceDateTime;
                    if (AutoPrintReceipt && HasThermalPrinter) await PrintThermalReceiptAsync(saved);
                    if (SelectedPointOfSale?.PrintCopies >= 2 && HasThermalPrinter)
                    { await Task.Delay(500); await PrintThermalReceiptAsync(saved, isDuplicate: true); }
                    _customerDisplay.ShowNormalized(saved.TotalTTC, result.CodeDEFDGI, result.QRCodeContent);
                }
                if (SelectedPaymentType == PaymentType.Especes && SelectedPointOfSale?.EnableCashDrawer == true && HasThermalPrinter)
                    OpenCashDrawer();
                ShowReceiptOverlay = true;
                StatusMessage = $"✓ Vente normalisée — {result.CodeDEFDGI}"; ShowSuccess = true;
                await RefreshDailyStatsAsync();
            }
            else { StatusMessage = result.ErrorMessage ?? "Erreur inconnue."; ShowError = true; }
        }
        catch (Exception ex) { StatusMessage = $"Erreur : {ex.Message}"; ShowError = true; }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════════════════
    //  THERMAL PRINTING
    // ══════════════════════════════════════════════════════════

    private async Task PrintThermalReceiptAsync(Invoice invoice, bool isDuplicate = false)
    {
        await Task.Run(() =>
        {
            try
            {
                var receiptBytes = EscPosReceiptBuilder.Build(invoice, _currentCompany!, SelectedPointOfSale, _currentExchangeRate, isDuplicate);
                RawPrinterHelper.SendBytesToPrinter(ThermalPrinterName, receiptBytes, $"Facture {invoice.InvoiceNumber}");
            }
            catch (Exception ex)
            { System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage += $" ⚠ Impression: {ex.Message}"); }
        });
    }

    [RelayCommand]
    private async Task ReprintReceipt()
    {
        if (!IsNormalized || string.IsNullOrEmpty(CodeDEFDGI)) return;
        try
        {
            var invoice = await _unitOfWork.Invoices.GetByCodeDEFDGIAsync(CodeDEFDGI);
            if (invoice != null) { await PrintThermalReceiptAsync(invoice, isDuplicate: true); StatusMessage = "✓ Duplicata imprimé."; ShowSuccess = true; }
        }
        catch (Exception ex) { StatusMessage = $"Erreur réimpression: {ex.Message}"; ShowError = true; }
    }

    [RelayCommand]
    private void OpenCashDrawer()
    {
        if (!HasThermalPrinter) return;
        RawPrinterHelper.SendBytesToPrinter(ThermalPrinterName, new byte[] { 0x1B, 0x70, 0x00, 0x32, 0x32 }, "CashDrawer");
    }

    // ══════════════════════════════════════════════════════════
    //  FACTURES EN ATTENTE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void TogglePendingPanel()
    {
        bool wasOpen = ShowPendingPanel;
        CloseAllOverlays();
        if (!wasOpen) ShowPendingPanel = true;
    }

    [RelayCommand]
    private async Task CheckPendingInvoices()
    {
        IsCheckingPending = true; ShowPendingError = false; ShowPendingSuccess = false;
        PendingStatusMessage = "Interrogation du dispositif…";
        try
        {
            var status = await _fiscalDevice.GetStatusAsync();
            if (!status.Success) { PendingStatusMessage = status.ErrorMessage ?? "Impossible de contacter le dispositif."; ShowPendingError = true; return; }
            PendingInvoiceCount = status.PendingCount; PendingInvoices.Clear();
            foreach (var p in status.PendingInvoices) PendingInvoices.Add(new PendingInvoiceItem { Uid = p.Uid, DateDisplay = p.DateDisplay });
            OnPropertyChanged(nameof(HasPendingInvoices));
            if (PendingInvoiceCount == 0) { PendingStatusMessage = "✓ Aucune facture en attente."; ShowPendingSuccess = true; }
            else { PendingStatusMessage = $"⚠ {PendingInvoiceCount} facture(s) en attente."; ShowPendingError = true; }
        }
        catch (Exception ex) { PendingStatusMessage = $"Erreur : {ex.Message}"; ShowPendingError = true; }
        finally { IsCheckingPending = false; }
    }

    [RelayCommand]
    private async Task CancelPendingInvoice(string? uid)
    {
        string targetUid = uid ?? CancelUid;
        if (string.IsNullOrWhiteSpace(targetUid)) { PendingStatusMessage = "Veuillez saisir un UID."; ShowPendingError = true; return; }
        ShowPendingError = false; ShowPendingSuccess = false; PendingStatusMessage = $"Annulation de {targetUid}…";
        try
        {
            bool cancelled = await _fiscalDevice.CancelPendingInvoiceAsync(targetUid);
            if (cancelled)
            {
                PendingStatusMessage = $"✓ Facture « {targetUid} » annulée."; ShowPendingSuccess = true;
                var item = PendingInvoices.FirstOrDefault(p => p.Uid == targetUid);
                if (item != null) PendingInvoices.Remove(item);
                PendingInvoiceCount = Math.Max(0, PendingInvoiceCount - 1);
                OnPropertyChanged(nameof(HasPendingInvoices)); CancelUid = "";
            }
            else { PendingStatusMessage = "Échec de l'annulation."; ShowPendingError = true; }
        }
        catch (Exception ex) { PendingStatusMessage = $"Erreur : {ex.Message}"; ShowPendingError = true; }
    }

    [RelayCommand]
    private async Task CancelAllPending()
    {
        if (PendingInvoices.Count == 0) return;
        int ok = 0, fail = 0;
        foreach (var item in PendingInvoices.ToList())
        { try { if (await _fiscalDevice.CancelPendingInvoiceAsync(item.Uid)) { PendingInvoices.Remove(item); ok++; } else fail++; } catch { fail++; } }
        PendingInvoiceCount = PendingInvoices.Count; OnPropertyChanged(nameof(HasPendingInvoices));
        PendingStatusMessage = fail == 0 ? $"✓ {ok} annulée(s)." : $"⚠ {ok} annulée(s), {fail} en échec.";
        if (fail == 0) ShowPendingSuccess = true; else ShowPendingError = true;
    }

    // ══════════════════════════════════════════════════════════
    //  NEW SALE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task NewSale()
    {
        CartItems.Clear(); SelectedCartItem = null; ShowDiscountPanel = false;
        ReceivedAmount = ""; PaymentAmount = ""; PaymentItems.Clear();
        TotalPaid = 0; Remaining = 0; ChangeAmount = 0; ShowChange = false;
        ShowSplitPayment = false; IsNormalized = false; ShowReceiptOverlay = false;
        CodeDEFDGI = ""; Nim = ""; Counters = ""; QrCodeContent = ""; DeviceDateTime = "";
        ClearClient();
        IsCreditNote = false; IsOriginalLoaded = false; OriginalInvoiceSummary = "";
        _loadedOriginalInvoice = null; _cumulativeRefunded.Clear(); CreditNoteSelections.Clear();
        OriginalReference = ""; ShowCreditNotePanel = false;
        IsAdvanceInvoice = false; AdvanceGroupId = ""; AdvancesTotalPaid = 0;
        PreviousAdvances.Clear(); ShowAdvancePanel = false;
        CommentA = ""; CommentB = ""; CommentC = ""; CommentD = "";
        CommentE = ""; CommentF = ""; CommentG = ""; CommentH = "";
        ShowCommentPanel = false; ShowPendingPanel = false; PendingStatusMessage = "";
        ShowPendingSuccess = false; ShowPendingError = false;
        GrandTotal = 0; GrandTotalLabel = PriceMode == PriceMode.TTC ? "TOTAL TTC" : "TOTAL HT";
        TaxGroupSummaries.Clear(); TotalInAlternateCurrency = 0;
        RecalculateTotals(); ClearStatus(); CloseAllOverlays();
        InvoiceType = InvoiceType.FV; await GenerateNewNumber();
        ShowFavoritesOnly = true; SelectedCategory = null;
        await LoadDisplayProductsAsync(); _customerDisplay.SetIdle();
    }

    private void ClearStatus() { StatusMessage = ""; ShowSuccess = false; ShowError = false; }

    private void CloseAllOverlays()
    {
        ShowDiscountPanel = false;
        ShowHeldPanel = false;
        ShowClientPanel = false;
        ShowCreditNotePanel = false;
        ShowAdvancePanel = false;
        ShowCommentPanel = false;
        ShowPendingPanel = false;
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
            ClientType = SelectedClientType,
            ClientNIF = ClientNIF,
            ClientName = ClientName,
            ClientAddress = ClientAddress,
            ClientPhone = ClientPhone,
            ClientEmail = ClientEmail,
            ClientRCCM = ClientRCCM,
            OperatorName = OperatorName,
            OperatorId = "01",
            CommentA = CommentA,
            CommentB = CommentB,
            CommentC = CommentC,
            CommentD = CommentD,
            CommentE = CommentE,
            CommentF = CommentF,
            CommentG = CommentG,
            CommentH = CommentH,
            CurrencyCode = SelectedCurrency.ToString(),
            CurrencyRate = ExchangeRate,
            TotalHTBeforeDiscount = TotalHTBeforeDiscount,
            TotalDiscount = TotalDiscount,
            PointOfSaleId = SelectedPointOfSale?.Id ?? 1
        };

        if (IsCreditNote)
        {
            invoice.CreditNoteNature = SelectedCreditNoteNature;
            invoice.OriginalInvoiceReference = OriginalReference;
            invoice.ReferenceType = SelectedCreditNoteNature.ToString();
            invoice.ReferenceDesc = GetCreditNoteDesc(SelectedCreditNoteNature);
        }
        if (IsAdvanceInvoice && !string.IsNullOrWhiteSpace(AdvanceGroupId))
            invoice.AdvanceGroupId = AdvanceGroupId;

        // ═══════════════════════════════════════════════════════════════
        //  V6 FIX : Les valeurs du ViewModel sont désormais correctes.
        //
        //  - PerArticle : AmountHT inclut TS, AmountTVA sur (HT+TS)
        //  - OnTotal    : AmountHT inclut TS distribuée, AmountTVA recalculée
        //  - HT + TVA = TTC garanti pour chaque ligne
        //
        //  On passe les valeurs directement. TaxSpecificAmount reste
        //  disponible pour la ventilation d'affichage sur le reçu.
        //
        //  FIX Bug 7 : on écrit les champs typés SpecificTaxType /
        //  SpecificTaxValue au lieu de SpecificTaxRate / TaxSpecificValue.
        // ═══════════════════════════════════════════════════════════════

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

                // ── V6 : champs typés TS ──
                HasSpecificTax = item.HasSpecificTax,
                SpecificTaxName = item.SpecificTaxName,
                SpecificTaxType = item.SpecificTaxType,
                SpecificTaxValue = item.SpecificTaxValue,
                TaxApplicationMode = item.TaxApplicationMode,

                // ── Montants fiscaux (TS déjà dans HT, TVA sur HT+TS) ──
                AmountHT = item.AmountHT,
                AmountTVA = item.AmountTVA,
                AmountTTC = item.AmountTTC,
                TaxSpecificAmount = item.TaxSpecificAmount,  // ventilation affichage
            });
        }

        // ── Totaux facture — cohérents avec les lignes ──
        invoice.TotalHT = TotalHT;
        invoice.TotalTVA = TotalTVA;
        invoice.TotalTTC = TotalTTC;
        invoice.TotalSpecificTax = TotalSpecificTax;  // ventilation, déjà dans TotalHT

        // ── Paiements (inchangés) ──
        if (PaymentItems.Count > 0)
        {
            foreach (var pay in PaymentItems)
                invoice.Payments.Add(new InvoicePayment
                {
                    PaymentType = pay.PaymentType,
                    Amount = pay.Amount,
                    CurrencyCode = SelectedCurrency.ToString(),
                    CurrencyRate = ExchangeRate
                });
        }
        else
        {
            invoice.Payments.Add(new InvoicePayment
            {
                PaymentType = SelectedPaymentType,
                Amount = paidAmount,
                CurrencyCode = SelectedCurrency.ToString(),
                CurrencyRate = ExchangeRate
            });
        }

        return invoice;
    }

    // ══════════════════════════════════════════════════════════
    //  HOLD / RECALL
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void RequestHold()
    {
        if (CartItems.Count == 0 || IsNormalized) return;
        if (CartItems.Count <= 2) { HoldReason = ""; HoldCurrentSale(); return; }
        HoldReason = ""; ShowHoldDialog = true;
    }

    [RelayCommand]
    private void HoldCurrentSale()
    {
        if (CartItems.Count == 0 || IsNormalized) return;
        ShowHoldDialog = false;
        var held = new HeldTransactionViewModel
        {
            Label = CartItems.Count == 1 ? CartItems[0].Name : $"{CartItems[0].Name} +{CartItems.Count - 1}",
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

                // V6 : champs typés TS
                SpecificTaxType = item.SpecificTaxType,
                SpecificTaxValue = item.SpecificTaxValue,
                SpecificTaxName = item.SpecificTaxName,
                TaxApplicationMode = item.TaxApplicationMode,
                TaxSpecificAmount = item.TaxSpecificAmount,

                AmountHT = item.AmountHT,
                AmountTVA = item.AmountTVA,
                AmountTTC = item.AmountTTC,
                StockQuantity = item.StockQuantity,
                TrackStock = item.TrackStock
            });
        HeldTransactions.Add(held); HeldCount = HeldTransactions.Count;
        CartItems.Clear(); SelectedCartItem = null; ShowDiscountPanel = false;
        ReceivedAmount = ""; ChangeAmount = 0; ShowChange = false;
        PaymentItems.Clear(); RecalculatePayments(); RecalculateTotals();
        _ = GenerateNewNumber();
        StatusMessage = $"⏸ Panier en attente — {held.ItemCount} article(s)"; ShowSuccess = true; HoldReason = "";
    }
    [RelayCommand]
    private void RecallHeldSale(HeldTransactionViewModel? held)
    {
        if (held == null || IsNormalized) return;
        if (CartItems.Count > 0) { HoldReason = "(auto-hold)"; HoldCurrentSale(); }
        CartItems.Clear();
        foreach (var snapshot in held.Items)
            CartItems.Add(new CartItemViewModel
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

                SpecificTaxType = snapshot.SpecificTaxType,
                SpecificTaxValue = snapshot.SpecificTaxValue,
                SpecificTaxName = snapshot.SpecificTaxName,
                TaxApplicationMode = snapshot.TaxApplicationMode,
                TaxSpecificAmount = snapshot.TaxSpecificAmount,

                AmountHT = snapshot.AmountHT,
                AmountTVA = snapshot.AmountTVA,
                AmountTTC = snapshot.AmountTTC,
                StockQuantity = snapshot.StockQuantity,
                TrackStock = snapshot.TrackStock
            });
        InvoiceNumber = held.InvoiceNumber; InvoiceType = held.InvoiceType;
        SelectedPaymentType = held.PaymentType; ReceivedAmount = held.ReceivedAmount;

        // V10: ALWAYS re-run Recalculate — held values may be from pre-V10 code
        foreach (var item in CartItems)
            item.Recalculate(PriceMode, _discountBeforeTax);

        RecalculateTotals();
        HeldTransactions.Remove(held); HeldCount = HeldTransactions.Count;
        ShowHeldPanel = HeldTransactions.Count > 0 && ShowHeldPanel;
        StatusMessage = $"▶ Panier rappelé — {held.ItemCount} article(s)"; ShowSuccess = true;
    }

    [RelayCommand]
    private void DeleteHeldSale(HeldTransactionViewModel? held)
    {
        if (held == null) return;
        HeldTransactions.Remove(held); HeldCount = HeldTransactions.Count;
        if (HeldTransactions.Count == 0) ShowHeldPanel = false;
        StatusMessage = "🗑 Panier supprimé"; ShowSuccess = true;
    }

    [RelayCommand]
    private void ToggleHeldPanel()
    {
        if (HeldTransactions.Count == 0) { ShowHeldPanel = false; return; }
        bool wasOpen = ShowHeldPanel;
        CloseAllOverlays();
        if (!wasOpen) ShowHeldPanel = true;
    }

    [RelayCommand]
    private void CancelHold() { ShowHoldDialog = false; HoldReason = ""; }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private static string GetPaymentLabel(PaymentType type) => type switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèques",
        PaymentType.Credit => "Crédit",
        PaymentType.Autre => "Autre",
        _ => type.ToString()
    };

    private static string GetCreditNoteDesc(CreditNoteNature nature) => nature switch
    {
        CreditNoteNature.COR => "Correction",
        CreditNoteNature.RAN => "Annulation",
        CreditNoteNature.RAM => "Remboursement",
        CreditNoteNature.RRR => "Remise/Ristourne/Rabais",
        _ => ""
    };

    // ══════════════════════════════════════════════════════════
    //  IActivatable — 🆕 Uses auth for POS selection
    // ══════════════════════════════════════════════════════════

    public async Task ActivateAsync()
    {
        if (_isFirstActivation) { _isFirstActivation = false; return; }
        try
        {
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null)
            {
                _currentCompany = company;
                var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);
                if (companyWithPos?.PointsOfSale != null)
                {
                    var activePosList = companyWithPos.PointsOfSale
                        .Where(p => p.IsActive).OrderBy(p => p.Code).ToList();
                    var previousId = SelectedPointOfSale?.Id;
                    AvailablePointsOfSale.Clear();
                    foreach (var pos in activePosList) AvailablePointsOfSale.Add(pos);
                    HasMultiplePos = activePosList.Count > 1;

                    // 🆕 Priority: user-assigned POS → previous → first
                    SelectedPointOfSale = PosSelectionHelper.SelectBestPos(
                        activePosList, _auth.CurrentUser?.PointOfSaleId, previousId);

                    Isf = company.ISF;
                }
            }
            try
            {
                var appSettings = await _unitOfWork.AppSettings.GetCurrentAsync();
                if (appSettings != null)
                {
                    _discountBeforeTax = appSettings.DiscountBeforeTax;
                    SelectedCurrency = appSettings.DefaultCurrency;
                    ExchangeRate = appSettings.CurrentExchangeRate;
                    _currentExchangeRate = appSettings.CurrentExchangeRate;
                }
            }
            catch { }

            // 🆕 Always refresh products on re-activation
            try { await LoadDisplayProductsAsync(); } catch { }
            try { await RefreshDailyStatsAsync(); } catch { }
            try { await LoadOperatorsAsync(); } catch { }
        }
        catch { }
    }
}