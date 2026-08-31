using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Events;
using SFE.Application.Helpers;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Services;

namespace SFE.WPF.ViewModels;

public partial class ProductsViewModel : BaseViewModel, IActivatable
{
    private readonly ProductService _productService;
    private readonly SettingsService _settingsService;
    private readonly IAuthService _authService;

    // ══════════════════════════════════════════════
    //  LISTE
    // ══════════════════════════════════════════════
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<ProductCategory> Categories { get; } = new();
    private bool _isFirstActivation = true;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ProductCategory? _selectedCategoryFilter;
    [ObservableProperty] private int _productCount;
    [ObservableProperty] private Product? _selectedProduct;

    // ══════════════════════════════════════════════
    //  SETTINGS-DRIVEN
    // ══════════════════════════════════════════════
    [ObservableProperty] private bool _isHtMode;
    [ObservableProperty] private string _activeCurrency = "CDF";
    [ObservableProperty] private decimal _exchangeRate = 2800m;

    // ══════════════════════════════════════════════
    //  FORMULAIRE
    // ══════════════════════════════════════════════
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNewProduct;
    [ObservableProperty] private string _formTitle = "Nouveau produit";

    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _editCode = "";
    [ObservableProperty] private string _editBarcode = "";
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private ProductCategory? _editCategory;

    [ObservableProperty] private ItemType _editItemType = ItemType.BIE;
    [ObservableProperty] private TaxGroup _editTaxGroup = TaxGroup.B;

    [ObservableProperty] private SpecificTaxType _editSpecificTaxType = SpecificTaxType.None;
    [ObservableProperty] private string _editSpecificTaxValue = "";
    [ObservableProperty] private TaxSpecificMode _editTaxSpecificMode = TaxSpecificMode.PerArticle;
    [ObservableProperty] private bool _showSpecificTaxFields;

    // ── TWO PRICE INPUT FIELDS (HT ↔ TTC cross-computation) ──
    [ObservableProperty] private string _editPriceHtInput = "";
    [ObservableProperty] private string _editPriceTtcInput = "";
    private bool _isUpdatingPrices;   // prevents infinite HT↔TTC loop

    [ObservableProperty] private decimal _calcHtCdf;
    [ObservableProperty] private decimal _calcTtcCdf;
    [ObservableProperty] private decimal _calcHtUsd;
    [ObservableProperty] private decimal _calcTtcUsd;
    [ObservableProperty] private bool _hasPriceCalculation;
    [ObservableProperty] private string _editUnit = "pce";

    [ObservableProperty] private string _activeTaxRateDisplay = "";

    [ObservableProperty] private DiscountType _editDefaultDiscountType = DiscountType.None;
    [ObservableProperty] private string _editDefaultDiscountValue = "";
    [ObservableProperty] private bool _showDiscountValue;

    [ObservableProperty] private string _editStockQuantity = "0";
    [ObservableProperty] private string _editMinStockLevel = "0";
    [ObservableProperty] private bool _editTrackStock;

    [ObservableProperty] private bool _editIsFavorite;
    [ObservableProperty] private bool _editIsActive = true;

    // ══════════════════════════════════════════════
    //  STATUS
    // ══════════════════════════════════════════════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;

    // ══════════════════════════════════════════════
    //  ENUM SOURCES
    // ══════════════════════════════════════════════
    public ItemType[] ItemTypes { get; } = Enum.GetValues<ItemType>();
    public TaxGroup[] TaxGroups { get; } = Enum.GetValues<TaxGroup>();
    public SpecificTaxType[] SpecificTaxTypes { get; } = Enum.GetValues<SpecificTaxType>();
    public TaxSpecificMode[] TaxSpecificModes { get; } = Enum.GetValues<TaxSpecificMode>();

    public DiscountType[] DiscountTypes { get; } = Enum.GetValues<DiscountType>();
    public string[] CommonUnits { get; } =
    {
        "pce", "kg", "g", "L", "mL", "m", "m²", "m³",
        "h", "j", "lot", "crt", "bte", "btle", "sac"
    };

    // ── NEW ──
    [ObservableProperty] private TaxGroupAType _editTaxGroupAType = TaxGroupAType.Exonere;
    [ObservableProperty] private bool _showTaxGroupAVariant;

