namespace SFE.Application.Services;

/// <summary>
/// DTO passed from the SessionCloseViewModel to ReportService
/// containing all data needed to generate a session Z-report.
/// </summary>
public class SessionCloseData
{
    // ── Opening (from CashSessionInfo) ──
    public DateTime SessionOpenedAt { get; set; }
    public int PointOfSaleId { get; set; }
    public string OperatorName { get; set; } = "";

    public decimal OpeningAmountUSD { get; set; }
    public decimal OpeningAmountCDF { get; set; }
    public decimal OpeningAmountEUR { get; set; }
    public decimal OpeningAmountCNY { get; set; }

    public decimal RateUSD { get; set; }
    public decimal RateEUR { get; set; }
    public decimal RateCNY { get; set; }

    public string? OpeningNotes { get; set; }

    // ── Closing (operator counted) ──
    public decimal ClosingAmountUSD { get; set; }
    public decimal ClosingAmountCDF { get; set; }
    public decimal ClosingAmountEUR { get; set; }
    public decimal ClosingAmountCNY { get; set; }

    public string? ClosingNotes { get; set; }
}