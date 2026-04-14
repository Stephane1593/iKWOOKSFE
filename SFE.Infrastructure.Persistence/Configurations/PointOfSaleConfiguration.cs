using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Configurations;

public class PointOfSaleConfiguration : IEntityTypeConfiguration<PointOfSale>
{
    public void Configure(EntityTypeBuilder<PointOfSale> builder)
    {
        builder.ToTable("PointsOfSale");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Address)
            .HasMaxLength(300);

        builder.Property(p => p.City)
            .HasMaxLength(100);

        builder.Property(p => p.Phone)
            .HasMaxLength(50);

        builder.Property(p => p.DeviceType)
            .HasConversion<int>()
            .HasDefaultValue(DeviceType.EMcf);

        builder.Property(p => p.EmcfApiUrl)
            .HasMaxLength(500);

        builder.Property(p => p.EmcfToken)
            .HasMaxLength(2000);

        builder.Property(p => p.EmcfNIM)
            .HasMaxLength(100);

        builder.Property(p => p.McfPortName)
            .HasMaxLength(20);

        builder.Property(p => p.McfBaudRate)
            .HasDefaultValue(115200);
    }
}