    public TaxGroupAType[] TaxGroupATypes { get; } = Enum.GetValues<TaxGroupAType>();

    // ══════════════════════════════════════════════
    //  CONSTRUCTOR
    // ══════════════════════════════════════════════
    public ProductsViewModel(ProductService productService, SettingsService settingsService, IAuthService authService)
    {
        _productService = productService;
        _settingsService = settingsService;
        _authService = authService;
        PageTitle = "Catalogue Produits";

        Subscribe(OnStockOrProductChangedAsync,
            AppEvent.StockUpdated,
            AppEvent.ProductCreated,
            AppEvent.ProductUpdated,
            AppEvent.ProductDeleted);

        Subscribe(OnCategoryChangedAsync,
            AppEvent.CategoryCreated,
            AppEvent.CategoryUpdated,
            AppEvent.CategoryDeleted);

        _ = InitializeAsync();
    }

    public bool CanDeleteProducts => _authService.HasPermission("authorize.deleteProduct");

    private async Task OnStockOrProductChangedAsync()
    {
        if (!IsEditing)
            await LoadProductsAsync();
    }

    private async Task OnCategoryChangedAsync()
    {
        var cats = await _productService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _productService.SeedSampleDataAsync();

            var settings = await _settingsService.LoadSettingsAsync();
            IsHtMode = settings.DefaultPriceMode != PriceMode.TTC;
            ActiveCurrency = settings.DefaultCurrency.ToString();
            ExchangeRate = settings.CurrentExchangeRate > 0
                ? settings.CurrentExchangeRate : 2800m;

            var cats = await _productService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur au chargement : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════
    //  PRICE CROSS-COMPUTATION (HT ↔ TTC)
    // ══════════════════════════════════════════════

    /// <summary>User typed in the HT field → compute TTC + refresh card.</summary>
    partial void OnEditPriceHtInputChanged(string value)
    {
        if (_isUpdatingPrices) return;
        ComputeFromHt();
    }

    /// <summary>User typed in the TTC field → compute HT + refresh card.</summary>
    partial void OnEditPriceTtcInputChanged(string value)
    {
        if (_isUpdatingPrices) return;
        ComputeFromTtc();
    }

    partial void OnEditTaxGroupChanged(TaxGroup value)
    {
        ShowTaxGroupAVariant = value == TaxGroup.A;
        if (value != TaxGroup.A)
            EditTaxGroupAType = TaxGroupAType.Exonere;   // reset when leaving A

        UpdateTaxRateDisplay();
        RecomputeFromBestInput();
    }

    partial void OnEditSpecificTaxTypeChanged(SpecificTaxType value)
    {
        ShowSpecificTaxFields = value != SpecificTaxType.None;
        RecomputeFromBestInput();
    }

    partial void OnEditSpecificTaxValueChanged(string value) => RecomputeFromBestInput();

    /// <summary>Re-derive prices from whichever field has a valid value (prefer HT).</summary>
    private void RecomputeFromBestInput()
    {
        if (DecimalParsingHelper.TryParseFlexible(EditPriceHtInput, out var ht) && ht > 0)
            ComputeFromHt();
        else if (DecimalParsingHelper.TryParseFlexible(EditPriceTtcInput, out var ttc) && ttc > 0)
            ComputeFromTtc();
    }

    private void UpdateTaxRateDisplay()
    {
        var rate = TaxCalculator.GetDefaultRate(EditTaxGroup);
        var code = EditTaxGroup.DisplayCode(
            EditTaxGroup == TaxGroup.A ? EditTaxGroupAType : null);
        ActiveTaxRateDisplay =
            $"TVA [{code}] — {rate}%  ({EditTaxGroup.GetGroupLabel(EditTaxGroupAType)})";
    }

    // Re-run the display line when the user flips Exonéré ↔ Hors champ
    partial void OnEditTaxGroupATypeChanged(TaxGroupAType value) => UpdateTaxRateDisplay();

    /// <summary>
    /// User entered HT (CDF) → derive TTC, fill TTC input field + 4-price card.
    /// </summary>
    private void ComputeFromHt()
    {
        if (!DecimalParsingHelper.TryParseFlexible(EditPriceHtInput, out var htCdf) || htCdf < 0)
        {
            ClearPrices();
            return;
        }

        _isUpdatingPrices = true;
        try
        {
            decimal tvaRate = TaxCalculator.GetDefaultRate(EditTaxGroup);
            decimal xRate = ExchangeRate > 0 ? ExchangeRate : 1m;
            decimal tsValue = 0m;
            if (EditSpecificTaxType != SpecificTaxType.None)
                DecimalParsingHelper.TryParseFlexible(EditSpecificTaxValue, out tsValue);

            var (_, ttcCdf) = TaxCalculator.EnsureDualPrices(
                htCdf, PriceMode.HT, tvaRate,
                EditSpecificTaxType, tsValue);

            htCdf = Math.Max(0, Math.Round(htCdf, 4));
            ttcCdf = Math.Max(0, Math.Round(ttcCdf, 2));

            // Auto-fill the TTC input field
            EditPriceTtcInput = ttcCdf.ToString("F2");

            RefreshPriceCard(htCdf, ttcCdf, xRate);
        }
        finally
        {
            _isUpdatingPrices = false;
        }
    }

    /// <summary>
    /// User entered TTC (CDF) → derive HT, fill HT input field + 4-price card.
    /// </summary>
    private void ComputeFromTtc()
    {
        if (!DecimalParsingHelper.TryParseFlexible(EditPriceTtcInput, out var ttcCdf) || ttcCdf < 0)
        {
            ClearPrices();
            return;
        }

        _isUpdatingPrices = true;
        try
        {
            decimal tvaRate = TaxCalculator.GetDefaultRate(EditTaxGroup);
            decimal xRate = ExchangeRate > 0 ? ExchangeRate : 1m;
            decimal tsValue = 0m;
            if (EditSpecificTaxType != SpecificTaxType.None)
                DecimalParsingHelper.TryParseFlexible(EditSpecificTaxValue, out tsValue);

            var (htCdf, _) = TaxCalculator.EnsureDualPrices(
                ttcCdf, PriceMode.TTC, tvaRate,
                EditSpecificTaxType, tsValue);

            htCdf = Math.Max(0, Math.Round(htCdf, 4));
            ttcCdf = Math.Max(0, Math.Round(ttcCdf, 2));

            // Auto-fill the HT input field
            EditPriceHtInput = htCdf.ToString("F2");

            RefreshPriceCard(htCdf, ttcCdf, xRate);
        }
        finally
        {
            _isUpdatingPrices = false;
        }
    }

    private void RefreshPriceCard(decimal htCdf, decimal ttcCdf, decimal xRate)
    {
        CalcHtCdf = htCdf;
        CalcTtcCdf = ttcCdf;
        CalcHtUsd = xRate > 0 ? Math.Round(htCdf / xRate, 4) : 0;
        CalcTtcUsd = xRate > 0 ? Math.Round(ttcCdf / xRate, 4) : 0;
        HasPriceCalculation = true;
    }

    private void ClearPrices()
    {
        HasPriceCalculation = false;
        CalcHtCdf = CalcTtcCdf = CalcHtUsd = CalcTtcUsd = 0;
    }

    [RelayCommand]
    private async Task PrintBarcode(Product? product)
    {
        if (product == null) return;

        try
        {
            // Default: print 1 thermal copy. You can change copies or choose non-thermal by updating parameters.
            // If you want to use a configured copy count or prefer full-label printing, adapt here.
            await Task.Run(() =>
            {
                // Print on UI thread - PrintDialog requires STA/UI thread.
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Use thermal layout by default (80mm). Pass copies if you want more.
                    BarcodePrinter.PrintProductBarcode(product, copies: 1, thermal: true);
                });
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur impression : {ex.Message}";
            ShowError = true;
        }
    }

    // ══════════════════════════════════════════════
    //  AUTO-GENERATE PRODUCT CODE
    // ══════════════════════════════════════════════
    partial void OnEditCategoryChanged(ProductCategory? value)
    {
        // Only auto-generate code for NEW products when a category is selected
        if (IsNewProduct && value != null)
            _ = GenerateCodeAsync(value.Id);
    }

    private async Task GenerateCodeAsync(int categoryId)
    {
        try
        {
            var code = await _productService.GenerateNextCodeAsync(categoryId);
            EditCode = code;
        }
        catch
        {
            // Silently fail — user can still type a code manually
        }
    }

    // ══════════════════════════════════════════════
    //  DISCOUNT VISIBILITY
    // ══════════════════════════════════════════════
    partial void OnEditDefaultDiscountTypeChanged(DiscountType value)
    {
        ShowDiscountValue = value != DiscountType.None;
    }

    // ══════════════════════════════════════════════
    //  SEARCH + FILTER
    // ══════════════════════════════════════════════
    private async Task LoadProductsAsync()
    {
        List<Product> results;

        if (!string.IsNullOrWhiteSpace(SearchText))
            results = await _productService.SearchAsync(SearchText, 100);
        else
            results = await _productService.GetAllActiveAsync();

        if (SelectedCategoryFilter != null)
            results = results.Where(p => p.CategoryId == SelectedCategoryFilter.Id).ToList();

        Products.Clear();
        foreach (var p in results) Products.Add(p);
        ProductCount = Products.Count;
    }

    partial void OnSearchTextChanged(string value) => _ = LoadProductsAsync();
    partial void OnSelectedCategoryFilterChanged(ProductCategory? value) => _ = LoadProductsAsync();

    [RelayCommand]
    private void ClearCategoryFilter() => SelectedCategoryFilter = null;

    // ══════════════════════════════════════════════
    //  CRUD — NEW
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void StartNewProduct()
    {
        ShowError = false;
        ShowSuccess = false;
        IsNewProduct = true;
        IsEditing = true;
        FormTitle = "Nouveau produit";

        EditId = 0;
        EditCode = "";              // auto-generated when category is selected
        EditBarcode = "";
        EditName = "";
        EditDescription = "";
        EditItemType = ItemType.BIE;
        EditTaxGroup = TaxGroup.B;
        EditTaxGroupAType = TaxGroupAType.Exonere;    // NEW
        ShowTaxGroupAVariant = false;                  // NEW
        EditSpecificTaxType = SpecificTaxType.None;
        EditSpecificTaxValue = "";
        EditTaxSpecificMode = TaxSpecificMode.PerArticle;

        _isUpdatingPrices = true;   // prevent cross-compute during reset
        EditPriceHtInput = "";
        EditPriceTtcInput = "";
        _isUpdatingPrices = false;

        EditUnit = "pce";
        EditDefaultDiscountType = DiscountType.None;
        EditDefaultDiscountValue = "";
        EditCategory = null;
        EditStockQuantity = "0";
        EditMinStockLevel = "0";
        EditTrackStock = false;
        EditIsFavorite = false;
        EditIsActive = true;

        ClearPrices();
        UpdateTaxRateDisplay();
    }

    // ══════════════════════════════════════════════
    //  CRUD — EDIT
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void StartEditProduct(Product? product)
    {
        if (product == null) return;

        ShowError = false;
        ShowSuccess = false;
        IsNewProduct = false;
        IsEditing = true;
        FormTitle = $"Modifier « {product.Name} »";
        SelectedProduct = product;

        EditId = product.Id;
        EditCode = product.Code;
        EditBarcode = product.Barcode;
        EditName = product.Name;
        EditDescription = product.Description;
        EditItemType = product.ItemType;
        EditTaxGroup = product.TaxGroup;
        EditTaxGroupAType = product.TaxGroupAType ?? TaxGroupAType.Exonere;   // NEW
        ShowTaxGroupAVariant = product.TaxGroup == TaxGroup.A;
        EditSpecificTaxType = product.SpecificTaxType;
        EditSpecificTaxValue = product.SpecificTaxValue > 0
            ? product.SpecificTaxValue.ToString("G") : "";
        EditTaxSpecificMode = product.TaxSpecificMode;
        EditUnit = product.Unit;
        EditDefaultDiscountType = product.DefaultDiscountType;
        EditDefaultDiscountValue = product.DefaultDiscountValue > 0
            ? product.DefaultDiscountValue.ToString("G") : "";
        EditCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
        EditStockQuantity = product.StockQuantity.ToString("F0");
        EditMinStockLevel = product.MinStockLevel.ToString("F0");
        EditTrackStock = product.TrackStock;
        EditIsFavorite = product.IsFavorite;
        EditIsActive = product.IsActive;



        // ── Populate BOTH price fields without triggering cross-compute ──
        _isUpdatingPrices = true;
        try
        {
            EditPriceHtInput = product.UnitPriceHtCdf.ToString("F2");
            EditPriceTtcInput = product.UnitPriceTtcCdf.ToString("F2");

            decimal xRate = ExchangeRate > 0 ? ExchangeRate : 1m;
            CalcHtCdf = product.UnitPriceHtCdf;
            CalcTtcCdf = product.UnitPriceTtcCdf;
            CalcHtUsd = xRate > 0 ? Math.Round(product.UnitPriceHtCdf / xRate, 4) : 0;
            CalcTtcUsd = xRate > 0 ? Math.Round(product.UnitPriceTtcCdf / xRate, 4) : 0;
            HasPriceCalculation = true;
        }
        finally
        {
            _isUpdatingPrices = false;
        }

        UpdateTaxRateDisplay();
    }

    // ══════════════════════════════════════════════
    //  CRUD — SAVE
    // ══════════════════════════════════════════════
    [RelayCommand]
    private async Task SaveProduct()
    {
        ShowError = false;
        ShowSuccess = false;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Le nom du produit est obligatoire.";
            ShowError = true;
            return;
        }

        if (!HasPriceCalculation || CalcHtCdf <= 0)
        {
            StatusMessage = "Saisissez un prix unitaire valide.";
            ShowError = true;
            return;
        }

        decimal specificTaxVal = 0m;
        if (EditSpecificTaxType != SpecificTaxType.None)
        {
            if (!DecimalParsingHelper.TryParseFlexible(EditSpecificTaxValue, out specificTaxVal)
                || specificTaxVal <= 0)
            {
                StatusMessage = "La valeur de la taxe spécifique doit être un nombre positif.";
                ShowError = true;
                return;
            }

            if (EditSpecificTaxType == SpecificTaxType.Percentage && specificTaxVal > 100)
            {
                StatusMessage = "Le pourcentage de la taxe spécifique ne peut pas dépasser 100%.";
                ShowError = true;
                return;
            }
        }

        DecimalParsingHelper.TryParseFlexible(EditStockQuantity, out var stock);
        DecimalParsingHelper.TryParseFlexible(EditMinStockLevel, out var minStock);

        decimal discountVal = 0;
        if (EditDefaultDiscountType != DiscountType.None)
        {
            if (!DecimalParsingHelper.TryParseFlexible(EditDefaultDiscountValue, out discountVal)
                || discountVal <= 0)
            {
                StatusMessage = "La valeur de remise doit être un nombre positif.";
                ShowError = true;
                return;
            }

            if (EditDefaultDiscountType == DiscountType.Percentage && discountVal > 100)
            {
                StatusMessage = "Le pourcentage de remise ne peut pas dépasser 100%.";
                ShowError = true;
                return;
            }
        }

        // ── Auto-generate code if still empty on save ──
        if (IsNewProduct && string.IsNullOrWhiteSpace(EditCode) && EditCategory != null)
        {
            try { EditCode = await _productService.GenerateNextCodeAsync(EditCategory.Id); }
            catch { /* service validation will catch duplicates */ }
        }

        if (IsNewProduct)
        {
            var product = new Product
            {
                Code = EditCode.Trim(),
                Barcode = EditBarcode.Trim(),
                Name = EditName.Trim(),
                Description = EditDescription.Trim(),
                ItemType = EditItemType,
                TaxGroup = EditTaxGroup,
                TaxGroupAType = EditTaxGroup == TaxGroup.A ? EditTaxGroupAType : (TaxGroupAType?)null,
                SpecificTaxType = EditSpecificTaxType,
                SpecificTaxValue = specificTaxVal,
                TaxSpecificMode = EditTaxSpecificMode,
                UnitPriceHtCdf = CalcHtCdf,
                UnitPriceTtcCdf = CalcTtcCdf,
                UnitPriceHtUsd = CalcHtUsd,
                UnitPriceTtcUsd = CalcTtcUsd,
                UnitPrice = CalcHtCdf,
                Unit = EditUnit.Trim(),
                DefaultDiscountType = EditDefaultDiscountType,
                DefaultDiscountValue = discountVal,
                CategoryId = EditCategory?.Id,
                StockQuantity = stock,
                MinStockLevel = minStock,
                TrackStock = EditTrackStock,
                IsFavorite = EditIsFavorite,
                IsActive = EditIsActive
            };
            var result = await _productService.CreateAsync(product);
            if (!result.Success) { StatusMessage = result.ErrorMessage; ShowError = true; return; }
            StatusMessage = $"✓ Produit « {product.Name} » créé avec succès.";
        }
        else
        {
            var product = await _productService.GetByIdAsync(EditId);
            if (product == null) { StatusMessage = "Produit introuvable."; ShowError = true; return; }

            product.Code = EditCode.Trim();
            product.Barcode = EditBarcode.Trim();
            product.Name = EditName.Trim();
            product.Description = EditDescription.Trim();
            product.ItemType = EditItemType;
            product.TaxGroup = EditTaxGroup;
            product.TaxGroupAType = EditTaxGroup == TaxGroup.A ? EditTaxGroupAType : null;
            product.SpecificTaxType = EditSpecificTaxType;
            product.SpecificTaxValue = specificTaxVal;
            product.TaxSpecificMode = EditTaxSpecificMode;
            product.UnitPriceHtCdf = CalcHtCdf;
            product.UnitPriceTtcCdf = CalcTtcCdf;
            product.UnitPriceHtUsd = CalcHtUsd;
            product.UnitPriceTtcUsd = CalcTtcUsd;
            product.UnitPrice = CalcHtCdf;
            product.Unit = EditUnit.Trim();
            product.DefaultDiscountType = EditDefaultDiscountType;
            product.DefaultDiscountValue = discountVal;
            product.CategoryId = EditCategory?.Id;
            product.StockQuantity = stock;
            product.MinStockLevel = minStock;
            product.TrackStock = EditTrackStock;
            product.IsFavorite = EditIsFavorite;
            product.IsActive = EditIsActive;

            var result = await _productService.UpdateAsync(product);
            if (!result.Success) { StatusMessage = result.ErrorMessage; ShowError = true; return; }
            StatusMessage = $"✓ Produit « {product.Name} » mis à jour.";
        }

        ShowSuccess = true;
        IsEditing = false;
        await LoadProductsAsync();
    }

