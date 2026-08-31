using Cysharp.Text;

namespace SFE.Domain.Common;

/// <summary>
/// Base for entities that are THE tenant itself (currently only <c>Company</c>).
/// Has global identity, timestamps, soft delete, versioning — but NO CompanyId,
/// because the tenant can't reference itself.
/// </summary>
public abstract class SyncableRootEntity
{
    public int Id { get; set; }
    public Ulid SyncId { get; set; } = Ulid.NewUlid();

    public int? OriginPointOfSaleId { get; set; }
    public Ulid? OriginPointOfSaleSyncId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

    public long Version { get; set; } = 1;

    public bool IsDeleted => DeletedAtUtc is not null;

    public void MarkUpdated(DateTimeOffset utcNow)
    {
        UpdatedAtUtc = utcNow;
        Version++;
    }

    public void MarkDeleted(DateTimeOffset utcNow)
    {
        if (DeletedAtUtc is null) { DeletedAtUtc = utcNow; MarkUpdated(utcNow); }
    }

    public void Restore(DateTimeOffset utcNow)
    {
        if (DeletedAtUtc is not null) { DeletedAtUtc = null; MarkUpdated(utcNow); }
    }
}