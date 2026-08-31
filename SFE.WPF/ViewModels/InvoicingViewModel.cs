using System.Collections.ObjectModel;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Helpers;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Messages;
using SFE.WPF.Helpers;
using SFE.Domain.Abstractions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SFE.WPF.Services;

namespace SFE.WPF.ViewModels;

public partial class InvoicingViewModel : BaseViewModel,
    IRecipient<PriceModeChangedMessage>,
    IRecipient<DiscountBeforeTaxChangedMessage>,
    IRecipient<ExchangeRateChangedMessage>,
    IActivatable
{
    private readonly InvoiceService _invoiceService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProductService _productService;
    private readonly ClientService _clientService;
    private readonly IFiscalDeviceService _fiscalDevice;
    private readonly IAuthService _auth;
    private readonly ITimeProvider _timeProvider;
    private readonly ManagerGate _gate;
    private bool _isFirstActivation = true;

    // ══════ OPERATORS ══════
    public ObservableCollection<string> AvailableOperators { get; } = new();

    // ══════ PARAMÈTRE GLOBAL ══════
    private bool _discountBeforeTax = true;

    // ══════ HEADER ══════
    [ObservableProperty] private InvoiceType _selectedInvoiceType = InvoiceType.FV;
    [ObservableProperty] private PriceMode _selectedPriceMode = PriceMode.TTC;
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private string _operatorName = "Opérateur";
    [ObservableProperty] private string _isf = "";
    // ⚠️ Ne PAS initialiser depuis DateTime.Now dans un initialiseur de champ.
    //    La valeur est fixée par le constructeur via _timeProvider.
    [ObservableProperty] private string _currentDateTime = "";

    // ══════ CLIENT ══════
    [ObservableProperty] private ClientType _selectedClientType = ClientType.PP;
    [ObservableProperty] private string _clientNIF = "";
    [ObservableProperty] private string _clientName = "";
    [ObservableProperty] private string _clientAddress = "";
    [ObservableProperty] private string _clientPhone = "";
    [ObservableProperty] private string _clientEmail = "";
    [ObservableProperty] private string _clientRCCM = "";
    [ObservableProperty] private bool _showClientDetails = false;

    public ObservableCollection<Client> ClientSearchResults { get; } = new();
    [ObservableProperty] private bool _isClientSearchOpen;
    [ObservableProperty] private string _clientSearchText = "";
    [ObservableProperty] private int? _selectedClientId;

    public bool IsClientNifRequired => SelectedClientType is ClientType.PM or ClientType.PC or ClientType.PL;
    public bool IsClientNameRequired => SelectedClientType != ClientType.PP;
    public string ClientTypeMention => ClientService.GetTypeMention(SelectedClientType);

    // ══════ AVOIR ══════
    [ObservableProperty] private CreditNoteNature _selectedCreditNoteNature = CreditNoteNature.COR;
    [ObservableProperty] private string _originalReference = "";
    [ObservableProperty] private bool _isCreditNote = false;
    [ObservableProperty] private bool _isOriginalLoaded;
    [ObservableProperty] private string _originalInvoiceSummary = "";
    [ObservableProperty] private bool _isLoadingOriginal;
    private Invoice? _loadedOriginalInvoice;
    private Dictionary<string, decimal> _cumulativeRefunded = new();
    public ObservableCollection<CreditNoteLineSelection> CreditNoteSelections { get; } = new();
    public bool IsRRR => IsCreditNote && SelectedCreditNoteNature == CreditNoteNature.RRR;
    public bool RequiresOriginalLookup => IsCreditNote && !IsRRR;

    // ══════ ACOMPTE ══════
    [ObservableProperty] private bool _isAdvanceInvoice;
    [ObservableProperty] private string _advanceGroupId = "";
    [ObservableProperty] private decimal _advancesTotalPaid;
    [ObservableProperty] private bool _showAdvanceSection;
    public ObservableCollection<AdvanceInvoiceSummary> PreviousAdvances { get; } = new();
    [ObservableProperty] private string _advanceAmountInput = "";

    // ══════ SAISIE ARTICLE ══════
    [ObservableProperty] private string _articleSearch = "";
    [ObservableProperty] private string _articleCode = "";
    [ObservableProperty] private string _articleName = "";
    [ObservableProperty] private ItemType _articleItemType = ItemType.BIE;
    [ObservableProperty] private TaxGroup _articleTaxGroup = TaxGroup.B;
    [ObservableProperty] private TaxGroupAType? _articleTaxGroupAType;   // 🆕

    [ObservableProperty] private string _articleUnitPrice = "";
    [ObservableProperty] private string _articleQuantity = "1";
    [ObservableProperty] private string _articleUnit = "pce";

    [ObservableProperty] private DiscountType _articleDiscountType = DiscountType.None;
    [ObservableProperty] private string _articleDiscountValue = "";

    [ObservableProperty] private SpecificTaxType _articleSpecificTaxType = SpecificTaxType.None;
    [ObservableProperty] private string _articleSpecificTaxValue = "";
    [ObservableProperty] private string _articleSpecificTaxName = "";
    [ObservableProperty] private TaxApplicationMode _articleTaxApplicationMode = TaxApplicationMode.PerArticle;
    [ObservableProperty] private bool _showArticleSpecificTaxFields;

    [ObservableProperty] private Currency _selectedCurrency = Currency.CDF;
    [ObservableProperty] private decimal _exchangeRate = 2800m;
    [ObservableProperty] private decimal _totalInAlternateCurrency;
    [ObservableProperty] private string _alternateCurrencyLabel = "USD";

    // ══════ POINT OF SALE ══════
    public ObservableCollection<PointOfSale> AvailablePointsOfSale { get; } = new();
    [ObservableProperty] private PointOfSale? _selectedPointOfSale;
    [ObservableProperty] private bool _hasMultiplePos;
    [ObservableProperty] private string _selectedPosInfo = "";

    public string[] CommonUnits { get; } = new[]
    {
        "pce", "kg", "g", "L", "mL", "m", "m²", "m³",
        "bte", "btle", "sac", "pqt", "cart", "ram", "fût",
        "h", "j", "mois", "fft", "lot",
    };

    // ══════ PAIEMENT ══════
    [ObservableProperty] private PaymentType _selectedPaymentType = PaymentType.Especes;
    [ObservableProperty] private string _paymentAmount = "";

    // ══════ TOTAUX ══════
    [ObservableProperty] private decimal _totalHTBeforeDiscount;
    [ObservableProperty] private decimal _totalDiscount;
    [ObservableProperty] private decimal _totalHT;
    [ObservableProperty] private decimal _totalTVA;
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private decimal _totalSpecificTax;
    [ObservableProperty] private decimal _totalPaid;
    [ObservableProperty] private decimal _remaining;

    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private string _grandTotalLabel = "TOTAL TTC";

    public bool HasAnyDiscount => TotalDiscount > 0;
    public bool ArticleHasSpecificTax => ArticleSpecificTaxType != SpecificTaxType.None;

    // ══════ SÉCURITÉ ══════
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _nim = "";
    [ObservableProperty] private string _counters = "";
    [ObservableProperty] private string _deviceDateTime = "";
    [ObservableProperty] private string _qrCodeContent = "";
    [ObservableProperty] private bool _isNormalized = false;

    // ══════ STATUS ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess = false;
    [ObservableProperty] private bool _showError = false;

    // ══════ COMMENTAIRES ══════
    [ObservableProperty] private string _commentA = "";
    [ObservableProperty] private string _commentB = "";
    [ObservableProperty] private string _commentC = "";
    [ObservableProperty] private string _commentD = "";
    [ObservableProperty] private string _commentE = "";
    [ObservableProperty] private string _commentF = "";
    [ObservableProperty] private string _commentG = "";
    [ObservableProperty] private string _commentH = "";

    // ══════ FACTURES EN ATTENTE ══════
    [ObservableProperty] private int _pendingInvoiceCount;
    [ObservableProperty] private string _cancelUid = "";
    [ObservableProperty] private bool _isCheckingPending;
    [ObservableProperty] private string _pendingStatusMessage = "";
    [ObservableProperty] private bool _showPendingSuccess;
    [ObservableProperty] private bool _showPendingError;
    [ObservableProperty] private bool _showPendingSection;

    // ══════ PROFORMA ══════
    [ObservableProperty] private bool _isProforma;
    // ⚠️ Fixé par le constructeur via _timeProvider.
    [ObservableProperty] private DateTime? _proformaValidUntil;
    [ObservableProperty] private bool _showProformaSection;

    public bool IsFiscalInvoice => !IsProforma;
    public ObservableCollection<PendingInvoiceItem> PendingInvoices { get; } = new();
    public bool HasPendingInvoices => PendingInvoiceCount > 0;

    public bool IsCommentARequired =>
        SelectedClientType == ClientType.AO ||
        InvoiceLines.Any(l => l.TaxGroup == TaxGroup.D);

    public string CommentALabel => SelectedClientType == ClientType.AO
        ? "Réf. certificat d'exonération *"
        : InvoiceLines.Any(l => l.TaxGroup == TaxGroup.D)
            ? "Réf. document de dérogation DGI *"
            : "Ligne A (optionnel)";

    // ══════ COLLECTIONS ══════
    public ObservableCollection<InvoiceLineViewModel> InvoiceLines { get; } = new();
    public ObservableCollection<PaymentDisplayItem> PaymentItems { get; } = new();
    public ObservableCollection<TaxGroupSummary> TaxGroupSummaries { get; } = new();

    // ══════ ENUMS POUR ComboBox ══════
    public InvoiceType[] InvoiceTypes { get; } = Enum.GetValues<InvoiceType>();
    public PriceMode[] PriceModes { get; } = Enum.GetValues<PriceMode>();
    public ClientType[] ClientTypes { get; } = Enum.GetValues<ClientType>();
    public ItemType[] ItemTypes { get; } = Enum.GetValues<ItemType>();
    public TaxGroup[] TaxGroups { get; } = Enum.GetValues<TaxGroup>();
    public PaymentType[] PaymentTypes { get; } = Enum.GetValues<PaymentType>();
    public CreditNoteNature[] CreditNoteNatures { get; } = Enum.GetValues<CreditNoteNature>();
    public DiscountType[] DiscountTypes { get; } = Enum.GetValues<DiscountType>();
    public SpecificTaxType[] SpecificTaxTypes { get; } = Enum.GetValues<SpecificTaxType>();
    public TaxApplicationMode[] TaxApplicationModes { get; } = Enum.GetValues<TaxApplicationMode>();
    public Currency[] Currencies { get; } = Enum.GetValues<Currency>();

    public bool IsHtMode => SelectedPriceMode == PriceMode.HT;
    public string PriceModeDisplay => SelectedPriceMode == PriceMode.TTC ? "Prix TTC" : "Prix HT";

    // ══════ RECHERCHE PRODUIT ══════
    public ObservableCollection<Product> ProductSearchResults { get; } = new();
    [ObservableProperty] private bool _isSearchPopupOpen;

    // ══════ PROPRIÉTÉS CALCULÉES — T.S. détaillées ══════
    public bool HasAnySpecificTax => TotalSpecificTax > 0;
    public bool HasFixedSpecificTax => InvoiceLines
        .Any(l => l.SpecificTaxType == SpecificTaxType.FixedPerUnit && l.TaxSpecificAmount > 0);
    public decimal TotalFixedSpecificTax => InvoiceLines
        .Where(l => l.SpecificTaxType == SpecificTaxType.FixedPerUnit)
        .Sum(l => l.TaxSpecificAmount);
    public bool HasPercentSpecificTax => InvoiceLines
        .Any(l => l.SpecificTaxType == SpecificTaxType.Percentage && l.TaxSpecificAmount > 0);
    public decimal TotalPercentSpecificTax => InvoiceLines
        .Where(l => l.SpecificTaxType == SpecificTaxType.Percentage)
        .Sum(l => l.TaxSpecificAmount);

    // ══════════════════════════════════════════════════════════
    //  PARTIAL CHANGE HANDLERS
    // ══════════════════════════════════════════════════════════

    partial void OnSelectedPriceModeChanged(PriceMode value)
    {
        if (IsNormalized) return;
        OnPropertyChanged(nameof(IsHtMode));
        OnPropertyChanged(nameof(PriceModeDisplay));
        RecalculateAllLines();
    }

    partial void OnArticleSpecificTaxTypeChanged(SpecificTaxType value)
    {
        ShowArticleSpecificTaxFields = value != SpecificTaxType.None;
        OnPropertyChanged(nameof(ArticleHasSpecificTax));
    }

    partial void OnArticleNameChanged(string value) => _ = SearchProductsAsync(value);

    private async Task SearchProductsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            ProductSearchResults.Clear();
            IsSearchPopupOpen = false;
            return;
        }

        try
        {
            var results = await _productService.SearchAsync(query, 8);
            ProductSearchResults.Clear();
            foreach (var p in results)
                ProductSearchResults.Add(p);
            IsSearchPopupOpen = ProductSearchResults.Count > 0;
        }
        catch { IsSearchPopupOpen = false; }
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

        // 🆕 Régénérer le numéro avec le préfixe du nouveau POS
        if (!IsNormalized && InvoiceLines.Count == 0)
            _ = GenerateNewInvoiceNumber();
    }

    [RelayCommand]
    private void SelectProduct(Product? product)
    {
        if (product == null) return;

        ArticleCode = product.Code;
        ArticleName = product.Name;
        ArticleItemType = product.ItemType;
        ArticleTaxGroup = product.TaxGroup;
        ArticleTaxGroupAType = product.TaxGroupAType;   // 🆕

        if (SelectedPriceMode == PriceMode.TTC)
        {
            ArticleUnitPrice = SelectedCurrency == Currency.CDF
                ? product.UnitPriceTtcCdf.ToString("F2")
                : product.UnitPriceTtcUsd.ToString("F2");
        }
        else
        {
            ArticleUnitPrice = SelectedCurrency == Currency.CDF
                ? product.UnitPriceHtCdf.ToString("F2")
                : product.UnitPriceTtcUsd.ToString("F2");
        }

        ArticleUnit = product.Unit;

        ArticleSpecificTaxType = product.SpecificTaxType;
        ArticleSpecificTaxValue = product.SpecificTaxValue > 0
            ? product.SpecificTaxValue.ToString("G") : "";

        ArticleTaxApplicationMode = product.TaxSpecificMode == TaxSpecificMode.OnTotal
            ? TaxApplicationMode.OnTotal
            : TaxApplicationMode.PerArticle;

        ArticleDiscountType = product.DefaultDiscountType;
        ArticleDiscountValue = product.DefaultDiscountValue > 0
            ? product.DefaultDiscountValue.ToString("G")
            : "";

        ProductSearchResults.Clear();
        IsSearchPopupOpen = false;
    }

    partial void OnSelectedInvoiceTypeChanged(InvoiceType value)
    {
        IsCreditNote = value == InvoiceType.FA || value == InvoiceType.EA;
        IsAdvanceInvoice = value == InvoiceType.FT || value == InvoiceType.ET;
        IsProforma = value == InvoiceType.PRO;
        ShowAdvanceSection = IsAdvanceInvoice;
        ShowProformaSection = IsProforma;

        if (!IsCreditNote)
        {
            IsOriginalLoaded = false;
            OriginalInvoiceSummary = "";
            _loadedOriginalInvoice = null;
            CreditNoteSelections.Clear();
        }

        OnPropertyChanged(nameof(IsRRR));
        OnPropertyChanged(nameof(RequiresOriginalLookup));
        OnPropertyChanged(nameof(IsFiscalInvoice));

        _ = GenerateNewInvoiceNumber();
    }

    partial void OnSelectedCreditNoteNatureChanged(CreditNoteNature value)
    {
        OnPropertyChanged(nameof(IsRRR));
        OnPropertyChanged(nameof(RequiresOriginalLookup));

        if (IsRRR)
        {
            OriginalReference = "RRR";
            IsOriginalLoaded = false;
            _loadedOriginalInvoice = null;
            CreditNoteSelections.Clear();
        }
        else
        {
            if (OriginalReference == "RRR")
                OriginalReference = "";
        }
    }

    partial void OnSelectedClientTypeChanged(ClientType value)
    {
        ShowClientDetails = value != ClientType.PP;

        OnPropertyChanged(nameof(IsClientNifRequired));
        OnPropertyChanged(nameof(IsClientNameRequired));
        OnPropertyChanged(nameof(ClientTypeMention));
        OnPropertyChanged(nameof(IsCommentARequired));
        OnPropertyChanged(nameof(CommentALabel));
    }

    partial void OnSelectedCurrencyChanged(Currency value) => UpdateAlternateCurrency();
    partial void OnExchangeRateChanged(decimal value) => UpdateAlternateCurrency();
    partial void OnClientSearchTextChanged(string value) => _ = SearchClientsAsync(value);

    // ══════════════════════════════════════════════════════════
    //  CONSTRUCTEUR
    // ══════════════════════════════════════════════════════════

    public InvoicingViewModel(
        InvoiceService invoiceService, IUnitOfWork unitOfWork,
        ProductService productService, ClientService clientService,
        IFiscalDeviceService fiscalDevice,
        IAuthService auth,
        ManagerGate gate,
        ITimeProvider timeProvider)
    {
        _invoiceService = invoiceService;
        _unitOfWork = unitOfWork;
        _productService = productService;
        _clientService = clientService;
        _fiscalDevice = fiscalDevice;
        _auth = auth;
        _timeProvider = timeProvider;
        _gate = gate; 
        PageTitle = "Facturation";

        // 🆕 Initialisation temporelle via ITimeProvider (plus de DateTime.Now direct)
        var nowLocal = _timeProvider.LocalNow;
        CurrentDateTime = nowLocal.ToString("dd/MM/yyyy HH:mm");
        ProformaValidUntil = nowLocal.Date.AddDays(30);

        // 🆕 Set operator name from logged-in user
        OperatorName = _auth.CurrentUser?.FullName ?? "Opérateur";

        WeakReferenceMessenger.Default.Register<PriceModeChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<DiscountBeforeTaxChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<ExchangeRateChangedMessage>(this);

        _ = InitializeAsync();
    }

    // ══════════════════════════════════════════════════════════
    //  MESSAGES
    // ══════════════════════════════════════════════════════════

    public void Receive(PriceModeChangedMessage message) => SelectedPriceMode = message.Value;

    public void Receive(DiscountBeforeTaxChangedMessage message)
    {
        _discountBeforeTax = message.Value;
        if (!IsNormalized) RecalculateAllLines();
    }

    public void Receive(ExchangeRateChangedMessage message)
    {
        ExchangeRate = message.Value.UsdRate;
        UpdateAlternateCurrency();
    }

    // ══════════════════════════════════════════════════════════
    //  INITIALISATION
    // ══════════════════════════════════════════════════════════

    private async Task InitializeAsync()
    {
        try
        {
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null)
            {
                var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);
                Isf = company.ISF;
                SelectedPriceMode = company.DefaultPriceMode;

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

                    SelectedPointOfSale = PosSelectionHelper.SelectBestPos(
                        activePosList, _auth.CurrentUser?.PointOfSaleId);
                }
            }

            try
            {
                var appSettings = await _unitOfWork.AppSettings.GetCurrentAsync();
                if (appSettings != null)
                {
                    _discountBeforeTax = appSettings.DiscountBeforeTax;
                    SelectedPriceMode = appSettings.DefaultPriceMode;
                    SelectedCurrency = appSettings.DefaultCurrency;
                    ExchangeRate = appSettings.CurrentExchangeRate;
                }
            }
            catch { _discountBeforeTax = true; }

            await GenerateNewInvoiceNumber();
            await LoadOperatorsAsync();
        }
        catch { }
    }

    private async Task LoadOperatorsAsync()
    {
        try
        {
            var operators = await _unitOfWork.Invoices.GetDistinctOperatorNamesAsync();

            AvailableOperators.Clear();
            foreach (var name in operators.OrderBy(n => n))
                AvailableOperators.Add(name);

            if (!string.IsNullOrEmpty(OperatorName) && !AvailableOperators.Contains(OperatorName))
                AvailableOperators.Insert(0, OperatorName);

            if (string.IsNullOrEmpty(OperatorName) && AvailableOperators.Count > 0)
                OperatorName = AvailableOperators[0];
        }
        catch { }
    }

    private async Task GenerateNewInvoiceNumber()
    {
        if (SelectedPointOfSale == null)
        {
            InvoiceNumber = "";
            return;
        }

        InvoiceNumber = SelectedInvoiceType == InvoiceType.PRO
            ? await _invoiceService.GenerateProformaNumberAsync(SelectedPointOfSale.Id)
            : await _invoiceService.GenerateInvoiceNumberAsync(SelectedInvoiceType, SelectedPointOfSale.Id);
    }

    // ══════════════════════════════════════════════════════════
    //  AJOUT D'ARTICLE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddArticle()
    {
        ShowError = false;
        ShowSuccess = false;

        if (string.IsNullOrWhiteSpace(ArticleName))
        {
            StatusMessage = "Le nom de l'article est obligatoire.";
            ShowError = true;
            return;
        }

        if (!DecimalParsingHelper.TryParseFlexible(ArticleUnitPrice, out var unitPrice)
            || unitPrice <= 0)
        {
            StatusMessage = "Le prix unitaire doit être un nombre positif.";
            ShowError = true;
            return;
        }

        if (!DecimalParsingHelper.TryParseFlexible(ArticleQuantity, out var quantity)
            || quantity <= 0)
        {
            StatusMessage = "La quantité doit être un nombre positif.";
            ShowError = true;
            return;
        }

        if (!TaxCalculator.IsItemTypeValidForGroup(ArticleItemType, ArticleTaxGroup))
        {
            StatusMessage = ArticleTaxGroup == TaxGroup.N
                ? "Le groupe N (Taxes et redevances) exige le type d'article TAX."
                : "Le type d'article TAX est réservé au groupe N (Taxes et redevances).";
            ShowError = true;
            return;
        }

        unitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        quantity = Math.Round(quantity, 3, MidpointRounding.AwayFromZero);

        var taxRate = TaxCalculator.GetDefaultRate(ArticleTaxGroup);

        decimal specificTaxVal = 0m;
        var specificTaxType = ArticleSpecificTaxType;
        if (specificTaxType != SpecificTaxType.None)
        {
            if (!DecimalParsingHelper.TryParseFlexible(ArticleSpecificTaxValue, out specificTaxVal)
                || specificTaxVal <= 0)
            {
                specificTaxType = SpecificTaxType.None;
                specificTaxVal = 0m;
            }
        }

        bool hasSpecificTax = specificTaxType != SpecificTaxType.None && specificTaxVal > 0;

        var tsTypeForPricing = hasSpecificTax && ArticleTaxApplicationMode != TaxApplicationMode.OnTotal
            ? specificTaxType
            : SpecificTaxType.None;
        var tsValForPricing = hasSpecificTax && ArticleTaxApplicationMode != TaxApplicationMode.OnTotal
            ? specificTaxVal
            : 0m;

        var (ht, ttc) = TaxCalculator.EnsureDualPrices(
            unitPrice, SelectedPriceMode, taxRate,
            tsTypeForPricing, tsValForPricing);

        var discountType = ArticleDiscountType;
        decimal discountValue = 0;
        if (discountType != DiscountType.None
            && DecimalParsingHelper.TryParseFlexible(ArticleDiscountValue, out var dv) && dv > 0)
        {
            discountValue = dv;
        }
        else
        {
            discountType = DiscountType.None;
        }

        var input = new LineCalculationInput
        {
            UnitPriceHT = ht,
            UnitPriceTTC = ttc,
            Quantity = quantity,
            TaxGroup = ArticleTaxGroup,
            TaxRate = taxRate,
            PriceMode = SelectedPriceMode,
            DiscountType = discountType,
            DiscountValue = discountValue,
            DiscountBeforeTax = _discountBeforeTax,
            SpecificTaxType = specificTaxType,
            SpecificTaxValue = specificTaxVal,
            TaxApplicationMode = ArticleTaxApplicationMode
        };

        var calc = TaxCalculator.CalculateLineFull(input);

        if (calc.AmountTTC <= 0m)
        {
            StatusMessage = "Le montant TTC de l'article doit être strictement positif (spec DGI art. 20-21).";
            ShowError = true;
            return;
        }

        var lineVm = new InvoiceLineViewModel
        {
            LineNumber = InvoiceLines.Count + 1,
            Code = ArticleCode,
            Name = ArticleName,
            ItemType = ArticleItemType,
            TaxGroup = ArticleTaxGroup,
            TaxGroupAType = ArticleTaxGroup == TaxGroup.A ? ArticleTaxGroupAType : null,  // 🆕
            TaxRate = taxRate,
            UnitPriceHT = ht,
            UnitPriceTTC = ttc,
            Quantity = quantity,
            Unit = ArticleUnit,
            DiscountType = discountType,
            DiscountValue = discountValue,
            DiscountAmount = calc.DiscountAmount,
            AmountHTBeforeDiscount = calc.AmountHTBeforeDiscount,
            SpecificTaxName = ArticleSpecificTaxName,
            SpecificTaxType = specificTaxType,
            SpecificTaxValue = specificTaxVal,
            TaxApplicationMode = ArticleTaxApplicationMode,
            TaxSpecificAmount = calc.TaxSpecificAmount,
            AmountHT = calc.AmountHT,
            AmountTVA = calc.AmountTVA,
            AmountTTC = calc.AmountTTC
        };

        InvoiceLines.Add(lineVm);
        RecalculateTotals();
        ClearArticleFields();
    }

    [RelayCommand]
    private void RemoveArticle(InvoiceLineViewModel? line)
    {
        if (line == null || IsNormalized) return;
        InvoiceLines.Remove(line);
        RenumberLines();
        RecalculateTotals();
    }

    private void ClearArticleFields()
    {
        ArticleCode = "";
        ArticleName = "";
        ArticleUnitPrice = "";
        ArticleQuantity = "1";
        ArticleUnit = "pce";
        ArticleSpecificTaxType = SpecificTaxType.None;
        ArticleSpecificTaxValue = "";
        ArticleSpecificTaxName = "";
        ArticleTaxApplicationMode = TaxApplicationMode.PerArticle;
        ArticleDiscountType = DiscountType.None;
        ArticleDiscountValue = "";
    }

    private void RenumberLines()
    {
        for (int i = 0; i < InvoiceLines.Count; i++)
            InvoiceLines[i].LineNumber = i + 1;
    }

    // ══════════════════════════════════════════════════════════
    //  RECALCUL
    // ══════════════════════════════════════════════════════════

    private void RecalculateAllLines()
    {
        foreach (var line in InvoiceLines)
        {
            var taxRate = TaxCalculator.GetDefaultRate(line.TaxGroup);

            var input = new LineCalculationInput
            {
                UnitPriceHT = line.UnitPriceHT,
                UnitPriceTTC = line.UnitPriceTTC,
                Quantity = line.Quantity,
                TaxGroup = line.TaxGroup,
                TaxRate = taxRate,
                PriceMode = SelectedPriceMode,
                DiscountType = line.DiscountType,
                DiscountValue = line.DiscountValue,
                DiscountBeforeTax = _discountBeforeTax,
                SpecificTaxType = line.SpecificTaxType,
                SpecificTaxValue = line.SpecificTaxValue,
                TaxApplicationMode = line.TaxApplicationMode
            };

            var calc = TaxCalculator.CalculateLineFull(input);

            line.TaxRate = taxRate;
            line.AmountHTBeforeDiscount = calc.AmountHTBeforeDiscount;
            line.DiscountAmount = calc.DiscountAmount;
            line.AmountHT = calc.AmountHT;
            line.AmountTVA = calc.AmountTVA;
            line.TaxSpecificAmount = calc.TaxSpecificAmount;
            line.AmountTTC = calc.AmountTTC;
        }

        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        bool isTTC = SelectedPriceMode == PriceMode.TTC;

        // 1. Remettre les lignes OnTotal à leur état de base
        foreach (var line in InvoiceLines)
        {
            if (line.TaxApplicationMode == TaxApplicationMode.OnTotal)
            {
                var input = new LineCalculationInput
                {
                    UnitPriceHT = line.UnitPriceHT,
                    UnitPriceTTC = line.UnitPriceTTC,
                    Quantity = line.Quantity,
                    TaxGroup = line.TaxGroup,
                    TaxRate = line.TaxRate,
                    PriceMode = SelectedPriceMode,
                    DiscountType = line.DiscountType,
                    DiscountValue = line.DiscountValue,
                    DiscountBeforeTax = _discountBeforeTax,
                    SpecificTaxType = line.SpecificTaxType,
                    SpecificTaxValue = line.SpecificTaxValue,
                    TaxApplicationMode = TaxApplicationMode.OnTotal
                };
                var calc = TaxCalculator.CalculateLineFull(input);
                line.AmountHTBeforeDiscount = calc.AmountHTBeforeDiscount;
                line.DiscountAmount = calc.DiscountAmount;
                line.AmountHT = calc.AmountHT;
                line.AmountTVA = calc.AmountTVA;
                line.AmountTTC = calc.AmountTTC;
                line.TaxSpecificAmount = 0m;
            }
        }

        // 2. Distribuer la TS OnTotal
        var onTotalGroups = InvoiceLines
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
                    decimal vatRate = line.TaxRate / 100m;

                    if (isTTC)
                    {
                        decimal goodsTTC = line.AmountTTC;
                        decimal goodsHT = line.AmountHT;
                        decimal tvaGoods = goodsTTC - goodsHT;

                        decimal ts = TaxCalculator.R2(goodsTTC * tsRate);
                        decimal tvaTS = TaxCalculator.R2(ts * vatRate);

                        line.TaxSpecificAmount = ts;
                        line.AmountHT = goodsHT + ts;
                        line.AmountTVA = tvaGoods + tvaTS;
                        line.AmountTTC = line.AmountHT + line.AmountTVA;
                    }
                    else
                    {
                        decimal goodsHT = line.AmountHT;
                        decimal ts = TaxCalculator.R2(goodsHT * tsRate);
                        decimal ht = goodsHT + ts;
                        decimal tva = TaxCalculator.R2(ht * vatRate);
                        decimal ttc = ht + tva;

                        line.TaxSpecificAmount = ts;
                        line.AmountHT = ht;
                        line.AmountTVA = tva;
                        line.AmountTTC = ttc;
                    }

                    if (line.AmountHT + line.AmountTVA != line.AmountTTC)
                        line.AmountTVA = line.AmountTTC - line.AmountHT;
                }
            }
            else
            {
                decimal groupQty = lines.Sum(l => l.Quantity);
                decimal tsForGroup = TaxCalculator.ComputeOnTotalSpecificTax(
                    grp.Key.SpecificTaxType, grp.Key.SpecificTaxValue, 0m, groupQty);

                decimal distributionBase = isTTC
                    ? lines.Sum(l => l.AmountTTC)
                    : lines.Sum(l => l.AmountHT);

                decimal distributed = 0m;

                for (int i = 0; i < lines.Count; i++)
                {
                    decimal share;
                    if (i < lines.Count - 1)
                    {
                        decimal lineBase = isTTC ? lines[i].AmountTTC : lines[i].AmountHT;
                        share = distributionBase > 0
                            ? TaxCalculator.R2(tsForGroup * lineBase / distributionBase)
                            : TaxCalculator.R2(tsForGroup / lines.Count);
                        distributed += share;
                    }
                    else
                    {
                        share = tsForGroup - distributed;
                    }

                    decimal vatRate = lines[i].TaxRate / 100m;
                    lines[i].TaxSpecificAmount = share;

                    if (isTTC)
                    {
                        decimal goodsTTC = lines[i].AmountTTC;
                        decimal goodsHT = lines[i].AmountHT;
                        decimal tvaGoods = goodsTTC - goodsHT;
                        decimal tvaTS = TaxCalculator.R2(share * vatRate);

                        lines[i].AmountHT = goodsHT + share;
                        lines[i].AmountTVA = tvaGoods + tvaTS;
                        lines[i].AmountTTC = lines[i].AmountHT + lines[i].AmountTVA;
                    }
                    else
                    {
                        decimal goodsHT = lines[i].AmountHT;
                        decimal newBase = goodsHT + share;
                        decimal newTTC = TaxCalculator.R2(newBase * (1m + vatRate));
                        decimal newTVA = newTTC - newBase;

                        lines[i].AmountHT = newBase;
                        lines[i].AmountTVA = newTVA;
                        lines[i].AmountTTC = newTTC;
                    }

                    if (lines[i].AmountHT + lines[i].AmountTVA != lines[i].AmountTTC)
                        lines[i].AmountTVA = lines[i].AmountTTC - lines[i].AmountHT;
                }
            }
        }

        // 3. Group-level DGI rounding alignment
        var taxGroups = InvoiceLines
            .Where(l => l.TaxGroup != TaxGroup.N && l.TaxRate > 0)
            .GroupBy(l => l.TaxGroup);

        foreach (var group in taxGroups)
        {
            var groupLines = group.ToList();
            if (groupLines.Count == 0) continue;

            decimal rate = groupLines.First().TaxRate / 100m;

            if (isTTC)
            {
                decimal groupTTC = groupLines.Sum(l => l.AmountTTC);
                decimal expectedHT = TaxCalculator.R2(groupTTC / (1m + rate));
                decimal expectedTVA = groupTTC - expectedHT;
                decimal actualTVA = groupLines.Sum(l => l.AmountTVA);
                decimal diff = expectedTVA - actualTVA;

                if (diff != 0m)
                {
                    var lastLine = groupLines.Last();
                    lastLine.AmountTVA += diff;
                    lastLine.AmountHT -= diff;
                }
            }
            else
            {
                decimal groupHT = groupLines.Sum(l => l.AmountHT);
                decimal expectedTVA = TaxCalculator.R2(groupHT * rate);
                decimal actualTVA = groupLines.Sum(l => l.AmountTVA);
                decimal diff = expectedTVA - actualTVA;

                if (diff != 0m)
                {
                    var lastLine = groupLines.Last();
                    lastLine.AmountTVA += diff;
                    lastLine.AmountTTC += diff;
                }
            }
        }

        // 4. Totaux
        TotalHTBeforeDiscount = InvoiceLines.Sum(l => l.AmountHTBeforeDiscount);
        TotalDiscount = InvoiceLines.Sum(l => l.DiscountAmount);
        TotalHT = InvoiceLines.Sum(l => l.AmountHT);
        TotalTVA = InvoiceLines.Sum(l => l.AmountTVA);
        TotalSpecificTax = InvoiceLines.Sum(l => l.TaxSpecificAmount);
        TotalTTC = InvoiceLines.Sum(l => l.AmountTTC);

        GrandTotal = SelectedPriceMode == PriceMode.TTC ? TotalTTC : TotalHT;
        GrandTotalLabel = SelectedPriceMode == PriceMode.TTC ? "TOTAL TTC" : "TOTAL HT";

        OnPropertyChanged(nameof(HasAnyDiscount));
        OnPropertyChanged(nameof(HasAnySpecificTax));
        OnPropertyChanged(nameof(HasFixedSpecificTax));
        OnPropertyChanged(nameof(TotalFixedSpecificTax));
        OnPropertyChanged(nameof(HasPercentSpecificTax));
        OnPropertyChanged(nameof(TotalPercentSpecificTax));

        UpdateAlternateCurrency();

        TaxGroupSummaries.Clear();
        var groups = InvoiceLines
            .GroupBy(l => l.TaxGroup)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            var rate2 = g.First().TaxRate;
            TaxGroupSummaries.Add(new TaxGroupSummary
            {
                Group = g.Key,
                Label = $"{(char)('A' + (int)g.Key)} - {TaxCalculator.GetGroupLabel(g.Key)}",
                Rate = rate2,
                TotalHT = g.Sum(l => l.AmountHT),
                TotalTVA = g.Sum(l => l.AmountTVA),
                TotalTTC = g.Sum(l => l.AmountTTC)
            });
        }

        RecalculatePayments();
        OnPropertyChanged(nameof(IsCommentARequired));
        OnPropertyChanged(nameof(CommentALabel));
    }

    // ══════════════════════════════════════════════════════════
    //  DEVISE
    // ══════════════════════════════════════════════════════════

    private void UpdateAlternateCurrency()
    {
        if (SelectedCurrency == Currency.CDF)
        {
            AlternateCurrencyLabel = "USD";
            TotalInAlternateCurrency = ExchangeRate > 0
                ? Math.Round(TotalTTC / ExchangeRate, 2)
                : 0;
        }
        else
        {
            AlternateCurrencyLabel = "CDF";
            TotalInAlternateCurrency = Math.Round(TotalTTC * ExchangeRate, 2);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PAIEMENT
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddPayment()
    {
        decimal amount;
        if (string.IsNullOrWhiteSpace(PaymentAmount))
        {
            amount = Remaining;
        }
        else if (!DecimalParsingHelper.TryParseFlexible(PaymentAmount, out amount) || amount <= 0)
        {
            StatusMessage = "Le montant du paiement doit être un nombre positif.";
            ShowError = true;
            return;
        }

        if (amount <= 0) return;

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        PaymentItems.Add(new PaymentDisplayItem
        {
            PaymentType = SelectedPaymentType,
            Amount = amount,
            Label = GetPaymentLabel(SelectedPaymentType)
        });

        PaymentAmount = "";
        RecalculatePayments();
    }

    [RelayCommand]
    private void RemovePayment(PaymentDisplayItem? item)
    {
        if (item == null || IsNormalized) return;
        PaymentItems.Remove(item);
        RecalculatePayments();
    }

    private void RecalculatePayments()
    {
        TotalPaid = PaymentItems.Sum(p => p.Amount);
        Remaining = TotalTTC - TotalPaid;
    }

    // ══════════════════════════════════════════════════════════
    //  NORMALISATION
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task NormalizeInvoice()
    {
        if (IsNormalized) return;

        if (SelectedPointOfSale == null)
        {
            StatusMessage = "Veuillez sélectionner un point de vente.";
            ShowError = true;
            return;
        }

        IsBusy = true;
        ShowError = false;
        ShowSuccess = false;

        try
        {
            // ─── BRANCHE PROFORMA ───
            if (IsProforma)
            {
                StatusMessage = "Sauvegarde de la proforma…";

                var proforma = BuildInvoiceEntity();
                proforma.ProformaValidUntil = ProformaValidUntil;

                var proResult = await _invoiceService.SaveProformaAsync(proforma);

                if (proResult.Success)
                {
                    IsNormalized = true;
                    StatusMessage = $"✓ Proforma enregistrée — {proforma.InvoiceNumber}";
                    ShowSuccess = true;
                }
                else
                {
                    StatusMessage = proResult.ErrorMessage ?? "Erreur inconnue";
                    ShowError = true;
                }
                return;
            }

            if (IsCreditNote)
            {
                var ok = await _gate.RequireAsync(
                    ManagerAction.IssueCreditNote,
                    new AuthorizationContext
                    {
                        InvoiceNumber = InvoiceNumber,
                        Amount = TotalTTC,
                        Reason = $"{SelectedCreditNoteNature} — Réf: {OriginalReference}",
                        RequestingUserId = _auth.CurrentUser?.Id,
                        RequestingUserName = _auth.CurrentUser?.FullName ?? OperatorName
                    });

                if (!ok)
                {
                    StatusMessage = "Avoir refusé — autorisation manager requise.";
                    ShowError = true;
                    return;                    // finally { IsBusy = false; } will run
                }
            }
            if (IsCreditNote)
            {
                var ok = await _gate.RequireAsync(
                    ManagerAction.IssueCreditNote,
                    new AuthorizationContext
                    {
                        InvoiceNumber = InvoiceNumber,
                        Amount = TotalTTC,
                        Reason = $"{SelectedCreditNoteNature} — Réf: {OriginalReference}",
                        RequestingUserId = _auth.CurrentUser?.Id,
                        RequestingUserName = _auth.CurrentUser?.FullName ?? OperatorName
                    });

                if (!ok)
                {
                    StatusMessage = "Avoir refusé — autorisation manager requise.";
                    ShowError = true;
                    return;                    // finally { IsBusy = false; } will run
                }
            }

            StatusMessage = "Normalisation en cours...";

            decimal advanceAmount = 0m;
            if (IsAdvanceInvoice)
            {
                if (!DecimalParsingHelper.TryParseFlexible(AdvanceAmountInput, out advanceAmount) || advanceAmount <= 0)
                {
                    StatusMessage = "Veuillez saisir le montant de l'acompte reçu.";
                    ShowError = true;
                    IsBusy = false;
                    return;
                }

                if (advanceAmount > TotalTTC + 0.01m)
                {
                    StatusMessage = $"L'acompte ({advanceAmount:N2}) ne peut pas dépasser le total commandé ({TotalTTC:N2}).";
                    ShowError = true;
                    IsBusy = false;
                    return;
                }

                PaymentItems.Clear();
                PaymentItems.Add(new PaymentDisplayItem
                {
                    PaymentType = SelectedPaymentType,
                    Amount = advanceAmount,
                    Label = GetPaymentLabel(SelectedPaymentType)
                });
                RecalculatePayments();
            }
            else
            {
                if (PaymentItems.Count == 0 || Remaining > 0)
                {
                    if (Remaining > 0)
                    {
                        PaymentItems.Add(new PaymentDisplayItem
                        {
                            PaymentType = PaymentType.Especes,
                            Amount = Remaining,
                            Label = "Espèces"
                        });
                        RecalculatePayments();
                    }
                }
            }

            var invoice = BuildInvoiceEntity(advanceAmount);
            var result = await _invoiceService.NormalizeInvoiceAsync(invoice);

            if (result.Success)
            {
                CodeDEFDGI = result.CodeDEFDGI;
                QrCodeContent = result.QRCodeContent;
                IsNormalized = true;

                var saved = await _unitOfWork.Invoices.GetWithDetailsAsync(result.InvoiceId);
                if (saved != null)
                {
                    Nim = saved.NIM;
                    Counters = saved.Counters;
                    DeviceDateTime = saved.DeviceDateTime;
                }

                StatusMessage = $"✓ Facture normalisée — {result.CodeDEFDGI}";
                ShowSuccess = true;

                bool printed = await PrintNormalizedInvoiceAsync(result.InvoiceId);
                if (printed)
                {
                    StatusMessage = $"✓ Facture normalisée et imprimée — {result.CodeDEFDGI}";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[Normalize] Auto-print failed; user can reprint from history.");
                }
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Erreur inconnue";
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
    private async Task NewInvoice()
    {
        InvoiceLines.Clear();
        PaymentItems.Clear();
        TaxGroupSummaries.Clear();

        ClientNIF = ""; ClientName = ""; ClientAddress = "";
        ClientPhone = ""; ClientEmail = ""; ClientRCCM = "";
        SelectedClientType = ClientType.PP;
        OriginalReference = "";
        CommentA = "";

        TotalHTBeforeDiscount = 0; TotalDiscount = 0;
        TotalHT = 0; TotalTVA = 0; TotalTTC = 0;
        TotalSpecificTax = 0; TotalPaid = 0; Remaining = 0;
        GrandTotal = 0;
        TotalInAlternateCurrency = 0;
        GrandTotalLabel = SelectedPriceMode == PriceMode.TTC ? "TOTAL TTC" : "TOTAL HT";

        CodeDEFDGI = ""; Nim = ""; Counters = "";
        DeviceDateTime = ""; QrCodeContent = "";
        IsNormalized = false;
        StatusMessage = "";
        ShowSuccess = false; ShowError = false;

        IsOriginalLoaded = false;
        OriginalInvoiceSummary = "";
        _loadedOriginalInvoice = null;
        _cumulativeRefunded.Clear();
        CreditNoteSelections.Clear();
        PreviousAdvances.Clear();
        AdvanceGroupId = "";
        AdvancesTotalPaid = 0;
        AdvanceAmountInput = "";

        SelectedClientId = null;
        ClientSearchText = "";

        CommentA = ""; CommentB = ""; CommentC = ""; CommentD = "";
        CommentE = ""; CommentF = ""; CommentG = ""; CommentH = "";

        PendingInvoices.Clear();
        PendingInvoiceCount = 0;
        PendingStatusMessage = "";
        ShowPendingSuccess = false;
        ShowPendingError = false;
        ShowPendingSection = false;
        CancelUid = "";

        await GenerateNewInvoiceNumber();

        var nowLocal = _timeProvider.LocalNow;
        CurrentDateTime = nowLocal.ToString("dd/MM/yyyy HH:mm");
        ProformaValidUntil = nowLocal.Date.AddDays(30);
    }

    // ══════════════════════════════════════════════════════════
    //  IMPRESSION AUTOMATIQUE
    // ══════════════════════════════════════════════════════════

    private async Task<bool> PrintNormalizedInvoiceAsync(int invoiceId)
    {
        try
        {
            int printNo;
            var time = _timeProvider;
            try
            {
                printNo = await _invoiceService.RegisterPrintAsync(invoiceId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoPrint] RegisterPrint failed: {ex.Message}");
                printNo = 1;
            }

            var invoice = await _unitOfWork.Invoices.GetWithDetailsAsync(invoiceId);
            if (invoice == null) return false;

            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();

            PointOfSale? pos = null;
            if (invoice.PointOfSaleId > 0)
                pos = await _unitOfWork.GetRepository<PointOfSale>()
                    .GetByIdAsync(invoice.PointOfSaleId);

            decimal exchangeRate = ExchangeRate;
            try
            {
                var settings = await _unitOfWork.AppSettings.GetCurrentAsync();
                if (settings != null && settings.CurrentExchangeRate > 0)
                    exchangeRate = settings.CurrentExchangeRate;
            }
            catch { }

            byte[]? logoBytes = company?.Logo;

            var vm = InvoiceDocumentViewModel.Create(
                invoice,
                company,
                _timeProvider,
                pos: pos,
                exchangeRate: exchangeRate);
            if (vm == null) return false;

            vm.SourceInvoice = invoice;
            vm.SourceCompany = company;
            vm.SourcePos = pos;
            vm.SourceExchangeRate = exchangeRate;
            vm.SourceLogoBytes = logoBytes;
            vm.PrintNumber = printNo;

            InvoicePrintHelper.Print(vm, time);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoPrint] Failed: {ex.Message}");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CONSTRUCTION DE L'ENTITÉ
    // ══════════════════════════════════════════════════════════

    private Invoice BuildInvoiceEntity(decimal advanceAmount = 0m)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = InvoiceNumber,
            Type = SelectedInvoiceType,
            PriceMode = SelectedPriceMode,
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

        if (IsAdvanceInvoice)
        {
            if (!string.IsNullOrWhiteSpace(AdvanceGroupId))
                invoice.AdvanceGroupId = AdvanceGroupId;

            invoice.OrderTotal = TotalTTC;
            invoice.AdvanceAmount = advanceAmount;
            invoice.PreviousAdvancesTotal = AdvancesTotalPaid;
        }

        int lineNum = 1;
        foreach (var lineVm in InvoiceLines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = lineNum++,
                Code = lineVm.Code,
                Name = lineVm.Name,
                ItemType = lineVm.ItemType,
                TaxGroup = lineVm.TaxGroup,
                TaxGroupAType = lineVm.TaxGroup == TaxGroup.A ? lineVm.TaxGroupAType : null,  // 🆕
                TaxRate = lineVm.TaxRate,
                UnitPriceHT = lineVm.UnitPriceHT,
                UnitPriceTTC = lineVm.UnitPriceTTC,
                Quantity = lineVm.Quantity,
                Unit = lineVm.Unit,
                DiscountType = lineVm.DiscountType,
                DiscountValue = lineVm.DiscountValue,
                DiscountAmount = lineVm.DiscountAmount,
                AmountHTBeforeDiscount = lineVm.AmountHTBeforeDiscount,

                HasSpecificTax = lineVm.HasSpecificTax,
                SpecificTaxName = lineVm.SpecificTaxName,
                SpecificTaxType = lineVm.SpecificTaxType,
                SpecificTaxValue = lineVm.SpecificTaxValue,
                TaxApplicationMode = lineVm.TaxApplicationMode,

                AmountHT = lineVm.AmountHT,
                AmountTVA = lineVm.AmountTVA,
                AmountTTC = lineVm.AmountTTC,
                TaxSpecificAmount = lineVm.TaxSpecificAmount,
            });
        }

        invoice.TotalHT = TotalHT;
        invoice.TotalTVA = TotalTVA;
        invoice.TotalTTC = TotalTTC;
        invoice.TotalSpecificTax = TotalSpecificTax;

        foreach (var pay in PaymentItems)
        {
            invoice.Payments.Add(new InvoicePayment
            {
                PaymentType = pay.PaymentType,
                Amount = pay.Amount
            });
        }

        return invoice;
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS STATIQUES
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

    // ══════ RECHERCHE CLIENT ══════

    private async Task SearchClientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            ClientSearchResults.Clear();
            IsClientSearchOpen = false;
            return;
        }

        try
        {
            var results = await _clientService.SearchAsync(query, 8);
            ClientSearchResults.Clear();
            foreach (var c in results)
                ClientSearchResults.Add(c);
            IsClientSearchOpen = ClientSearchResults.Count > 0;
        }
        catch { IsClientSearchOpen = false; }
    }

    [RelayCommand]
    private void SelectClient(Client? client)
    {
        if (client == null) return;

        SelectedClientId = client.Id;
        SelectedClientType = client.Type;
        ClientNIF = client.NIF ?? "";
        ClientName = client.Name;
        ClientAddress = client.Address ?? "";
        ClientPhone = client.Phone ?? "";
        ClientEmail = client.Email ?? "";
        ClientRCCM = client.RCCM ?? "";

        ClientSearchText = "";
        ClientSearchResults.Clear();
        IsClientSearchOpen = false;
    }

    [RelayCommand]
    private void ClearClient()
    {
        SelectedClientId = null;
        SelectedClientType = ClientType.PP;
        ClientNIF = ""; ClientName = ""; ClientAddress = "";
        ClientPhone = ""; ClientEmail = ""; ClientRCCM = "";
        ClientSearchText = "";
    }

    [RelayCommand]
    private async Task SaveClientInline()
    {
        ShowError = false;
        ShowSuccess = false;

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
        {
            SelectedClientId = result.Client!.Id;
            StatusMessage = $"✓ Client « {client.Name} » enregistré.";
            ShowSuccess = true;
        }
        else
        {
            StatusMessage = result.ErrorMessage;
            ShowError = true;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  AVOIR — Recherche facture originale
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LookupOriginalInvoice()
    {
        if (string.IsNullOrWhiteSpace(OriginalReference) || IsRRR)
            return;

        ShowError = false;
        ShowSuccess = false;
        IsLoadingOriginal = true;

        try
        {
            var original = await _invoiceService.LookupOriginalInvoiceAsync(OriginalReference.Trim());

            if (original == null)
            {
                StatusMessage = $"Facture introuvable pour le Code DEF/DGI « {OriginalReference} ».";
                ShowError = true;
                IsOriginalLoaded = false;
                _loadedOriginalInvoice = null;
                CreditNoteSelections.Clear();
                return;
            }

            _loadedOriginalInvoice = original;

            _cumulativeRefunded = await _invoiceService.GetCumulativeRefundedQuantitiesAsync(
                OriginalReference.Trim());

            // 🆕 CreatedAt est DateTimeOffset → on formatte en heure locale
            OriginalInvoiceSummary =
                $"{original.InvoiceNumber} — {original.ClientName} — " +
                $"{original.TotalTTC:N0} CDF — {original.CreatedAt.LocalDateTime:dd/MM/yyyy}";

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

            if (CreditNoteSelections.Count == 0)
            {
                StatusMessage = "Tous les articles de cette facture ont déjà été remboursés.";
                ShowError = true;
            }
            else
            {
                StatusMessage = $"✓ Facture chargée — {CreditNoteSelections.Count} article(s) disponible(s)";
                ShowSuccess = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
        finally
        {
            IsLoadingOriginal = false;
        }
    }

    [RelayCommand]
    private void AddCreditNoteLine(CreditNoteLineSelection? selection)
    {
        if (selection == null || !selection.IsSelected) return;
        if (selection.SelectedQuantity <= 0 || selection.SelectedQuantity > selection.MaxQuantity)
        {
            StatusMessage = $"Quantité invalide pour « {selection.OriginalLine.Name} ». Max: {selection.MaxQuantity:G}";
            ShowError = true;
            return;
        }

        ShowError = false;
        var ol = selection.OriginalLine;
        var taxRate = TaxCalculator.GetDefaultRate(ol.TaxGroup);

        var input = new LineCalculationInput
        {
            UnitPriceHT = ol.UnitPriceHT,
            UnitPriceTTC = ol.UnitPriceTTC,
            Quantity = selection.SelectedQuantity,
            TaxGroup = ol.TaxGroup,
            TaxGroupAType = ol.TaxGroupAType,   // 🆕 preserve
            TaxRate = taxRate,
            PriceMode = SelectedPriceMode,
            DiscountType = ol.DiscountType,
            DiscountValue = ol.DiscountValue,
            DiscountBeforeTax = _discountBeforeTax,
            SpecificTaxType = ol.SpecificTaxType,
            SpecificTaxValue = ol.SpecificTaxValue,
            TaxApplicationMode = ol.TaxApplicationMode
        };

        var calc = TaxCalculator.CalculateLineFull(input);

        var lineVm = new InvoiceLineViewModel
        {
            LineNumber = InvoiceLines.Count + 1,
            Code = ol.Code,
            Name = ol.Name,
            ItemType = ol.ItemType,
            TaxGroup = ol.TaxGroup,
            TaxRate = taxRate,
            UnitPriceHT = ol.UnitPriceHT,
            UnitPriceTTC = ol.UnitPriceTTC,
            Quantity = selection.SelectedQuantity,
            Unit = ol.Unit,
            DiscountType = ol.DiscountType,
            DiscountValue = ol.DiscountValue,
            DiscountAmount = calc.DiscountAmount,
            AmountHTBeforeDiscount = calc.AmountHTBeforeDiscount,
            SpecificTaxName = ol.SpecificTaxName,
            SpecificTaxType = ol.SpecificTaxType,
            SpecificTaxValue = ol.SpecificTaxValue,
            TaxApplicationMode = ol.TaxApplicationMode,
            TaxSpecificAmount = calc.TaxSpecificAmount,
            AmountHT = calc.AmountHT,
            AmountTVA = calc.AmountTVA,
            AmountTTC = calc.AmountTTC
        };

        InvoiceLines.Add(lineVm);
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
        var selected = CreditNoteSelections.Where(s => s.IsSelected).ToList();
        foreach (var s in selected)
            AddCreditNoteLine(s);
    }

    // ══════════════════════════════════════════════════════════
    //  ACOMPTE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private void CreateNewAdvanceGroup()
    {
        AdvanceGroupId = _invoiceService.GenerateAdvanceGroupId();
        PreviousAdvances.Clear();
        AdvancesTotalPaid = 0;
    }

    [RelayCommand]
    private async Task LoadAdvanceGroup()
    {
        if (string.IsNullOrWhiteSpace(AdvanceGroupId)) return;

        try
        {
            var advances = await _invoiceService.GetAdvancesForGroupAsync(AdvanceGroupId);
            PreviousAdvances.Clear();

            foreach (var adv in advances)
            {
                PreviousAdvances.Add(new AdvanceInvoiceSummary
                {
                    InvoiceNumber = adv.InvoiceNumber,
                    // 🆕 CreatedAt est DateTimeOffset — on le conserve tel quel
                    Date = adv.CreatedAt,
                    Amount = adv.TotalTTC,
                    CodeDEFDGI = adv.CodeDEFDGI
                });
            }

            AdvancesTotalPaid = advances.Sum(a => a.TotalTTC);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement avances : {ex.Message}";
            ShowError = true;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FACTURES EN ATTENTE
    // ══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task CheckPendingInvoices()
    {
        IsCheckingPending = true;
        ShowPendingError = false;
        ShowPendingSuccess = false;
        PendingStatusMessage = "Interrogation du dispositif…";
        ShowPendingSection = true;

        try
        {
            var status = await _fiscalDevice.GetStatusAsync();

            if (!status.Success)
            {
                PendingStatusMessage = status.ErrorMessage ?? "Impossible de contacter le dispositif.";
                ShowPendingError = true;
                return;
            }

            PendingInvoiceCount = status.PendingCount;
            PendingInvoices.Clear();

            foreach (var p in status.PendingInvoices)
            {
                PendingInvoices.Add(new PendingInvoiceItem
                {
                    Uid = p.Uid,
                    DateDisplay = p.DateDisplay
                });
            }

            OnPropertyChanged(nameof(HasPendingInvoices));

            if (PendingInvoiceCount == 0)
            {
                PendingStatusMessage = "✓ Aucune facture en attente.";
                ShowPendingSuccess = true;
            }
            else
            {
                PendingStatusMessage = $"⚠ {PendingInvoiceCount} facture(s) en attente.";
                ShowPendingError = true;
            }
        }
        catch (Exception ex)
        {
            PendingStatusMessage = $"Erreur : {ex.Message}";
            ShowPendingError = true;
        }
        finally
        {
            IsCheckingPending = false;
        }
    }

    [RelayCommand]
    private async Task CancelPendingInvoice(string? uid)
    {
        string targetUid = uid ?? CancelUid;

        if (string.IsNullOrWhiteSpace(targetUid))
        {
            PendingStatusMessage = "Veuillez saisir ou sélectionner un UID à annuler.";
            ShowPendingError = true;
            ShowPendingSuccess = false;
            return;
        }

        ShowPendingError = false;
        ShowPendingSuccess = false;
        PendingStatusMessage = $"Annulation de {targetUid}…";

        try
        {
            bool cancelled = await _fiscalDevice.CancelPendingInvoiceAsync(targetUid);

            if (cancelled)
            {
                PendingStatusMessage = $"✓ Facture « {targetUid} » annulée avec succès.";
                ShowPendingSuccess = true;

                var item = PendingInvoices.FirstOrDefault(p => p.Uid == targetUid);
                if (item != null)
                    PendingInvoices.Remove(item);

                PendingInvoiceCount = Math.Max(0, PendingInvoiceCount - 1);
                OnPropertyChanged(nameof(HasPendingInvoices));

                CancelUid = "";
            }
            else
            {
                PendingStatusMessage = $"Échec de l'annulation de « {targetUid} ».";
                ShowPendingError = true;
            }
        }
        catch (Exception ex)
        {
            PendingStatusMessage = $"Erreur : {ex.Message}";
            ShowPendingError = true;
        }
    }

    [RelayCommand]
    private async Task CancelAllPending()
    {
        if (PendingInvoices.Count == 0) return;

        ShowPendingError = false;
        ShowPendingSuccess = false;

        int successCount = 0;
        int failCount = 0;
        var toRemove = PendingInvoices.ToList();

        foreach (var item in toRemove)
        {
            PendingStatusMessage = $"Annulation de {item.Uid}…";
            try
            {
                bool ok = await _fiscalDevice.CancelPendingInvoiceAsync(item.Uid);
                if (ok)
                {
                    PendingInvoices.Remove(item);
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            catch
            {
                failCount++;
            }
        }

        PendingInvoiceCount = PendingInvoices.Count;
        OnPropertyChanged(nameof(HasPendingInvoices));

        if (failCount == 0)
        {
            PendingStatusMessage = $"✓ {successCount} facture(s) annulée(s) avec succès.";
            ShowPendingSuccess = true;
        }
        else
        {
            PendingStatusMessage = $"⚠ {successCount} annulée(s), {failCount} en échec.";
            ShowPendingError = true;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  IActivatable
    // ══════════════════════════════════════════════════════════

    public async Task ActivateAsync()
    {
        if (_isFirstActivation) { _isFirstActivation = false; return; }
        if (IsNormalized || InvoiceLines.Count > 0) return;

        try
        {
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null)
            {
                var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);
                Isf = company.ISF;

                if (companyWithPos?.PointsOfSale != null)
                {
                    var activePosList = companyWithPos.PointsOfSale
                        .Where(p => p.IsActive).OrderBy(p => p.Code).ToList();
                    var previousPosId = SelectedPointOfSale?.Id;
                    AvailablePointsOfSale.Clear();
                    foreach (var pos in activePosList) AvailablePointsOfSale.Add(pos);
                    HasMultiplePos = activePosList.Count > 1;
                    SelectedPointOfSale = PosSelectionHelper.SelectBestPos(
                        activePosList, _auth.CurrentUser?.PointOfSaleId, previousPosId);
                }
            }

            await GenerateNewInvoiceNumber();
            // 🆕 Via ITimeProvider plutôt que DateTime.Now
            CurrentDateTime = _timeProvider.LocalNow.ToString("dd/MM/yyyy HH:mm");
            await LoadOperatorsAsync();
        }
        catch { }
    }
}

// ══════ HELPER CLASSES ══════

public class PaymentDisplayItem
{
    public PaymentType PaymentType { get; set; }
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
}

public class TaxGroupSummary
{
    public TaxGroup Group { get; set; }
    public string Label { get; set; } = "";
    public decimal Rate { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
}

/// <summary>
/// Ligne de la facture originale disponible pour sélection dans un avoir.
/// </summary>
public partial class CreditNoteLineSelection : ObservableObject
{
    public InvoiceLine OriginalLine { get; set; } = null!;
    public decimal OriginalQuantity { get; set; }

    [ObservableProperty] private decimal _alreadyRefunded;
    [ObservableProperty] private decimal _maxQuantity;
    [ObservableProperty] private decimal _selectedQuantity;
    [ObservableProperty] private bool _isSelected;

    public string DisplayName => $"{OriginalLine.Code} — {OriginalLine.Name}";
    public string DisplayPrice => $"{OriginalLine.UnitPriceHT:N2} HT";
    public string DisplayQuantity => $"Orig: {OriginalQuantity:G} | Remb: {AlreadyRefunded:G} | Dispo: {MaxQuantity:G}";
    public string TaxGroupLabel => $"{(char)('A' + (int)OriginalLine.TaxGroup)} ({OriginalLine.TaxRate}%)";
}

/// <summary>
/// Résumé d'une facture d'acompte dans un groupe d'avances.
/// 🆕 Date est DateTimeOffset pour rester aligné sur Invoice.CreatedAt.
/// </summary>
public class AdvanceInvoiceSummary
{
    public string InvoiceNumber { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public decimal Amount { get; set; }
    public string CodeDEFDGI { get; set; } = "";
    public string DateDisplay => Date.LocalDateTime.ToString("dd/MM/yyyy");
}

/// <summary>
/// Facture en attente affichée dans la liste d'annulation.
/// </summary>
public class PendingInvoiceItem
{
    public string Uid { get; set; } = "";
    public string DateDisplay { get; set; } = "—";
}