using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(64);
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();

        builder.Property(i => i.Type).HasConversion<int>();
        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.PriceMode).HasConversion<int>();
        builder.Property(i => i.ClientType).HasConversion<int>();
        builder.Property(i => i.CreditNoteNature).HasConversion<int?>();

        builder.Property(i => i.ISF).HasMaxLength(20);
        builder.Property(i => i.ClientNIF).HasMaxLength(50);
        builder.Property(i => i.ClientName).HasMaxLength(300);
        builder.Property(i => i.ClientAddress).HasMaxLength(300);
        builder.Property(i => i.ClientPhone).HasMaxLength(50);
        builder.Property(i => i.ClientEmail).HasMaxLength(200);
        builder.Property(i => i.ClientRCCM).HasMaxLength(100);
        builder.Property(i => i.OperatorId).HasMaxLength(20);
        builder.Property(i => i.OperatorName).HasMaxLength(100);
        builder.Property(i => i.OriginalInvoiceReference).HasMaxLength(50);
        builder.Property(i => i.ReferenceType).HasMaxLength(10);
        builder.Property(i => i.ReferenceDesc).HasMaxLength(50);
        builder.Property(i => i.CurrencyCode).HasMaxLength(10);
        builder.Property(i => i.CurrencyRate).HasPrecision(18, 4);

        builder.Property(i => i.CommentA).HasMaxLength(500);
        builder.Property(i => i.CommentB).HasMaxLength(500);
        builder.Property(i => i.CommentC).HasMaxLength(500);
        builder.Property(i => i.CommentD).HasMaxLength(500);
        builder.Property(i => i.CommentE).HasMaxLength(500);
        builder.Property(i => i.CommentF).HasMaxLength(500);
        builder.Property(i => i.CommentG).HasMaxLength(500);
        builder.Property(i => i.CommentH).HasMaxLength(500);

        builder.Property(i => i.TotalHT).HasPrecision(18, 2);
        builder.Property(i => i.TotalTVA).HasPrecision(18, 2);
        builder.Property(i => i.TotalTTC).HasPrecision(18, 2);
        builder.Property(i => i.TotalSpecificTax).HasPrecision(18, 2);

        builder.Property(i => i.EmcfUid).HasMaxLength(50);
        builder.Property(i => i.CodeDEFDGI).HasMaxLength(50);
        builder.Property(i => i.QRCodeContent).HasMaxLength(200);
        builder.Property(i => i.NIM).HasMaxLength(30);
        builder.Property(i => i.Counters).HasMaxLength(50);
        builder.Property(i => i.DeviceDateTime).HasMaxLength(30);

        builder.HasIndex(i => i.Type);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAt);

        builder.HasMany(i => i.Lines)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne(p => p.Invoice)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add to InvoiceConfiguration.Configure():

        builder.Property(i => i.AdvanceGroupId).HasMaxLength(50);
        builder.Property(i => i.TotalAdvancesPaid).HasPrecision(18, 2);
        builder.Property(i => i.RemainingBalance).HasPrecision(18, 2);

        builder.HasOne(i => i.ParentInvoice)
            .WithMany(i => i.ChildInvoices)
            .HasForeignKey(i => i.ParentInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.AdvanceGroupId);
    }
}