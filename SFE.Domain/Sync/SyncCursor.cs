namespace SFE.Domain.Sync;

/// <summary>
/// Tracks how far this POS has pulled from the cloud for each entity type.
/// Keyed by (CompanyId, EntityType). The cloud's change-feed uses a
/// monotonic sequence number that we store in <see cref="LastSeenSequence"/>.
/// 
/// On resume after days offline, we ask the cloud for "changes after X".
/// </summary>
public class SyncCursor
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string EntityType { get; set; } = "";
    public long LastSeenSequence { get; set; }
    public DateTimeOffset? LastPullAtUtc { get; set; }
}