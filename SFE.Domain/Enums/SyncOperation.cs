namespace SFE.Domain.Sync;

public enum SyncOperation
{
    /// <summary>Row was inserted or updated. Cloud does an idempotent upsert by SyncId.</summary>
    Upsert = 0,

    /// <summary>Row was soft-deleted. Cloud records DeletedAtUtc; other POSes see a tombstone.</summary>
    SoftDelete = 1,
}