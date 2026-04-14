using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class ReportPaymentSummaryConfiguration
    : IEntityTypeConfiguration<ReportPaymentSummary>
{
    public void Configure(EntityTypeBuilder<ReportPaymentSummary> builder)
    {
        builder.ToTable("ReportPaymentSummaries");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");

        builder.HasIndex(p => p.DailyReportId);
    }
}