namespace SFE.WPF.Models;

public class ReportListItem
{
    public int Id { get; set; }
    public int ReportNumber { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public string OperatorName { get; set; } = "";
    public string ISF { get; set; } = "";
    public decimal GrandTotalTTC { get; set; }
    public int TotalInvoiceCount { get; set; }
    public string? PrintContent { get; set; }
    public bool HasSessionData { get; set; }
    public bool IsPeriodic { get; set; }
    public string TypePrefix { get; set; } = "";

    // ── Display helpers ──
    public string DateDisplay => GeneratedAt.ToString("dd/MM/yyyy HH:mm");
    public string ReferenceDisplay => $"{TypePrefix}-{ReportNumber:D3}";
    public string TotalDisplay => $"{GrandTotalTTC:N0} CDF";
    public string PeriodDisplay => $"{PeriodStart:dd/MM} → {PeriodEnd:dd/MM/yyyy HH:mm}";
    public string InvoiceCountText => $"{TotalInvoiceCount} facture(s)";
}