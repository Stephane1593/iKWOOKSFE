using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).HasMaxLength(50);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.ItemType).HasConversion<int>();
        builder.Property(l => l.TaxGroup).HasConversion<int>();
        builder.Property(l => l.TaxRate).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.Unit).HasMaxLength(20);
        builder.Property(l => l.TaxSpecificValue).HasMaxLength(30);
        builder.Property(l => l.TaxSpecificAmount).HasPrecision(18, 2);
        builder.Property(l => l.OriginalPrice).HasPrecision(18, 4);
        builder.Property(l => l.PriceModification).HasMaxLength(100);
        builder.Property(l => l.AmountHT).HasPrecision(18, 2);
        builder.Property(l => l.AmountTVA).HasPrecision(18, 2);
        builder.Property(l => l.AmountTTC).HasPrecision(18, 2);
    }
}