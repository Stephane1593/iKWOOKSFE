using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<Invoice?> GetWithDetailsAsync(int invoiceId);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber);
    Task<Invoice?> GetByCodeDEFDGIAsync(string codeDEFDGI);
    Task<List<Invoice>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<List<Invoice>> GetByTypeAsync(InvoiceType type);
    Task<string> GenerateNextInvoiceNumberAsync(InvoiceType type, int year);
    Task<int> GetTodayCountAsync();
    Task<decimal> GetTodayTotalAsync();
    Task<List<Invoice>> GetCreditNotesForOriginalAsync(string originalCodeDEFDGI);
    Task<string?> GetLastNumberAsync(InvoiceType type);

    // Add to IInvoiceRepository:

    /// <summary>
    /// Récupère toutes les factures d'acompte (FT/ET) normalisées d'un groupe d'avances.
    /// </summary>
    Task<List<Invoice>> GetAdvancesByGroupAsync(string advanceGroupId);

    /// <summary>
    /// Récupère toutes les factures d'un même groupe d'avances (FT + FV final).
    /// </summary>
    Task<List<Invoice>> GetByAdvanceGroupAsync(string advanceGroupId);

    /// <summary>
    /// Vérifie si un Code DEF/DGI existe en base.
    /// </summary>
    Task<bool> CodeDEFDGIExistsAsync(string codeDEFDGI);


    // ── Nouveaux pour le journal ──
    Task<(List<Invoice> Items, int TotalCount)> SearchAsync(
        InvoiceSearchCriteria criteria, int page, int pageSize);
    Task<InvoicePeriodStats> GetPeriodStatsAsync(DateTime from, DateTime to);
    Task<Invoice?> GetByCodeDEFAsync(string codeDEF);
    Task<Invoice?> GetByNumberAsync(string invoiceNumber);
    Task<List<Invoice>> GetRecentAsync(int count);
}

/// <summary>
/// Critères de recherche pour le journal des ventes
/// </summary>
public class InvoiceSearchCriteria
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public InvoiceType? Type { get; set; }
    public InvoiceStatus? Status { get; set; }
    public string? SearchText { get; set; }       // Num facture, Code DEF, nom client
    public PaymentType? PaymentType { get; set; }
    public string? OperatorName { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Statistiques pour une période donnée
/// </summary>
public class InvoicePeriodStats
{
    public int TotalCount { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public int FVCount { get; set; }
    public int FTCount { get; set; }
    public int EVCount { get; set; }
    public int ETCount { get; set; }
    public decimal AverageAmount { get; set; }
    public decimal MaxInvoiceAmount { get; set; }
}

