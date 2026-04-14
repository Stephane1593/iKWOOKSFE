// File: SFE.Infrastructure/Persistence/Configurations/StockTransferLineConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("StockTransferLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.RequestedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.Notes).HasMaxLength(300);

        builder.HasOne(l => l.Product)
               .WithMany()
               .HasForeignKey(l => l.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}