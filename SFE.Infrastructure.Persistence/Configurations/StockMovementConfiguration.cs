// File: SFE.Infrastructure/Persistence/Configurations/StockMovementConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.ProductId, m.PointOfSaleId, m.CreatedAt })
               .HasDatabaseName("IX_StockMvt_Product_Pos_Date");

        builder.HasIndex(m => m.Reference)
               .HasDatabaseName("IX_StockMvt_Reference");

        builder.HasIndex(m => m.TransferReference)
               .HasDatabaseName("IX_StockMvt_TransferRef");

        builder.HasIndex(m => m.CreatedAt)
               .HasDatabaseName("IX_StockMvt_Date");

        builder.Property(m => m.Quantity).HasPrecision(18, 4);
        builder.Property(m => m.QuantityBefore).HasPrecision(18, 4);
        builder.Property(m => m.QuantityAfter).HasPrecision(18, 4);
        builder.Property(m => m.UnitCost).HasPrecision(18, 4);

        builder.Property(m => m.Reference).HasMaxLength(200);
        builder.Property(m => m.TransferReference).HasMaxLength(200);
        builder.Property(m => m.Notes).HasMaxLength(500);
        builder.Property(m => m.OperatorName).HasMaxLength(100);

        builder.Property(m => m.Type)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.HasOne(m => m.Product)
               .WithMany()
               .HasForeignKey(m => m.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PointOfSale)
               .WithMany(pos => pos.StockMovements)
               .HasForeignKey(m => m.PointOfSaleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}