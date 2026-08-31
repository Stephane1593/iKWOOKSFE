using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

/// <summary>
/// Ligne d'article dans le module Facturation.
/// </summary>
public partial class InvoiceLineViewModel : ObservableObject
{
    [ObservableProperty] private int _lineNumber;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private ItemType _itemType = ItemType.BIE;
    [ObservableProperty] private TaxGroup _taxGroup = TaxGroup.B;
    [ObservableProperty] private TaxGroupAType? _taxGroupAType;
    [ObservableProperty] private decimal _taxRate;

    // ══════ DUAL PRICE ══════
    [ObservableProperty] private decimal _unitPriceHT;
    [ObservableProperty] private decimal _unitPriceTTC;

    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private string _unit = "pce";

    // ══════ REMISE ══════
    [ObservableProperty] private DiscountType _discountType = DiscountType.None;
    [ObservableProperty] private decimal _discountValue;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _amountHTBeforeDiscount;

    // ══════ TAXE SPÉCIFIQUE — TYPÉE ══════
    [ObservableProperty] private string _specificTaxName = "";
    [ObservableProperty] private SpecificTaxType _specificTaxType = SpecificTaxType.None;
    [ObservableProperty] private decimal _specificTaxValue;
    [ObservableProperty] private TaxApplicationMode _taxApplicationMode = TaxApplicationMode.PerArticle;
    [ObservableProperty] private decimal _taxSpecificAmount;

    // ══════ MONTANTS CALCULÉS ══════
    [ObservableProperty] private decimal _amountHT;
    [ObservableProperty] private decimal _amountTVA;
    [ObservableProperty] private decimal _amountTTC;

    // ══════ PROPRIÉTÉS CALCULÉES AFFICHAGE ══════
    public bool HasSpecificTax =>
        SpecificTaxType != SpecificTaxType.None && SpecificTaxValue > 0;

    public bool HasDiscount => DiscountType != DiscountType.None && DiscountValue > 0;

    public string DiscountDisplay => DiscountType switch
    {
        DiscountType.Percentage => $"-{DiscountValue:0.##}%",
        DiscountType.FixedAmount => $"-{DiscountAmount:N2}",
        _ => ""
    };

    public string SpecificTaxDisplay => SpecificTaxType switch
    {
        SpecificTaxType.Percentage => $"TS {SpecificTaxValue:0.##}%",
        SpecificTaxType.FixedPerUnit => $"TS {SpecificTaxValue:N2}/u",
        _ => ""
    };

    public string TaxGroupDisplay =>
        $"{TaxGroup.DisplayCode(TaxGroupAType)} ({TaxRate}%)";

    public string LineAmountDisplay =>
        HasDiscount
            ? $"{AmountHTBeforeDiscount:N2} → {AmountHT:N2}"
            : $"{AmountHT:N2}";
}