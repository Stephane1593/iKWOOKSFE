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

        // ── Identité ──
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

        // ── Commentaires ──
        builder.Property(i => i.CommentA).HasMaxLength(500);
        builder.Property(i => i.CommentB).HasMaxLength(500);
        builder.Property(i => i.CommentC).HasMaxLength(500);
        builder.Property(i => i.CommentD).HasMaxLength(500);
        builder.Property(i => i.CommentE).HasMaxLength(500);
        builder.Property(i => i.CommentF).HasMaxLength(500);
        builder.Property(i => i.CommentG).HasMaxLength(500);
        builder.Property(i => i.CommentH).HasMaxLength(500);

        // ── Totaux ──
        builder.Property(i => i.TotalHT).HasPrecision(18, 2);
        builder.Property(i => i.TotalTVA).HasPrecision(18, 2);
        builder.Property(i => i.TotalTTC).HasPrecision(18, 2);
        builder.Property(i => i.TotalSpecificTax).HasPrecision(18, 2);
        builder.Property(i => i.TotalHTBeforeDiscount).HasPrecision(18, 2);
        builder.Property(i => i.TotalDiscount).HasPrecision(18, 2);
        builder.Property(i => i.TotalFixedSpecificTax).HasPrecision(18, 2);
        builder.Property(i => i.TotalPercentSpecificTax).HasPrecision(18, 2);

        // ── Sécurité fiscale ──
        builder.Property(i => i.EmcfUid).HasMaxLength(50);
        builder.Property(i => i.CodeDEFDGI).HasMaxLength(50);
        builder.Property(i => i.QRCodeContent).HasMaxLength(200);
        builder.Property(i => i.NIM).HasMaxLength(30);
        builder.Property(i => i.Counters).HasMaxLength(50);
        builder.Property(i => i.DeviceDateTime).HasMaxLength(30);

        // ── Index ──
        builder.HasIndex(i => i.Type);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAt);

        // ── Relations Lines / Payments ──
        builder.HasMany(i => i.Lines)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne(p => p.Invoice)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ══════════════════════════════════════════════════════
        //  ACOMPTE / SOLDE — Chaîne d'avances
        // ══════════════════════════════════════════════════════

        builder.Property(i => i.AdvanceGroupId).HasMaxLength(50);

        // 🆕 Nouveaux champs Phase 1
        builder.Property(i => i.OrderTotal).HasPrecision(18, 2);
        builder.Property(i => i.AdvanceAmount).HasPrecision(18, 2);

        builder.Property(i => i.TotalAdvancesPaid).HasPrecision(18, 2);
        builder.Property(i => i.RemainingBalance).HasPrecision(18, 2);

        builder.HasOne(i => i.ParentInvoice)
            .WithMany(i => i.ChildInvoices)
            .HasForeignKey(i => i.ParentInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.AdvanceGroupId);

        // Advance chain
        builder.Property(i => i.AdvanceGroupId).HasMaxLength(40);
        builder.Property(i => i.OrderTotal).HasPrecision(18, 2);
        builder.Property(i => i.PreviousAdvancesTotal).HasPrecision(18, 2);
        builder.Property(i => i.AdvanceAmount).HasPrecision(18, 2);
        builder.Property(i => i.RemainingAfterAdvance).HasPrecision(18, 2);

        builder.HasIndex(i => i.AdvanceGroupId);

        // Proforma → FV self-reference
        builder.HasOne(i => i.ConvertedToInvoice)
               .WithMany()
               .HasForeignKey(i => i.ConvertedToInvoiceId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.ConvertedToInvoiceId);


        // CREDIT NOTE (FA/EA → Original FV)
        builder.HasOne(i => i.OriginalInvoice)
               .WithMany()
               .HasForeignKey(i => i.OriginalInvoiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.OriginalInvoiceId);

        // PROFORMA → Invoice that was created from it
        builder.HasOne(i => i.SourceProforma)
               .WithMany()
               .HasForeignKey(i => i.SourceProformaId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.SourceProformaId);
        builder.HasIndex(i => new { i.Type, i.ConvertedToInvoiceId })
               .HasFilter("[Type] = 6");

        builder.Property(i => i.ProformaValidUntil).IsRequired(false);

        builder.Property(i => i.PrintCount).HasDefaultValue(0);
        builder.Property(i => i.FirstPrintedAt).IsRequired(false);
        builder.Property(i => i.LastPrintedAt).IsRequired(false);
    }
}