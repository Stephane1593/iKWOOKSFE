using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// §1.4.2.f — Détail par article pour le A-rapport.
/// </summary>
public class ArticleReportLine
{
    public int Id { get; set; }
    public int DailyReportId { get; set; }

    public string ArticleCode { get; set; } = "";
    public string ArticleName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public TaxGroup TaxGroup { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityInStock { get; set; }
    public decimal TotalAmount { get; set; }

    public DailyReport DailyReport { get; set; } = null!;
}