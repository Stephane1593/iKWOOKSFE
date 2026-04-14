using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int LineNumber { get; set; }

    // ══════════════════════════════════════════════
    // ARTICLE — Snapshot au moment de l'ajout
    // ══════════════════════════════════════════════
    public int? ArticleId { get; set; }           // FK optionnel pour traçabilité
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemType ItemType { get; set; } = ItemType.BIE;
    public TaxGroup TaxGroup { get; set; } = TaxGroup.B;
    public decimal TaxRate { get; set; }          // Taux TVA du groupe (ex: 16.00)

    // ══════════════════════════════════════════════
    // PRIX UNITAIRE — Les deux toujours remplis
    // (calculés depuis Article selon devise facture)
    // ══════════════════════════════════════════════
    public decimal UnitPriceHT { get; set; }      // Prix unitaire Hors Taxes
    public decimal UnitPriceTTC { get; set; }     // Prix unitaire TTC (HT + TVA uniquement, sans T.S.)
    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal PriceModification { get; set; }

    public string Unit { get; set; } = "pce";

    // ══════════════════════════════════════════════
    // REMISE — Vente/facturation uniquement
    // PAS une propriété de l'article !
    // ══════════════════════════════════════════════
    public DiscountType DiscountType { get; set; } = DiscountType.None;
    public decimal DiscountValue { get; set; }    // 10 pour 10%, ou 500 pour montant fixe
    public decimal DiscountAmount { get; set; }   // Montant remise calculé (en devise facture)

    // ══════════════════════════════════════════════
    // TAXE SPÉCIFIQUE (≠ TVA) — Snapshot depuis Article
    // ══════════════════════════════════════════════
    public bool HasSpecificTax { get; set; }
    public string SpecificTaxName { get; set; } = string.Empty;     // ex: "Taxe Tourisme"
    public decimal SpecificTaxRate { get; set; }                     // ex: 10 pour 10%
    public TaxApplicationMode TaxApplicationMode { get; set; } = TaxApplicationMode.PerArticle;

    // Champ protocole MCF (string: "230" ou "10%")
    public string TaxSpecificValue { get; set; } = string.Empty;

    // Montant T.S. calculé pour cette ligne
    // = montant réel si PerArticle
    // = 0 si OnTotal (reporté et calculé au niveau facture sur le sous-total groupé)
    public decimal TaxSpecificAmount { get; set; }
    public SpecificTaxType SpecificTaxType { get; set; } = SpecificTaxType.None;
    public decimal SpecificTaxValue { get; set; }
 

    // ══════════════════════════════════════════════
    // MONTANTS CALCULÉS — Chaîne complète
    // Ordre par défaut: HT → Remise → HT remisé → TVA + T.S. → TTC
    // ══════════════════════════════════════════════
    public decimal AmountHTBeforeDiscount { get; set; }  // Qty × UnitPriceHT
    public decimal AmountHT { get; set; }                // HT après remise
    public decimal AmountTVA { get; set; }               // Montant TVA
    public decimal AmountTTC { get; set; }               // Total final (HT + TVA + T.S. PerArticle)

    // ══════════════════════════════════════════════
    // RELATION
    // ══════════════════════════════════════════════
    public Invoice Invoice { get; set; } = null!;

    // Dans InvoiceLine, ajoutez :
    public int? ProductId { get; set; }  // 🆕 FK vers Product (pour décrément stock) 
}