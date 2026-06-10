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

        // ── Identité article ──
        builder.Property(l => l.Code).HasMaxLength(50);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.ItemType).HasConversion<int>();
        builder.Property(l => l.TaxGroup).HasConversion<int>();
        builder.Property(p => p.TaxGroupAType)
               .HasConversion<int>()
               .HasDefaultValue(SFE.Domain.Enums.TaxGroupAType.Exonere);
        builder.Property(l => l.TaxRate).HasPrecision(18, 4);

        // ── Prix unitaires (DUAL) — précision étendue à 4 décimales ──
        builder.Property(l => l.UnitPriceHT).HasPrecision(18, 4);
        builder.Property(l => l.UnitPriceTTC).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);          // legacy

        // ── Quantité — 3 décimales (spec DGI §1.5.1) ──
        builder.Property(l => l.Quantity).HasPrecision(18, 3);

        builder.Property(l => l.Unit).HasMaxLength(20);

        // ── Remise ──
        builder.Property(l => l.DiscountType).HasConversion<int>();
        builder.Property(l => l.DiscountValue).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.AmountHTBeforeDiscount).HasPrecision(18, 2);

        // ── Taxe spécifique (NEW typed model) ──
        builder.Property(l => l.SpecificTaxName).HasMaxLength(80);
        builder.Property(l => l.SpecificTaxType).HasConversion<int>();
        builder.Property(l => l.SpecificTaxValue).HasPrecision(18, 4);
        builder.Property(l => l.TaxApplicationMode).HasConversion<int>();
        builder.Property(l => l.SpecificTaxRate).HasPrecision(18, 4);    // legacy
        builder.Property(l => l.TaxSpecificValue).HasMaxLength(30);      // legacy MCF string
        builder.Property(l => l.TaxSpecificAmount).HasPrecision(18, 2);

        // ── Legacy fields (kept for back-compat — to be removed) ──
        builder.Property(l => l.OriginalPrice).HasPrecision(18, 4);
        builder.Property(l => l.PriceModification).HasPrecision(18, 4);  // 🐛 FIX: was HasMaxLength(100) on a decimal!

        // ── Totaux calculés ──
        builder.Property(l => l.AmountHT).HasPrecision(18, 2);
        builder.Property(l => l.AmountTVA).HasPrecision(18, 2);
        builder.Property(l => l.AmountTTC).HasPrecision(18, 2);

        // ── Index ──
        builder.HasIndex(l => l.InvoiceId);
        builder.HasIndex(l => new { l.InvoiceId, l.LineNumber }).IsUnique();
        builder.HasIndex(l => l.ArticleId);
        builder.HasIndex(l => l.ProductId);
    }
}