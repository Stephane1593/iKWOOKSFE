namespace SFE.WPF.Models;

public class CashSessionInfo
{
    public DateTimeOffset OpenedAt { get; set; }
    public string OperatorName { get; set; } = "";

    // Point of Sale
    public int PointOfSaleId { get; set; }
    public string PointOfSaleName { get; set; } = "";
    public string PointOfSaleCode { get; set; } = "";
    public string PointOfSaleCity { get; set; } = "";

    // Opening cash amounts
    public decimal OpeningAmountUSD { get; set; }
    public decimal OpeningAmountCDF { get; set; }
    public decimal OpeningAmountEUR { get; set; }
    public decimal OpeningAmountCNY { get; set; }

    // Exchange rates (X per 1 unit → CDF)
    public decimal RateUSD { get; set; }
    public decimal RateEUR { get; set; }
    public decimal RateCNY { get; set; }

    // Notes
    public string Notes { get; set; } = "";

    // Computed total in CDF
    public decimal TotalEquivalentCDF =>
        (OpeningAmountUSD * RateUSD) +
        OpeningAmountCDF +
        (OpeningAmountEUR * RateEUR) +
        (OpeningAmountCNY * RateCNY);
}