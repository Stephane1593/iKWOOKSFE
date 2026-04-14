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