using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("AuditLog");
        b.HasKey(e => e.Id);

        b.Property(e => e.Timestamp)
         .IsRequired();

        b.Property(e => e.Action)
         .HasConversion<int>()
         .IsRequired();

        b.Property(e => e.Module)
         .HasConversion<int>()
         .IsRequired();

        b.Property(e => e.Description).HasMaxLength(1000);
        b.Property(e => e.UserName).HasMaxLength(200);
        b.Property(e => e.EntityType).HasMaxLength(100);
        b.Property(e => e.EntityId).HasMaxLength(200);
        b.Property(e => e.CodeDEFDGI).HasMaxLength(500);
        b.Property(e => e.InvoiceNumber).HasMaxLength(100);
        b.Property(e => e.Details).HasMaxLength(4000);
        b.Property(e => e.PointOfSaleName).HasMaxLength(200);

        // ── Indexes for fast filtering ──
        b.HasIndex(e => e.Timestamp).HasDatabaseName("IX_AuditLog_Timestamp");
        b.HasIndex(e => e.Module).HasDatabaseName("IX_AuditLog_Module");
        b.HasIndex(e => e.Action).HasDatabaseName("IX_AuditLog_Action");
        b.HasIndex(e => e.UserId).HasDatabaseName("IX_AuditLog_UserId");
        b.HasIndex(e => e.CodeDEFDGI).HasDatabaseName("IX_AuditLog_CodeDEF");
        b.HasIndex(e => e.InvoiceNumber).HasDatabaseName("IX_AuditLog_InvNum");

        // Composite index for the most common query pattern
        b.HasIndex(e => new { e.Timestamp, e.Module })
         .HasDatabaseName("IX_AuditLog_Timestamp_Module");
    }
}