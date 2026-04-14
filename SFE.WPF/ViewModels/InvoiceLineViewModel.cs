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
        DiscountType.Percentage => $"-{DiscountValue:G}%",
        DiscountType.FixedAmount => $"-{DiscountAmount:N0}",
        _ => ""
    };

    public string SpecificTaxDisplay => SpecificTaxType switch
    {
        SpecificTaxType.Percentage => $"TS {SpecificTaxValue:G}%",
        SpecificTaxType.FixedPerUnit => $"TS {SpecificTaxValue:N0}/u",
        _ => ""
    };

    public string TaxGroupDisplay =>
        $"{(char)('A' + (int)TaxGroup)} ({TaxRate}%)";

    public string LineAmountDisplay =>
        HasDiscount
            ? $"{AmountHTBeforeDiscount:N0} → {AmountHT:N0}"
            : $"{AmountHT:N0}";
}