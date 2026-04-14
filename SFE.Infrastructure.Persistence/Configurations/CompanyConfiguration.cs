using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.NIF)
            .IsRequired()
            .HasMaxLength(50);

        // 🆕 ISF — Identifiant Système de Facturation
        builder.Property(c => c.ISF)
            .HasMaxLength(100)
            .HasDefaultValue("");

        builder.Property(c => c.RCCM)
            .HasMaxLength(100);

        builder.Property(c => c.Address)
            .HasMaxLength(300);

        builder.Property(c => c.City)
            .HasMaxLength(100);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        // 🆕 Logo — stored as BLOB (no max length needed for byte[])
        builder.Property(c => c.Logo)
            .HasColumnType("BLOB");

        builder.Property(c => c.DefaultPriceMode)
            .HasConversion<int>()
            .HasDefaultValue(PriceMode.TTC);

        builder.Property(c => c.DeploymentMode)
            .HasConversion<int>()
            .HasDefaultValue(DeploymentMode.Standalone);

        builder.Property(c => c.LoyaltyEarnRate)
            .HasPrecision(18, 2)
            .HasDefaultValue(1000m);

        builder.Property(c => c.LoyaltyRedeemRate)
            .HasPrecision(18, 2)
            .HasDefaultValue(500m);

        builder.HasMany(c => c.PointsOfSale)
            .WithOne(p => p.Company)
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}