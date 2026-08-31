namespace SFE.Domain.Abstractions;

/// <summary>
/// Provides an authoritative lower bound for "the app was definitely running
/// after this instant." Implementations query domain tables (Invoices,
/// AuditLog, etc.) for MAX(CreatedAtUtc) / MAX(Timestamp).
///
/// Must be safe to call before the DB is fully initialized; return null in
/// that case rather than throwing.
/// </summary>
public interface IMonotonicClockAnchor
{
    Task<DateTimeOffset?> GetLatestPersistedUtcAsync(CancellationToken ct = default);
}