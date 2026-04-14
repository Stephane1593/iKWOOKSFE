using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Application.Services;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

/// <summary>
/// Représente un article dans le panier POS.
/// Stocke les deux prix (HT + TTC) et gère la remise.
/// </summary>
public partial class CartItemViewModel : ObservableObject
{
    // ══════ IDENTITÉ ══════
    [ObservableProperty] private int _productId;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ItemType _itemType = ItemType.BIE;
    [ObservableProperty] private TaxGroup _taxGroup = TaxGroup.B;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private string _unit = "pce";

    // ══════ DUAL PRICE ══════
    [ObservableProperty] private decimal _unitPriceHT;
    [ObservableProperty] private decimal _unitPriceTTC;

    [ObservableProperty] private decimal _quantity = 1;

    // ══════ REMISE ══════
    [ObservableProperty] private DiscountType _discountType = DiscountType.None;
    [ObservableProperty] private decimal _discountValue;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _amountHTBeforeDiscount;

    // ══════ TAXE SPÉCIFIQUE ══════
    [ObservableProperty] private bool _hasSpecificTax;
    [ObservableProperty] private string _specificTaxName = "";
    [ObservableProperty] private decimal _specificTaxRate;
    [ObservableProperty] private string _taxSpecificValue = "";
    [ObservableProperty] private TaxApplicationMode _taxApplicationMode = TaxApplicationMode.PerArticle;
    [ObservableProperty] private decimal _taxSpecificAmount;

    // ══════ MONTANTS CALCULÉS ══════
    [ObservableProperty] private decimal _amountHT;
    [ObservableProperty] private decimal _amountTVA;
    [ObservableProperty] private decimal _amountTTC;

    // ══════ STOCK ══════
    [ObservableProperty] private decimal _stockQuantity;
    [ObservableProperty] private bool _trackStock;

    // ══════ AFFICHAGE ══════

    /// <summary>Mode en cours — mis à jour par Recalculate.</summary>
    private PriceMode _displayMode = PriceMode.TTC;

    /// <summary>Prix unitaire affiché selon le mode actif.</summary>
    public decimal DisplayUnitPrice => _displayMode == PriceMode.TTC ? UnitPriceTTC : UnitPriceHT;

    public string QuantityDisplay => $"{Quantity:G} × {DisplayUnitPrice:N0}";
    public string TaxGroupLabel => $"{(char)('A' + (int)TaxGroup)}";

    public bool HasDiscount => DiscountType != DiscountType.None && DiscountValue > 0;

    public string DiscountDisplay => DiscountType switch
    {
        DiscountType.Percentage => $"-{DiscountValue:G}%",
        DiscountType.FixedAmount => $"-{DiscountAmount:N0}",
        _ => ""
    };

    // ══════ CALCUL ══════

    /// <summary>
    /// Recalcule tous les montants de la ligne via TaxCalculator.CalculateLineFull.
    /// </summary>
    public void Recalculate(PriceMode mode, bool discountBeforeTax = true)
    {
        _displayMode = mode;
        TaxRate = TaxCalculator.GetDefaultRate(TaxGroup);

        var input = new LineCalculationInput
        {
            UnitPriceHT = UnitPriceHT,
            UnitPriceTTC = UnitPriceTTC,
            Quantity = Quantity,
            TaxGroup = TaxGroup,
            TaxRate = TaxRate,
            PriceMode = mode,
            DiscountType = DiscountType,
            DiscountValue = DiscountValue,
            DiscountBeforeTax = discountBeforeTax,
            HasSpecificTax = HasSpecificTax,
            SpecificTaxRate = SpecificTaxRate,
            TaxSpecificValue = TaxSpecificValue,
            TaxApplicationMode = TaxApplicationMode
        };

        var result = TaxCalculator.CalculateLineFull(input);

        AmountHTBeforeDiscount = result.AmountHTBeforeDiscount;
        DiscountAmount = result.DiscountAmount;
        AmountHT = result.AmountHT;
        AmountTVA = result.AmountTVA;
        TaxSpecificAmount = result.TaxSpecificAmount;
        AmountTTC = result.AmountTTC;

        OnPropertyChanged(nameof(DisplayUnitPrice));
        OnPropertyChanged(nameof(QuantityDisplay));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(DiscountDisplay));
    }

    partial void OnQuantityChanged(decimal value) { }
}