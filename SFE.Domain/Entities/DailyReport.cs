using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// Rapport statistique conforme DGI 2026 §1.3 (Z/X) et §1.4 (A).
/// Pour Type=Z avec session, inclut les données d'ouverture/clôture de caisse.
/// </summary>
public class DailyReport
{
    public int Id { get; set; }

    // ── Identification ──
    public ReportType Type { get; set; }
    public int ReportNumber { get; set; }
    public bool IsPeriodic { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // ── En-tête entreprise (snapshot) ──
    public string CompanyName { get; set; } = "";
    public string CompanyNIF { get; set; } = "";
    public string ISF { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public int PointOfSaleId { get; set; }

    // ── Compteurs globaux ──
    public int TotalInvoiceCount { get; set; }
    public int TotalItemCount { get; set; }
    public int IncompleteCount { get; set; }

    // ── Totaux généraux nets (ventes − avoirs) ──
    public decimal GrandTotalHT { get; set; }
    public decimal GrandTotalTVA { get; set; }
    public decimal GrandTotalTTC { get; set; }
    public decimal TotalSpecificTax { get; set; }

    // ── Détails structurés ──
    public List<ReportInvoiceTypeSummary> InvoiceTypeSummaries { get; set; } = new();
    public List<ReportTaxGroupDetail> TaxGroupDetails { get; set; } = new();
    public List<ReportPaymentSummary> PaymentSummaries { get; set; } = new();
    public List<ArticleReportLine> ArticleLines { get; set; } = new();

    // ── Contenu formaté pour impression ──
    public string? PrintContent { get; set; }

    // ══════════════════════════════════════════════════════════
    //  🆕 SESSION DE CAISSE (Type=Z uniquement)
    // ══════════════════════════════════════════════════════════

    // ── Ouverture ──
    public DateTime? SessionOpenedAt { get; set; }
    public decimal OpeningAmountUSD { get; set; }
    public decimal OpeningAmountCDF { get; set; }
    public decimal OpeningAmountEUR { get; set; }
    public decimal OpeningAmountCNY { get; set; }
    public decimal RateUSD { get; set; }
    public decimal RateEUR { get; set; }
    public decimal RateCNY { get; set; }
    public string? OpeningNotes { get; set; }

    // ── Clôture (montants comptés par l'opérateur) ──
    public decimal ClosingAmountUSD { get; set; }
    public decimal ClosingAmountCDF { get; set; }
    public decimal ClosingAmountEUR { get; set; }
    public decimal ClosingAmountCNY { get; set; }
    public string? ClosingNotes { get; set; }

    // ── Caisse attendue (calculée) ──
    public decimal ExpectedCashUSD { get; set; }
    public decimal ExpectedCashCDF { get; set; }
    public decimal ExpectedCashEUR { get; set; }
    public decimal ExpectedCashCNY { get; set; }

    // ── Écarts ──
    public decimal VarianceUSD { get; set; }
    public decimal VarianceCDF { get; set; }
    public decimal VarianceEUR { get; set; }
    public decimal VarianceCNY { get; set; }

    // ── Helpers ──
    public bool HasSessionData => SessionOpenedAt.HasValue;

    public decimal OpeningTotalCDF =>
        (OpeningAmountUSD * RateUSD) + OpeningAmountCDF +
        (OpeningAmountEUR * RateEUR) + (OpeningAmountCNY * RateCNY);

    public decimal ExpectedTotalCDF =>
        (ExpectedCashUSD * RateUSD) + ExpectedCashCDF +
        (ExpectedCashEUR * RateEUR) + (ExpectedCashCNY * RateCNY);

    public decimal ClosingTotalCDF =>
        (ClosingAmountUSD * RateUSD) + ClosingAmountCDF +
        (ClosingAmountEUR * RateEUR) + (ClosingAmountCNY * RateCNY);

    public decimal VarianceTotalCDF =>
        (VarianceUSD * RateUSD) + VarianceCDF +
        (VarianceEUR * RateEUR) + (VarianceCNY * RateCNY);
}