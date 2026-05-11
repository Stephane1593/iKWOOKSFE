using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Configurations;

public class PointOfSaleConfiguration : IEntityTypeConfiguration<PointOfSale>
{
    public void Configure(EntityTypeBuilder<PointOfSale> builder)
    {
        builder.ToTable("PointsOfSale");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Address)
            .HasMaxLength(300);

        builder.Property(p => p.City)
            .HasMaxLength(100);

        builder.Property(p => p.Phone)
            .HasMaxLength(50);

        // 🆕 Fuseau horaire du POS
        //    IANA ID ("Africa/Kinshasa"), Windows ID ("W. Central Africa Standard Time"),
        //    ou simple nom de ville ("Goma", "Lubumbashi").
        //    Résolu à l'exécution par ITimeProvider.GetZone(...).
        //    NULL => fallback sur AppTimeZone du tenant.
        builder.Property(p => p.TimeZoneId)
            .HasMaxLength(100);

        builder.Property(p => p.DeviceType)
            .HasConversion<int>()
            .HasDefaultValue(DeviceType.EMcf);

        builder.Property(p => p.EmcfApiUrl)
            .HasMaxLength(500);

        builder.Property(p => p.EmcfToken)
            .HasMaxLength(2000);

        builder.Property(p => p.EmcfNIM)
            .HasMaxLength(100);

        builder.Property(p => p.McfPortName)
            .HasMaxLength(20);

        builder.Property(p => p.McfBaudRate)
            .HasDefaultValue(115200);

        // ═══ Connection status ═══
        builder.Property(p => p.LastKnownNIM).HasMaxLength(100);
        builder.Property(p => p.LastKnownNIF).HasMaxLength(100);
        builder.Property(p => p.McfServerStatus).HasMaxLength(10);

        // 🆕 Stock
        builder.Property(p => p.ManagesStock).HasDefaultValue(true);
        builder.Property(p => p.AllowNegativeStock).HasDefaultValue(false);

        // 🖨 Printer columns
        builder.Property(p => p.ThermalPrinterName).HasMaxLength(200).HasDefaultValue("");
        builder.Property(p => p.PaperWidthMm).HasDefaultValue(80);
        builder.Property(p => p.AutoPrintReceipt).HasDefaultValue(true);
        builder.Property(p => p.PrintCopies).HasDefaultValue(1);
        builder.Property(p => p.EnableCustomerDisplay).HasDefaultValue(false);
        builder.Property(p => p.EnableCashDrawer).HasDefaultValue(false);
        builder.Property(p => p.CashDrawerPin).HasDefaultValue(0);
        builder.Property(p => p.PrinterCodePage).HasDefaultValue(858);
        builder.Property(p => p.PrintLogo).HasDefaultValue(false);
        builder.Property(p => p.ReceiptFooterText)
            .HasMaxLength(500)
            .HasDefaultValue("Merci pour votre achat !");

        // 🆕 Operators relationship
        builder.HasMany(p => p.Operators)
            .WithOne(u => u.PointOfSale)
            .HasForeignKey(u => u.PointOfSaleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}