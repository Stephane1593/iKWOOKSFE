using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// §1.3.3.j — Nombre de factures par mode de paiement.
/// §1.3.3.k — Montants totaux par mode de paiement.
/// </summary>
public class ReportPaymentSummary
{
    public int Id { get; set; }
    public int DailyReportId { get; set; }

    public PaymentType PaymentType { get; set; }
    public int InvoiceCount { get; set; }       // §1.3.3.j
    public decimal TotalAmount { get; set; }    // §1.3.3.k

    public DailyReport DailyReport { get; set; } = null!;
}