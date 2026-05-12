using Cysharp.Text;

namespace SFE.Domain.Sync;

/// <summary>
/// Durable, transactional log of changes waiting to be shipped to the cloud.
/// One row per (entity row × save). Ordered by <see cref="Id"/>.
/// 
/// Written INSIDE the same SaveChanges as the business rows — if the
/// business save rolls back, the outbox entry rolls back with it.
/// </summary>
public class SyncOutboxEntry
{
    public long Id { get; set; }

    // ── Tenant / origin ──
    public int CompanyId { get; set; }
    public Ulid? OriginPointOfSaleSyncId { get; set; }

    // ── What changed ──
    /// <summary>CLR simple name, e.g. "Product", "Invoice", "InvoiceLine".</summary>
    public string EntityType { get; set; } = "";

    public Ulid EntitySyncId { get; set; }

    public SyncOperation Operation { get; set; }

    /// <summary>Value of the entity's Version AT THE TIME of enqueue.</summary>
    public long EntityVersion { get; set; }

    /// <summary>
    /// JSON snapshot of the row (column values only — never navigations).
    /// Cloud applies this as the new authoritative state for the given Version.
    /// </summary>
    public string PayloadJson { get; set; } = "";

    // ── Delivery state ──
    public DateTimeOffset EnqueuedAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    /// <summary>Helper — sent ⇒ can be pruned after retention window.</summary>
    public bool IsSent => SentAtUtc.HasValue;
}