using SFE.Domain.Common;
using SFE.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace SFE.Domain.Entities;

public class Product : SyncableEntity
{

    // Id, CompanyId, SyncId, Version, CreatedAtUtc, UpdatedAtUtc,
    // DeletedAtUtc, OriginPointOfSaleSyncId — all inherited.
   // public int Id { get; set; }

    // === Identification ===
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // === Fiscalité ===
    public ItemType ItemType { get; set; } = ItemType.BIE;
    public TaxGroup TaxGroup { get; set; } = TaxGroup.B;

    // ── Taxe spécifique — TYPÉE (remplace string TaxSpecificValue) ──
    /// <summary>Type : pourcentage du HT ou montant fixe par unité</summary>
    public SpecificTaxType SpecificTaxType { get; set; } = SpecificTaxType.None;

    /// <summary>
    /// Valeur numérique :
    ///   - Si Percentage  : le taux (ex : 10 pour 10%)
    ///   - Si FixedPerUnit: le montant par unité (ex : 230 CDF)
    /// </summary>
    public decimal SpecificTaxValue { get; set; }

    /// <summary>Comment la taxe spécifique est appliquée sur la facture.</summary>
    public TaxSpecificMode TaxSpecificMode { get; set; } = TaxSpecificMode.PerArticle;

    // === Prix ===
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = "pce";

    // === Stock ===
    public decimal StockQuantity { get; set; }
    public decimal MinStockLevel { get; set; }
    public bool TrackStock { get; set; } = false;

    // === Catégorie ===
    public int? CategoryId { get; set; }
    public Ulid? CategorySyncId { get; set; }     // portable FK (set when Category is assigned)
    public ProductCategory? Category { get; set; }

    // === État ===
    public bool IsActive { get; set; } = true;
    public bool IsFavorite { get; set; } = false;
  //  public DateTimeOffset CreatedAt { get; set; }
  //  public DateTimeOffset? UpdatedAt { get; set; }

    // Multi-devise : stockés à la création/modification
    public decimal UnitPriceHtCdf { get; set; }
    public decimal UnitPriceTtcCdf { get; set; }
    public decimal UnitPriceHtUsd { get; set; }
    public decimal UnitPriceTtcUsd { get; set; }

    // Remise par défaut (appliquée auto en caisse/facture)
    public DiscountType DefaultDiscountType { get; set; } = DiscountType.None;
    public decimal DefaultDiscountValue { get; set; }

    public List<PosStock> PosStocks { get; set; } = new();

    // ── Propriétés calculées pour affichage ──

    [NotMapped]
    public bool HasSpecificTax =>
        SpecificTaxType != SpecificTaxType.None && SpecificTaxValue > 0;

    [NotMapped]
    public string SpecificTaxDisplay => SpecificTaxType switch
    {
        SpecificTaxType.Percentage => $"{SpecificTaxValue:G}%",
        SpecificTaxType.FixedPerUnit => $"{SpecificTaxValue:N0}/u",
        _ => "—"
    };

    [NotMapped]
    public string TaxSpecificModeShort => TaxSpecificMode == TaxSpecificMode.PerArticle
        ? "par art." : "sur total";

    [NotMapped]
    public bool HasDefaultDiscount =>
        DefaultDiscountType != DiscountType.None && DefaultDiscountValue > 0;

    [NotMapped]
    public string DefaultDiscountDisplay => DefaultDiscountType switch
    {
        DiscountType.Percentage => $"-{DefaultDiscountValue:G}%",
        DiscountType.FixedAmount => $"-{DefaultDiscountValue:N0}",
        _ => ""
    };

    [NotMapped]
    public string PriceSummary => $"{UnitPriceHtCdf:N0} → {UnitPriceTtcCdf:N0} CDF";

    // === Helpers ===
    public string TaxGroupLabel => $"{(char)('A' + (int)TaxGroup)} – {TaxGroup.GetGroupLabel()}";
    public string DisplayText => string.IsNullOrEmpty(Code) ? Name : $"{Code} — {Name}";
}