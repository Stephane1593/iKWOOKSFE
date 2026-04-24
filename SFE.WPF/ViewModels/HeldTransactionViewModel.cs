using System.Collections.ObjectModel;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

/// <summary>
/// Représente une transaction mise en attente dans le POS.
/// </summary>
public class HeldTransactionViewModel
{
    private static int _counter;

    public string Id { get; set; } = $"H{Interlocked.Increment(ref _counter):D3}";
    public string Label { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime HeldAt { get; set; }
    public decimal TotalTTC { get; set; }
    public int ItemCount { get; set; }
    public string OperatorName { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public InvoiceType InvoiceType { get; set; }
    public PaymentType PaymentType { get; set; }
    public string ReceivedAmount { get; set; } = "";
    public PriceMode PriceMode { get; set; }

    /// <summary>🆕 Préserve le mode remise actif au moment de la mise en attente.</summary>
    public bool DiscountBeforeTax { get; set; } = true;

    public ObservableCollection<CartItemSnapshot> Items { get; } = new();

    public string TimeDisplay => HeldAt.ToString("HH:mm");
    public string TotalDisplay => $"{TotalTTC:N0}";
}

/// <summary>
/// Snapshot complet d'un article du panier (pour restauration après attente).
/// </summary>
public class CartItemSnapshot
{
    public int ProductId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public ItemType ItemType { get; set; }
    public TaxGroup TaxGroup { get; set; }
    public decimal TaxRate { get; set; }
    public decimal UnitPriceHT { get; set; }
    public decimal UnitPriceTTC { get; set; }
    public string Unit { get; set; } = "pce";
    public decimal Quantity { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountHTBeforeDiscount { get; set; }

    // V6 : champs typés TS (remplacent HasSpecificTax, SpecificTaxRate, TaxSpecificValue string)
    public SpecificTaxType SpecificTaxType { get; set; } = SpecificTaxType.None;
    public decimal SpecificTaxValue { get; set; }
    public string SpecificTaxName { get; set; } = "";
    public TaxApplicationMode TaxApplicationMode { get; set; } = TaxApplicationMode.PerArticle;
    public decimal TaxSpecificAmount { get; set; }

    public decimal AmountHT { get; set; }
    public decimal AmountTVA { get; set; }
    public decimal AmountTTC { get; set; }
    public decimal StockQuantity { get; set; }
    public bool TrackStock { get; set; }
}