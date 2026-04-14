using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// §1.3.3.i — Montant total, montant taxable et TVA totale
/// par groupe de taxation, pour chaque type de facture.
/// </summary>
public class ReportTaxGroupDetail
{
    public int Id { get; set; }
    public int DailyReportId { get; set; }

    public InvoiceType InvoiceType { get; set; }
    public TaxGroup TaxGroup { get; set; }

    public decimal TotalAmount { get; set; }    // Montant total (TTC du groupe)
    public decimal TaxableAmount { get; set; }  // Montant taxable (HT)
    public decimal TaxAmount { get; set; }      // Montant TVA

    public DailyReport DailyReport { get; set; } = null!;
}