    // ══════════════════════════════════════════════
    //  CRUD — CANCEL / DELETE / TOGGLE
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ShowError = false;
        ShowSuccess = false;
    }

    // ── guarded command
    [RelayCommand]
    private async Task DeleteProduct(Product? product)
    {
        if (product == null) return;

        if (!_authService.HasPermission("authorize.deleteProduct"))
        {
            StatusMessage = "Vous n'avez pas l'autorisation de supprimer un produit. " +
                            "Contactez un administrateur.";
            ShowError = true;
            ShowSuccess = false;
            return;
        }

        var result = await _productService.DeleteAsync(product.Id);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage;
            ShowError = true;
            ShowSuccess = false;
            return;
        }

        StatusMessage = $"✓ Produit « {product.Name} » supprimé.";
        ShowSuccess = true;
        ShowError = false;
        if (IsEditing && EditId == product.Id) IsEditing = false;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task ToggleFavorite(Product? product)
    {
        if (product == null) return;
        product.IsFavorite = !product.IsFavorite;
        await _productService.UpdateAsync(product);
        await LoadProductsAsync();
    }

    // ══════════════════════════════════════════════
    //  IActivatable
    // ══════════════════════════════════════════════
    public async Task ActivateAsync()
    {
        if (_isFirstActivation)
        {
            _isFirstActivation = false;
            return;
        }

        if (IsEditing) return;

        IsBusy = true;
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            IsHtMode = settings.DefaultPriceMode != PriceMode.TTC;
            ActiveCurrency = settings.DefaultCurrency.ToString();
            ExchangeRate = settings.CurrentExchangeRate > 0
                ? settings.CurrentExchangeRate : 2800m;

            var cats = await _productService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur au rechargement : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }
}