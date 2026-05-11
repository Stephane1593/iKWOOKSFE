using Cysharp.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Common;

namespace SFE.Infrastructure.Persistence.Configurations;

public static class SyncableEntityConfiguration
{
    /// <summary>Configures columns shared by every syncable entity (root or tenant-scoped).</summary>
    public static void ApplySyncableRootConfig<T>(this EntityTypeBuilder<T> b)
        where T : SyncableRootEntity
    {
        b.HasKey(e => e.Id);

        b.Property(e => e.SyncId)
            .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
            .HasMaxLength(26)
            .IsRequired();
        b.HasIndex(e => e.SyncId).IsUnique();

        b.Property(e => e.CreatedAtUtc).IsRequired();
        b.Property(e => e.UpdatedAtUtc).IsRequired();
        b.HasIndex(e => e.UpdatedAtUtc); // delta sync
        b.HasIndex(e => e.DeletedAtUtc);

        b.Property(e => e.Version).IsRequired().IsConcurrencyToken();

        b.Property(e => e.OriginPointOfSaleSyncId)
            .HasConversion(
                v => v == null ? null : v.Value.ToString(),
                v => v == null ? null : (Ulid?)Ulid.Parse(v))
            .HasMaxLength(26);
    }

    /// <summary>Adds CompanyId column + delta-sync composite index.</summary>
    public static void ApplySyncableConfig<T>(this EntityTypeBuilder<T> b)
        where T : SyncableEntity
    {
        b.ApplySyncableRootConfig();
        b.Property(e => e.CompanyId).IsRequired();
        b.HasIndex(e => new { e.CompanyId, e.UpdatedAtUtc });
    }
}