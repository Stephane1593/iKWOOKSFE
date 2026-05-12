using Cysharp.Text;

namespace SFE.Domain.Common;

/// <summary>
/// Base for every tenant-scoped row that flows through cloud sync.
/// Parallel to <see cref="SyncableRootEntity"/> — does NOT inherit from it.
/// </summary>
public abstract class SyncableEntity
{
    public int Id { get; set; }

    /// <summary>Tenant scope. Stamped from <see cref="ITenantProvider.CompanyId"/> on insert.</summary>
    public int CompanyId { get; set; }

    /// <summary>Portable identity — unique per (CompanyId, SyncId). Immutable after first save.</summary>
    public Ulid SyncId { get; set; } = Ulid.NewUlid();

    /// <summary>Monotonic version. Starts at 1, +1 on each save. Basis for LWW conflict resolution.</summary>
    public long Version { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>Local FK to the origin POS. Null for company-wide rows.</summary>
    public int? OriginPointOfSaleId { get; set; }

    /// <summary>Portable identity of the origin POS. Set in parallel with <see cref="OriginPointOfSaleId"/>.</summary>
    public Ulid? OriginPointOfSaleSyncId { get; set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    // ── Lifecycle helpers used by AppDbContext.SaveChanges ──

    public void MarkUpdated(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        Version++;
    }

    public void MarkDeleted(DateTimeOffset now)
    {
        DeletedAtUtc = now;
        UpdatedAtUtc = now;
        Version++;
    }
}

/// <summary>
/// Base for the tenant-root entity (<c>Company</c>). No CompanyId — it IS the tenant.
/// No OriginPointOfSale — a company isn't owned by a POS.
/// </summary>
public abstract class SyncableRootEntity
{
    public int Id { get; set; }
    public Ulid SyncId { get; set; } = Ulid.NewUlid();
    public long Version { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public void MarkUpdated(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        Version++;
    }

    public void MarkDeleted(DateTimeOffset now)
    {
        DeletedAtUtc = now;
        UpdatedAtUtc = now;
        Version++;
    }
}