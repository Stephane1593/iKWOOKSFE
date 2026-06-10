using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Application.Services;
using SFE.Domain.Enums;
using CommunityToolkit.Mvvm.Messaging;
using SFE.WPF.Messages;

namespace SFE.WPF.ViewModels;

/// <summary>
/// Représente un article dans le panier POS.
/// V12: CalculateLineFull now applies TS on price directly (WinDev style).
/// </summary>
public partial class CartItemViewModel : ObservableObject
{
    // ══════ IDENTITÉ ══════
    [ObservableProperty] private int _productId;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ItemType _itemType = ItemType.BIE;
    [ObservableProperty] private TaxGroup _taxGroup = TaxGroup.B;
    [ObservableProperty] private TaxGroupAType? _taxGroupAType;   // 🆕
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

    // ══════ TAXE SPÉCIFIQUE — champs typés ══════
    [ObservableProperty] private SpecificTaxType _specificTaxType = SpecificTaxType.None;
    [ObservableProperty] private decimal _specificTaxValue;
    [ObservableProperty] private string _specificTaxName = "";
    [ObservableProperty] private TaxApplicationMode _taxApplicationMode = TaxApplicationMode.PerArticle;
    [ObservableProperty] private decimal _taxSpecificAmount;

    public bool HasSpecificTax =>
        SpecificTaxType != SpecificTaxType.None && SpecificTaxValue > 0;

    // ══════ MONTANTS CALCULÉS ══════
    [ObservableProperty] private decimal _amountHT;
    [ObservableProperty] private decimal _amountTVA;
    [ObservableProperty] private decimal _amountTTC;

    // ══════ STOCK ══════
    [ObservableProperty] private decimal _stockQuantity;
    [ObservableProperty] private bool _trackStock;

    // ── ADD in the private-fields region of the class ──────────
    // État mémorisé pour pouvoir recalculer la ligne depuis OnQuantityChanged
    // (édition directe via TextBox du panier) sans que PosViewModel ait à
    // repasser PriceMode/discountBeforeTax.
    private bool _lastDiscountBeforeTax = true;
    private bool _isInitialized;

    // ══════ AFFICHAGE ══════
    private PriceMode _displayMode = PriceMode.TTC;

    public decimal DisplayUnitPrice =>
        _displayMode == PriceMode.TTC ? UnitPriceTTC : UnitPriceHT;

    public string QuantityDisplay => $"{Quantity:0.###} × {DisplayUnitPrice:N2}";
    public string TaxGroupLabel => $"{(char)('A' + (int)TaxGroup)}";

    public bool HasDiscount =>
        DiscountType != DiscountType.None && DiscountValue > 0;

    public string DiscountDisplay => DiscountType switch
    {
        DiscountType.Percentage => $"-{DiscountValue:0.##}%",
        DiscountType.FixedAmount => $"-{DiscountAmount:N2}",
        _ => ""
    };

    public string SpecificTaxDisplay => SpecificTaxType switch
    {
        SpecificTaxType.Percentage => $"TS {SpecificTaxValue:G}%",
        SpecificTaxType.FixedPerUnit => $"TS {SpecificTaxValue:N2}/u",
        _ => ""
    };

    // ══════ PARTIAL CHANGE HANDLERS ══════

    partial void OnSpecificTaxTypeChanged(SpecificTaxType value)
    {
        OnPropertyChanged(nameof(HasSpecificTax));
        OnPropertyChanged(nameof(SpecificTaxDisplay));
    }

    partial void OnSpecificTaxValueChanged(decimal value)
    {
        OnPropertyChanged(nameof(HasSpecificTax));
        OnPropertyChanged(nameof(SpecificTaxDisplay));
    }

    partial void OnTaxGroupChanged(TaxGroup value)
    {
        OnPropertyChanged(nameof(TaxGroupLabel));
    }

    partial void OnQuantityChanged(decimal value)
    {
        // DGI-spec: la quantité est tolérée jusqu'à 3 décimales (ex. 3.587, 3.45).
        // On tronque l'éventuel surplus pour éviter des arrondis invisibles
        // qui provoqueraient des écarts de ±1 FC sur le total TTC.
        var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        if (rounded != value)
        {
            Quantity = rounded;   // réentrance — le 2ᵉ appel passera par le else
            return;
        }

        OnPropertyChanged(nameof(QuantityDisplay));

        // Pendant la construction (object-initializer `new CartItemViewModel { ... }`)
        // les prix unitaires ne sont pas encore affectés : on saute l'auto-recalcul
        // et on laisse l'appelant déclencher le premier Recalculate explicite.
        if (!_isInitialized) return;

        // Édition directe depuis l'UI (TextBox) — on recalcule la ligne avec
        // le dernier couple (PriceMode, discountBeforeTax) connu, puis on
        // notifie le PosViewModel pour qu'il rafraîchisse les totaux globaux.
        Recalculate(_displayMode, _lastDiscountBeforeTax);
        WeakReferenceMessenger.Default.Send(new CartLineRecalculatedMessage(this));
    }

    // ══════ CALCUL — V12: WinDev-aligned ══════

    /// <summary>
    /// Recalcule tous les montants via TaxCalculator.CalculateLineFull.
    /// V12: TS applied on price directly, R2 standard rounding.
    /// </summary>
    public void Recalculate(PriceMode mode, bool discountBeforeTax = true)
    {
        _displayMode = mode;
        _lastDiscountBeforeTax = discountBeforeTax;   // 🆕 mémorisé pour auto-recalc
        _isInitialized = true;                        // 🆕 débloque OnQuantityChanged
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
            SpecificTaxType = SpecificTaxType,
            SpecificTaxValue = SpecificTaxValue,
            TaxApplicationMode = TaxApplicationMode
        };

        var result = TaxCalculator.CalculateLineFull(input);

        AmountHTBeforeDiscount = result.AmountHTBeforeDiscount;
        DiscountAmount = result.DiscountAmount;
        TaxSpecificAmount = result.TaxSpecificAmount;
        AmountHT = result.AmountHT;
        AmountTVA = result.AmountTVA;
        AmountTTC = result.AmountTTC;

        OnPropertyChanged(nameof(DisplayUnitPrice));
        OnPropertyChanged(nameof(QuantityDisplay));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(HasSpecificTax));
        OnPropertyChanged(nameof(SpecificTaxDisplay));
    }
}