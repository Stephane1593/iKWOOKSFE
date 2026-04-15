using SFE.Domain.Enums;

namespace SFE.Domain.Entities
{
    public class AppSettings
    {
        public int Id { get; set; }

        // --- Taux de change ---
        public ExchangeRateMode ExchangeRateMode { get; set; } = ExchangeRateMode.Manual;
        public decimal CurrentExchangeRate { get; set; } = 2800m; // 1 USD = X CDF
        public decimal CurrentExchangeRateEUR { get; set; }   // ← NEW
        public decimal CurrentExchangeRateCNY { get; set; }   // ← NEW
        public DateTime ExchangeRateUpdatedAt { get; set; } = DateTime.Now;

        // --- Devise & mode de prix par défaut ---
        public Currency DefaultCurrency { get; set; } = Currency.CDF;
        public PriceMode DefaultPriceMode { get; set; } = PriceMode.TTC;  // 🆕

        // --- Ordre de calcul ---
        // true  = HT → Remise → HT remisé → Taxes → TTC (défaut)
        // false = HT → Taxes → Remise → TTC
        public bool DiscountBeforeTax { get; set; } = true;

        // --- Informations entreprise ---
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyNIF { get; set; } = string.Empty;
        public string CompanyRCCM { get; set; } = string.Empty;
        public string CompanyIdNat { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}