using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class DailyReportConfiguration : IEntityTypeConfiguration<DailyReport>
{
    public void Configure(EntityTypeBuilder<DailyReport> builder)
    {
        builder.ToTable("DailyReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.CompanyName).HasMaxLength(200);
        builder.Property(r => r.CompanyNIF).HasMaxLength(50);
        builder.Property(r => r.ISF).HasMaxLength(100);
        builder.Property(r => r.OperatorName).HasMaxLength(100);

        builder.Property(r => r.GrandTotalHT).HasColumnType("decimal(18,2)");
        builder.Property(r => r.GrandTotalTVA).HasColumnType("decimal(18,2)");
        builder.Property(r => r.GrandTotalTTC).HasColumnType("decimal(18,2)");
        builder.Property(r => r.TotalSpecificTax).HasColumnType("decimal(18,2)");

        // ── 🆕 Session Opening ──
        builder.Property(r => r.OpeningAmountUSD).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.OpeningAmountCDF).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.OpeningAmountEUR).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.OpeningAmountCNY).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.RateUSD).HasColumnType("decimal(18,4)").HasDefaultValue(0);
        builder.Property(r => r.RateEUR).HasColumnType("decimal(18,4)").HasDefaultValue(0);
        builder.Property(r => r.RateCNY).HasColumnType("decimal(18,4)").HasDefaultValue(0);
        builder.Property(r => r.OpeningNotes).HasMaxLength(500);

        // ── 🆕 Session Closing ──
        builder.Property(r => r.ClosingAmountUSD).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ClosingAmountCDF).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ClosingAmountEUR).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ClosingAmountCNY).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ClosingNotes).HasMaxLength(500);

        // ── 🆕 Expected Cash ──
        builder.Property(r => r.ExpectedCashUSD).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ExpectedCashCDF).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ExpectedCashEUR).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.ExpectedCashCNY).HasColumnType("decimal(18,2)").HasDefaultValue(0);

        // ── 🆕 Variance ──
        builder.Property(r => r.VarianceUSD).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.VarianceCDF).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.VarianceEUR).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        builder.Property(r => r.VarianceCNY).HasColumnType("decimal(18,2)").HasDefaultValue(0);

        // ── 🆕 Ignore computed properties ──
        builder.Ignore(r => r.HasSessionData);
        builder.Ignore(r => r.OpeningTotalCDF);
        builder.Ignore(r => r.ExpectedTotalCDF);
        builder.Ignore(r => r.ClosingTotalCDF);
        builder.Ignore(r => r.VarianceTotalCDF);

        // ── Relations ──
        builder.HasMany(r => r.InvoiceTypeSummaries)
            .WithOne(s => s.DailyReport)
            .HasForeignKey(s => s.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.TaxGroupDetails)
            .WithOne(d => d.DailyReport)
            .HasForeignKey(d => d.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.PaymentSummaries)
            .WithOne(p => p.DailyReport)
            .HasForeignKey(p => p.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.ArticleLines)
            .WithOne(a => a.DailyReport)
            .HasForeignKey(a => a.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}