using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Sync;

namespace SFE.Infrastructure.Persistence.Configurations;

public class SyncOutboxEntryConfig : IEntityTypeConfiguration<SyncOutboxEntry>
{
    public void Configure(EntityTypeBuilder<SyncOutboxEntry> b)
    {
        b.ToTable("SyncOutbox");
        b.HasKey(x => x.Id);

        b.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);

        // Ulid stored as TEXT (26 chars) in SQLite.
        b.Property(x => x.EntitySyncId)
            .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
            .HasMaxLength(26)
            .IsRequired();

        b.Property(x => x.OriginPointOfSaleSyncId)
            .HasConversion(v => v!.Value.ToString(), v => Ulid.Parse(v))
            .HasMaxLength(26);

        // Worker scans "unsent, due now" — this is the hot index.
        b.HasIndex(x => new { x.SentAtUtc, x.NextAttemptAtUtc, x.Id })
            .HasDatabaseName("IX_SyncOutbox_Pending");

        b.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntitySyncId });
    }
}