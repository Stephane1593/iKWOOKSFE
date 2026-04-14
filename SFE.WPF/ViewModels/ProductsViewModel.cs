// File: SFE.WPF/ViewModels/ProductsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Events;
using SFE.Application.Helpers;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class ProductsViewModel : BaseViewModel, IActivatable
{
    private readonly ProductService _productService;
    private readonly SettingsService _settingsService;

    // ══════════════════════════════════════════════
    //  LISTE
    // ══════════════════════════════════════════════
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<ProductCategory> Categories { get; } = new();
    private bool _isFirstActivation = true;   // 

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
    [ObservableProperty] private string _activeField = "HT_CDF";

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

    [ObservableProperty] private string _editPriceInput = "";
    [ObservableProperty] private string _editPriceLabel = "Prix unitaire HT (CDF) *";
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

    // ══════════════════════════════════════════════
    //  CONSTRUCTOR
    // ══════════════════════════════════════════════
    public ProductsViewModel(ProductService productService, SettingsService settingsService)
    {
        _productService = productService;
        _settingsService = settingsService;
        PageTitle = "Catalogue Produits";

        // ── EVENT SUBSCRIPTIONS ──
        // Reload products when stock changes (Product.StockQuantity is updated
        // by StockService.UpdateProductGlobalStockAsync)
        Subscribe(OnStockOrProductChangedAsync,
            AppEvent.StockUpdated,
            AppEvent.ProductCreated,
            AppEvent.ProductUpdated,
            AppEvent.ProductDeleted);

        _ = InitializeAsync();
    }

    private async Task OnStockOrProductChangedAsync()
    {
        // Only reload the list if we're NOT currently editing
        // (avoids losing unsaved form data due to background refresh)
        if (!IsEditing)
        {
            await LoadProductsAsync();
        }
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
            UpdatePriceLabel();

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
    //  PRICE LABEL + ACTIVE FIELD
    // ══════════════════════════════════════════════
    private void UpdatePriceLabel()
    {
        ActiveField = (IsHtMode, ActiveCurrency) switch
        {
            (true, "CDF") => "HT_CDF",
            (false, "CDF") => "TTC_CDF",
            (true, "USD") => "HT_USD",
            (false, "USD") => "TTC_USD",
            _ => "HT_CDF"
        };

        EditPriceLabel = ActiveField switch
        {
            "HT_CDF" => "Prix unitaire HT (CDF) *",
            "TTC_CDF" => "Prix unitaire TTC (CDF) *",
            "HT_USD" => "Prix unitaire HT (USD) *",
            "TTC_USD" => "Prix unitaire TTC (USD) *",
            _ => "Prix unitaire *"
        };
    }

    // ══════════════════════════════════════════════
    //  MULTI-CURRENCY AUTO-CALCULATION
    // ══════════════════════════════════════════════
    partial void OnEditPriceInputChanged(string value) => RecalculatePrices();

    partial void OnEditTaxGroupChanged(TaxGroup value)
    {
        UpdateTaxRateDisplay();
        RecalculatePrices();
    }

    partial void OnEditSpecificTaxTypeChanged(SpecificTaxType value)
    {
        ShowSpecificTaxFields = value != SpecificTaxType.None;
        RecalculatePrices();
    }

    partial void OnEditSpecificTaxValueChanged(string value) => RecalculatePrices();

    private void UpdateTaxRateDisplay()
    {
        var rate = TaxCalculator.GetDefaultRate(EditTaxGroup);
        ActiveTaxRateDisplay = $"TVA {EditTaxGroup} — {rate}%  ({TaxCalculator.GetGroupLabel(EditTaxGroup)})";
    }

    private void RecalculatePrices()
    {
        if (!DecimalParsingHelper.TryParseFlexible(EditPriceInput, out var input) || input < 0)
        {
            HasPriceCalculation = false;
            CalcHtCdf = CalcTtcCdf = CalcHtUsd = CalcTtcUsd = 0;
            return;
        }

        decimal tvaRate = TaxCalculator.GetDefaultRate(EditTaxGroup);
        decimal xRate = ExchangeRate > 0 ? ExchangeRate : 1m;

        decimal tsValue = 0m;
        if (EditSpecificTaxType != SpecificTaxType.None)
            DecimalParsingHelper.TryParseFlexible(EditSpecificTaxValue, out tsValue);

        decimal htCdf, ttcCdf;

        switch (ActiveField)
        {
            case "HT_CDF":
                htCdf = input;
                (_, ttcCdf) = TaxCalculator.EnsureDualPrices(
                    htCdf, PriceMode.HT, tvaRate,
                    EditSpecificTaxType, tsValue);
                break;

            case "TTC_CDF":
                ttcCdf = input;
                (htCdf, _) = TaxCalculator.EnsureDualPrices(
                    ttcCdf, PriceMode.TTC, tvaRate,
                    EditSpecificTaxType, tsValue);
                break;

            case "HT_USD":
                htCdf = input * xRate;
                (_, ttcCdf) = TaxCalculator.EnsureDualPrices(
                    htCdf, PriceMode.HT, tvaRate,
                    EditSpecificTaxType, tsValue);
                break;

            case "TTC_USD":
                ttcCdf = input * xRate;
                (htCdf, _) = TaxCalculator.EnsureDualPrices(
                    ttcCdf, PriceMode.TTC, tvaRate,
                    EditSpecificTaxType, tsValue);
                break;

            default:
                return;
        }

        htCdf = Math.Max(0, Math.Round(htCdf, 4));
        ttcCdf = Math.Max(0, Math.Round(ttcCdf, 4));

        CalcHtCdf = htCdf;
        CalcTtcCdf = ttcCdf;
        CalcHtUsd = xRate > 0 ? Math.Round(htCdf / xRate, 4) : 0;
        CalcTtcUsd = xRate > 0 ? Math.Round(ttcCdf / xRate, 4) : 0;
        HasPriceCalculation = true;
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
        EditCode = "";
        EditBarcode = "";
        EditName = "";
        EditDescription = "";
        EditItemType = ItemType.BIE;
        EditTaxGroup = TaxGroup.B;
        EditSpecificTaxType = SpecificTaxType.None;
        EditSpecificTaxValue = "";
        EditTaxSpecificMode = TaxSpecificMode.PerArticle;
        EditPriceInput = "";
        EditUnit = "pce";
        EditDefaultDiscountType = DiscountType.None;
        EditDefaultDiscountValue = "";
        EditCategory = null;
        EditStockQuantity = "0";
        EditMinStockLevel = "0";
        EditTrackStock = false;
        EditIsFavorite = false;
        EditIsActive = true;

        HasPriceCalculation = false;
        CalcHtCdf = CalcTtcCdf = CalcHtUsd = CalcTtcUsd = 0;

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

        EditPriceInput = ActiveField switch
        {
            "HT_CDF" => product.UnitPriceHtCdf.ToString("F2"),
            "TTC_CDF" => product.UnitPriceTtcCdf.ToString("F2"),
            "HT_USD" => product.UnitPriceHtUsd.ToString("F4"),
            "TTC_USD" => product.UnitPriceTtcUsd.ToString("F4"),
            _ => product.UnitPriceHtCdf.ToString("F2")
        };

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
        // NOTE: LoadProductsAsync will also be triggered by the
        // ProductCreated/ProductUpdated event, but the explicit call
        // gives immediate feedback.
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

    [RelayCommand]
    private async Task DeleteProduct(Product? product)
    {
        if (product == null) return;
        await _productService.DeleteAsync(product.Id);
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
    public async Task ActivateAsync()                                  // 🆕
    {
        // Skip first call — InitializeAsync already ran from constructor
        if (_isFirstActivation)
        {
            _isFirstActivation = false;
            return;
        }

        // Don't blow away unsaved form data
        if (IsEditing) return;

        IsBusy = true;
        try
        {
            // ── Refresh settings (price mode / currency / rate may have changed) ──
            var settings = await _settingsService.LoadSettingsAsync();
            IsHtMode = settings.DefaultPriceMode != PriceMode.TTC;
            ActiveCurrency = settings.DefaultCurrency.ToString();
            ExchangeRate = settings.CurrentExchangeRate > 0
                ? settings.CurrentExchangeRate : 2800m;
            UpdatePriceLabel();

            // ── Refresh categories ──
            var cats = await _productService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            // ── Refresh product list ──
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