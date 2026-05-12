using System.Text.Json;
using System.Text.Json.Serialization;
using Cysharp.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SFE.Domain.Abstractions;
using SFE.Domain.Sync;
using SFE.Domain.Common;

namespace SFE.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Cross-cutting EF interceptor that turns every change to a
/// <see cref="SyncableEntity"/>/<see cref="SyncableRootEntity"/> into:
/// 
///   1. Proper audit stamping (CreatedAtUtc, UpdatedAtUtc, Version).
///   2. Soft-delete rewriting (hard Delete → Modified + DeletedAtUtc).
///   3. An entry in <see cref="SyncOutboxEntry"/> for cloud upload.
/// 
/// Entirely data-driven: no per-entity code required.
/// </summary>
public sealed class SyncTrackingInterceptor : SaveChangesInterceptor
{
    private readonly ITimeProvider _clock;
    private readonly ITenantProvider _tenant;

    private static readonly JsonSerializerOptions PayloadJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
        Converters = { new UlidJsonConverter() }
    };

    public SyncTrackingInterceptor(ITimeProvider clock, ITenantProvider tenant)
    {
        _clock = clock;
        _tenant = tenant;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChangesAsync(eventData, result, ct);

        var now = _clock.UtcNow;
        var outboxSet = ctx.Set<SyncOutboxEntry>();

        foreach (var entry in ctx.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is SyncableEntity syncable)
                ApplyToSyncable(entry, syncable, now, outboxSet);
            else if (entry.Entity is SyncableRootEntity root)
                ApplyToRoot(entry, root, now, outboxSet);
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }

    // ═══════════════════════════════════════════════════════════════

    private void ApplyToSyncable(
        EntityEntry entry, SyncableEntity e,
        DateTimeOffset now, DbSet<SyncOutboxEntry> outbox)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                if (e.SyncId == default) e.SyncId = Ulid.NewUlid();
                if (e.CompanyId == 0 && _tenant.CompanyId is int cid) e.CompanyId = cid;
                if (e.OriginPointOfSaleSyncId is null && _tenant.CurrentPointOfSaleSyncId is Ulid pos)
                    e.OriginPointOfSaleSyncId = pos;
                e.CreatedAtUtc = now;
                e.UpdatedAtUtc = now;
                e.Version = 1;
                EnqueueOutbox(outbox, entry, e.CompanyId, e.OriginPointOfSaleSyncId,
                    e.SyncId, e.Version, SyncOperation.Upsert, now);
                break;

            case EntityState.Modified:
                e.UpdatedAtUtc = now;
                e.Version++;
                EnqueueOutbox(outbox, entry, e.CompanyId, e.OriginPointOfSaleSyncId,
                    e.SyncId, e.Version, SyncOperation.Upsert, now);
                break;

            case EntityState.Deleted:
                // Rewrite hard delete as soft delete.
                entry.State = EntityState.Modified;
                e.DeletedAtUtc = now;
                e.UpdatedAtUtc = now;
                e.Version++;
                EnqueueOutbox(outbox, entry, e.CompanyId, e.OriginPointOfSaleSyncId,
                    e.SyncId, e.Version, SyncOperation.SoftDelete, now);
                break;
        }
    }

    private void ApplyToRoot(
        EntityEntry entry, SyncableRootEntity e,
        DateTimeOffset now, DbSet<SyncOutboxEntry> outbox)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                if (e.SyncId == default) e.SyncId = Ulid.NewUlid();
                e.CreatedAtUtc = now;
                e.UpdatedAtUtc = now;
                e.Version = 1;
                // Company's own CompanyId is... itself. We use Id once assigned.
                // Outbox will carry CompanyId = 0 for root; cloud resolves via SyncId.
                EnqueueOutbox(outbox, entry, 0, null,
                    e.SyncId, e.Version, SyncOperation.Upsert, now);
                break;

            case EntityState.Modified:
                e.UpdatedAtUtc = now;
                e.Version++;
                EnqueueOutbox(outbox, entry, 0, null,
                    e.SyncId, e.Version, SyncOperation.Upsert, now);
                break;

            case EntityState.Deleted:
                entry.State = EntityState.Modified;
                e.DeletedAtUtc = now;
                e.UpdatedAtUtc = now;
                e.Version++;
                EnqueueOutbox(outbox, entry, 0, null,
                    e.SyncId, e.Version, SyncOperation.SoftDelete, now);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════

    private static void EnqueueOutbox(
        DbSet<SyncOutboxEntry> outbox,
        EntityEntry entry,
        int companyId,
        Ulid? originPos,
        Ulid syncId,
        long version,
        SyncOperation op,
        DateTimeOffset now)
    {
        outbox.Add(new SyncOutboxEntry
        {
            CompanyId = companyId,
            OriginPointOfSaleSyncId = originPos,
            EntityType = entry.Metadata.ClrType.Name,
            EntitySyncId = syncId,
            EntityVersion = version,
            Operation = op,
            PayloadJson = SerializePayload(entry),
            EnqueuedAtUtc = now,
            AttemptCount = 0
        });
    }

    /// <summary>
    /// Serializes only the mapped scalar properties — never navigation
    /// properties, never lazy-loaded collections. Keys by property name.
    /// </summary>
    private static string SerializePayload(EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in entry.Metadata.GetProperties())
        {
            // Skip shadow properties (no CLR member, EF-managed only).
            if (prop.IsShadowProperty()) continue;
            dict[prop.Name] = entry.Property(prop.Name).CurrentValue;
        }
        return JsonSerializer.Serialize(dict, PayloadJsonOpts);
    }

    // Minimal Ulid ↔ string converter. Elevate to a shared file later.
    private sealed class UlidJsonConverter : JsonConverter<Ulid>
    {
        public override Ulid Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __)
            => Ulid.Parse(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, Ulid value, JsonSerializerOptions _)
            => writer.WriteStringValue(value.ToString());
    }
}