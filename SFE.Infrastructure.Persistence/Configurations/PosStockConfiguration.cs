// File: SFE.Infrastructure/Persistence/Configurations/PosStockConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class PosStockConfiguration : IEntityTypeConfiguration<PosStock>
{
    public void Configure(EntityTypeBuilder<PosStock> builder)
    {
        builder.ToTable("PosStocks");

        builder.HasKey(ps => ps.Id);

        // Index unique : un seul enregistrement par (Product, POS)
        builder.HasIndex(ps => new { ps.ProductId, ps.PointOfSaleId })
               .IsUnique()
               .HasDatabaseName("IX_PosStock_Product_Pos");

        // Index pour recherche par POS
        builder.HasIndex(ps => ps.PointOfSaleId)
               .HasDatabaseName("IX_PosStock_Pos");

        builder.Property(ps => ps.Quantity)
               .HasPrecision(18, 4);
        builder.Property(ps => ps.MinStockLevel)
               .HasPrecision(18, 4);
        builder.Property(ps => ps.MaxStockLevel)
               .HasPrecision(18, 4);

        builder.HasOne(ps => ps.Product)
               .WithMany(p => p.PosStocks)
               .HasForeignKey(ps => ps.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.PointOfSale)
               .WithMany(pos => pos.PosStocks)
               .HasForeignKey(ps => ps.PointOfSaleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}