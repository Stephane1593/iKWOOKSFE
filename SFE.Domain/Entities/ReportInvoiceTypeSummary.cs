using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// §1.3.3.g — Montant total, montant taxable et taxe par type de facture.
/// </summary>
public class ReportInvoiceTypeSummary
{
    public int Id { get; set; }
    public int DailyReportId { get; set; }

    public InvoiceType InvoiceType { get; set; }
    public int Count { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal TotalSpecificTax { get; set; }

    public DailyReport DailyReport { get; set; } = null!;
}