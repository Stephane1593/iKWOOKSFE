using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        // ══════════════════════════════════════
        //  IDENTITÉ
        // ══════════════════════════════════════
        builder.Property(p => p.Code).HasMaxLength(50);
        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(500);

        // ══════════════════════════════════════
        //  FISCALITÉ
        // ══════════════════════════════════════
        builder.Property(p => p.ItemType).HasConversion<int>();
        builder.Property(p => p.TaxGroup).HasConversion<int>();
        builder.Property(p => p.SpecificTaxValue).HasMaxLength(30);
        builder.Property(p => p.TaxSpecificMode).HasConversion<int>();

        // ══════════════════════════════════════
        //  TARIFICATION MULTI-DEVISE
        // ══════════════════════════════════════
        builder.Property(p => p.UnitPriceHtCdf).HasPrecision(18, 4);
        builder.Property(p => p.UnitPriceTtcCdf).HasPrecision(18, 4);
        builder.Property(p => p.UnitPriceHtUsd).HasPrecision(18, 4);
        builder.Property(p => p.UnitPriceTtcUsd).HasPrecision(18, 4);
        builder.Property(p => p.UnitPrice).HasPrecision(18, 4);   // rétro-compat
        builder.Property(p => p.Unit).HasMaxLength(20);

        // ══════════════════════════════════════
        //  REMISE PAR DÉFAUT
        // ══════════════════════════════════════
        builder.Property(p => p.DefaultDiscountType).HasConversion<int>();
        builder.Property(p => p.DefaultDiscountValue).HasPrecision(18, 4);

        // ══════════════════════════════════════
        //  STOCK
        // ══════════════════════════════════════
        builder.Property(p => p.StockQuantity).HasPrecision(18, 3);
        builder.Property(p => p.MinStockLevel).HasPrecision(18, 3);

        // ══════════════════════════════════════
        //  RELATION — CATÉGORIE
        // ══════════════════════════════════════
        builder.HasOne(p => p.Category)
               .WithMany()
               .HasForeignKey(p => p.CategoryId)
               .OnDelete(DeleteBehavior.SetNull);

        // ══════════════════════════════════════
        //  INDEX
        // ══════════════════════════════════════
        builder.HasIndex(p => p.Code);
        builder.HasIndex(p => p.Barcode);
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.IsFavorite);

        // ══════════════════════════════════════
        //  PROPRIÉTÉS IGNORÉES (NotMapped)
        // ══════════════════════════════════════
        builder.Ignore(p => p.TaxGroupLabel);
        builder.Ignore(p => p.DisplayText);
        builder.Ignore(p => p.TaxSpecificModeShort);
        builder.Ignore(p => p.HasDefaultDiscount);
        builder.Ignore(p => p.DefaultDiscountDisplay);
        builder.Ignore(p => p.PriceSummary);
    }
}