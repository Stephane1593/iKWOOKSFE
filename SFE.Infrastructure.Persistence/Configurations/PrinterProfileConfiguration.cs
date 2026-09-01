using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class PrinterProfileConfiguration : IEntityTypeConfiguration<PrinterProfile>
{
    public void Configure(EntityTypeBuilder<PrinterProfile> builder)
    {
        builder.ToTable("PrinterProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
        .IsRequired()
        .HasMaxLength(200);

        builder.Property(p => p.Kind)
        .IsRequired()
        .HasMaxLength(50);

        builder.Property(p => p.ConnectionString)
        .IsRequired()
        .HasMaxLength(500);

        builder.Property(p => p.IsDefaultKitchen)
        .HasDefaultValue(false);

        builder.Property(p => p.IsDefaultReceipt)
        .HasDefaultValue(false);

        builder.HasIndex(p => p.Name);

        builder.HasIndex(p => p.IsDefaultKitchen);

        builder.HasIndex(p => p.IsDefaultReceipt);
    }
}