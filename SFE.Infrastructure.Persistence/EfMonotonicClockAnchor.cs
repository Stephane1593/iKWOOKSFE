using Microsoft.EntityFrameworkCore;
using SFE.Domain.Abstractions;

namespace SFE.Infrastructure.Persistence;

/// <summary>
/// Returns the newest domain-persisted UTC instant across tables that are
/// written by normal app usage. Used by AntiClockTamper to detect system-clock
/// rollbacks even if the licensing state file has been deleted.
/// </summary>
public sealed class EfMonotonicClockAnchor : IMonotonicClockAnchor
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public EfMonotonicClockAnchor(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    public async Task<DateTimeOffset?> GetLatestPersistedUtcAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            // IgnoreQueryFilters => we don't want tenant / soft-delete filters
            // to hide the true "last time the app wrote something" instant.

            var invoiceMax = await db.Invoices
                .IgnoreQueryFilters()
                .Select(i => (DateTimeOffset?)i.CreatedAt)
                .MaxAsync(ct);

            var auditMax = await db.AuditLogEntries
                .IgnoreQueryFilters()
                .Select(a => (DateTimeOffset?)a.Timestamp)     // ← rename if your property differs
                .MaxAsync(ct);

            var paymentMax = await db.PaymentTransactions
                .IgnoreQueryFilters()
                .Select(p => (DateTimeOffset?)p.CreatedUtc)  // ← rename if your property differs
                .MaxAsync(ct);

            return Max(Max(invoiceMax, auditMax), paymentMax);
        }
        catch
        {
            // DB not ready / migration in flight / fresh install — the state file
            // alone will carry the high-water mark. Never let the anchor break boot.
            return null;
        }
    }

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a > b ? a : b;
    }
}