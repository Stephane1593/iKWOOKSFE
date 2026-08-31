using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("AppSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.CompanyNIF).HasMaxLength(50);
        builder.Property(x => x.CompanyRCCM).HasMaxLength(50);
        builder.Property(x => x.CompanyIdNat).HasMaxLength(50);
        builder.Property(x => x.CompanyAddress).HasMaxLength(500);
        builder.Property(x => x.CompanyPhone).HasMaxLength(50);
        builder.Property(x => x.CompanyEmail).HasMaxLength(200);

        builder.Property(x => x.CurrentExchangeRate)
            .HasColumnType("decimal(18,4)");
        builder.Property(x => x.CurrentExchangeRateEUR)
            .HasColumnType("decimal(18,4)");
        builder.Property(x => x.CurrentExchangeRateCNY)
            .HasColumnType("decimal(18,4)");

        // Stocker les enums en string pour lisibilité en DB
        builder.Property(x => x.ExchangeRateMode).HasConversion<string>();
        builder.Property(x => x.DefaultCurrency).HasConversion<string>();
        builder.Property(x => x.DefaultPriceMode).HasConversion<string>();

        // Seed une ligne par défaut
        builder.HasData(new AppSettings
        {
            Id = 1,
            ExchangeRateMode = Domain.Enums.ExchangeRateMode.Manual,
            CurrentExchangeRate = 2800m,
            CurrentExchangeRateEUR = 3100m,     // ← NEW
            CurrentExchangeRateCNY = 385m,      // ← NEW
            ExchangeRateUpdatedAt = new DateTime(2026, 1, 1),
            DefaultCurrency = Domain.Enums.Currency.CDF,
            DefaultPriceMode = Domain.Enums.PriceMode.TTC,
            DiscountBeforeTax = true,
            CompanyName = string.Empty,
            CompanyNIF = string.Empty,
            CompanyRCCM = string.Empty,
            CompanyIdNat = string.Empty,
            CompanyAddress = string.Empty,
            CompanyPhone = string.Empty,
            CompanyEmail = string.Empty,
            UpdatedAt = new DateTime(2026, 1, 1)
        });
    }
}