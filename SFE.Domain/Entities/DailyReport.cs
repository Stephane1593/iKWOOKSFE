using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// Rapport statistique conforme DGI 2026 §1.3 (Z/X) et §1.4 (A).
/// </summary>
public class DailyReport
{
    public int Id { get; set; }

    // ── Identification ──
    public ReportType Type { get; set; }
    public int ReportNumber { get; set; }
    public bool IsPeriodic { get; set; }          // true = X périodique (§1.3.2)
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // ── En-tête entreprise (snapshot au moment de la génération) ──
    public string CompanyName { get; set; } = "";   // §1.3.3.a
    public string CompanyNIF { get; set; } = "";    // §1.3.3.b
    public string ISF { get; set; } = "";           // §1.3.3.f
    public string OperatorName { get; set; } = "";
    public int PointOfSaleId { get; set; }

    // ── Compteurs globaux ──
    public int TotalInvoiceCount { get; set; }
    public int TotalItemCount { get; set; }
    public int IncompleteCount { get; set; }        // §1.3.3.m

    // ── Totaux généraux nets (ventes − avoirs) ──
    public decimal GrandTotalHT { get; set; }
    public decimal GrandTotalTVA { get; set; }
    public decimal GrandTotalTTC { get; set; }
    public decimal TotalSpecificTax { get; set; }

    // ── Détails structurés ──
    public List<ReportInvoiceTypeSummary> InvoiceTypeSummaries { get; set; } = new();
    public List<ReportTaxGroupDetail> TaxGroupDetails { get; set; } = new();
    public List<ReportPaymentSummary> PaymentSummaries { get; set; } = new();
    public List<ArticleReportLine> ArticleLines { get; set; } = new();  // A-rapport uniquement

    // ── Contenu formaté pour impression ──
    public string? PrintContent { get; set; }
}