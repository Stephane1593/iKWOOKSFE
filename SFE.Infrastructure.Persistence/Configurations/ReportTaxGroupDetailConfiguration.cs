using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class ReportTaxGroupDetailConfiguration
    : IEntityTypeConfiguration<ReportTaxGroupDetail>
{
    public void Configure(EntityTypeBuilder<ReportTaxGroupDetail> builder)
    {
        builder.ToTable("ReportTaxGroupDetails");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(d => d.TaxableAmount).HasColumnType("decimal(18,2)");
        builder.Property(d => d.TaxAmount).HasColumnType("decimal(18,2)");

        builder.HasIndex(d => d.DailyReportId);
    }
}