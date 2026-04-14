using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class ArticleReportLineConfiguration
    : IEntityTypeConfiguration<ArticleReportLine>
{
    public void Configure(EntityTypeBuilder<ArticleReportLine> builder)
    {
        builder.ToTable("ArticleReportLines");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ArticleCode).HasMaxLength(50);
        builder.Property(a => a.ArticleName).HasMaxLength(200);
        builder.Property(a => a.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(a => a.TaxRate).HasColumnType("decimal(5,2)");
        builder.Property(a => a.QuantitySold).HasColumnType("decimal(18,3)");
        builder.Property(a => a.QuantityReturned).HasColumnType("decimal(18,3)");
        builder.Property(a => a.QuantityInStock).HasColumnType("decimal(18,3)");
        builder.Property(a => a.TotalAmount).HasColumnType("decimal(18,2)");

        builder.HasIndex(a => a.DailyReportId);
    }
}