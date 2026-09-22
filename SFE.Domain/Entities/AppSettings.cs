using SFE.Domain.Enums;
using System;

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
        public DateTimeOffset ExchangeRateUpdatedAt { get; set; }

        // --- Devise & mode de prix par défaut ---
        public Currency DefaultCurrency { get; set; } = Currency.CDF;
        public PriceMode DefaultPriceMode { get; set; } = PriceMode.TTC;  // 🆕

        // --- Ordre de calcul ---
        public bool DiscountBeforeTax { get; set; } = true;

        // --- Informations entreprise ---
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyNIF { get; set; } = string.Empty;
        public string CompanyRCCM { get; set; } = string.Empty;
        public string CompanyIdNat { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;

        // ─── PARAMÈTRES DE FIDÉLITÉ (NEW) ───
        public bool LoyaltyEnabled { get; set; } = false;
        public decimal LoyaltyEarnRate { get; set; } = 1000m; // ex: 1 point gagné pour 1000 CDF dépensés
        public decimal LoyaltyRedeemRate { get; set; } = 10m; // ex: 1 point = 10 CDF de remise
        public int LoyaltyMinRedeemPoints { get; set; } = 100; // Minimum de points requis pour les utiliser

        public DateTimeOffset UpdatedAt { get; set; }
    }
}