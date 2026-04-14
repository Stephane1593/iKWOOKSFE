using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class ReportInvoiceTypeSummaryConfiguration
    : IEntityTypeConfiguration<ReportInvoiceTypeSummary>
{
    public void Configure(EntityTypeBuilder<ReportInvoiceTypeSummary> builder)
    {
        builder.ToTable("ReportInvoiceTypeSummaries");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TotalHT).HasColumnType("decimal(18,2)");
        builder.Property(s => s.TotalTVA).HasColumnType("decimal(18,2)");
        builder.Property(s => s.TotalTTC).HasColumnType("decimal(18,2)");
        builder.Property(s => s.TotalSpecificTax).HasColumnType("decimal(18,2)");

        builder.HasIndex(s => s.DailyReportId);
    }
}