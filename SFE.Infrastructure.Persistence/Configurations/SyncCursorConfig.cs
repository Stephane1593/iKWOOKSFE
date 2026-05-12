using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Sync;

namespace SFE.Infrastructure.Persistence.Configurations;

public class SyncCursorConfig : IEntityTypeConfiguration<SyncCursor>
{
    public void Configure(EntityTypeBuilder<SyncCursor> b)
    {
        b.ToTable("SyncCursors");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.CompanyId, x.EntityType }).IsUnique();
    }
}