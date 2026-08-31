using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IAuditLogRepository : IRepository<AuditLogEntry>
{
    Task<(List<AuditLogEntry> Items, int TotalCount)> SearchAsync(
        AuditLogSearchCriteria criteria, int page, int pageSize);

    Task<AuditLogStats> GetStatsAsync(DateTimeOffset from, DateTimeOffset to);

    Task<List<string>> GetDistinctUserNamesAsync();
}

// ── Search criteria ──────────────────────────────────
public class AuditLogSearchCriteria
{
    /// <summary>
    /// Inclusive lower bound. Must already be normalized to start-of-day
    /// in the caller's timezone (use ToStartOfDayOffset in the VM).
    /// </summary>
    public DateTimeOffset? DateFrom { get; set; }

    /// <summary>
    /// Inclusive upper bound. Must already be normalized to end-of-day
    /// in the caller's timezone (use ToEndOfDayOffset in the VM).
    /// </summary>
    public DateTimeOffset? DateTo { get; set; }

    public AuditModule? Module { get; set; }
    public AuditAction? Action { get; set; }
    public string? UserName { get; set; }
    public string? SearchText { get; set; }
}

// ── Period stats ─────────────────────────────────────
public class AuditLogStats
{
    public int TotalCount { get; set; }
    public int InvoiceCount { get; set; }
    public int ReportCount { get; set; }
    public int AuthCount { get; set; }
    public int StockCount { get; set; }
    public int SettingsCount { get; set; }
    public int OtherCount { get; set; }
}