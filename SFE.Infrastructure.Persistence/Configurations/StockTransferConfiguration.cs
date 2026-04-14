// File: SFE.Infrastructure/Persistence/Configurations/StockTransferConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("StockTransfers");
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.TransferNumber).IsUnique();
        builder.HasIndex(t => t.Status);

        builder.Property(t => t.TransferNumber).HasMaxLength(50);
        builder.Property(t => t.Notes).HasMaxLength(500);
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.ReceivedBy).HasMaxLength(100);

        builder.Property(t => t.Status)
               .HasConversion<string>()
               .HasMaxLength(25);

        builder.HasOne(t => t.FromPointOfSale)
               .WithMany()
               .HasForeignKey(t => t.FromPointOfSaleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToPointOfSale)
               .WithMany()
               .HasForeignKey(t => t.ToPointOfSaleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Lines)
               .WithOne(l => l.StockTransfer)
               .HasForeignKey(l => l.StockTransferId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}