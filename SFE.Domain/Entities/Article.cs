using SFE.Domain.Enums;

namespace SFE.Domain.Entities
{
    public class Article
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Barcode { get; set; }

        // --- Tarification multi-devise (4 prix stockés) ---
        public decimal PriceHT_CDF { get; set; }
        public decimal PriceTTC_CDF { get; set; }
        public decimal PriceHT_USD { get; set; }
        public decimal PriceTTC_USD { get; set; }

        // Devise de référence (celle saisie par l'utilisateur)
        public Currency ReferenceCurrency { get; set; } = Currency.CDF;
        // Taux utilisé pour calculer l'autre devise
        public decimal ExchangeRateUsed { get; set; }

        // --- TVA standard (DGI) ---
        public TaxGroup TaxGroup { get; set; } = TaxGroup.A; // A = 16%

        // --- Taxe spécifique (≠ TVA) ---
        public bool HasSpecificTax { get; set; }
        public string? SpecificTaxName { get; set; }
        public decimal SpecificTaxRate { get; set; } // ex: 10 pour 10%
        public TaxApplicationMode TaxApplicationMode { get; set; } = TaxApplicationMode.PerArticle;

        // --- Stock basique ---
        public decimal? StockQuantity { get; set; }
        public string? Unit { get; set; } // pièce, kg, litre...

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        // Navigation
        public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();
    }
}