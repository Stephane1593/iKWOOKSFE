using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("InvoicePayments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentType).HasConversion<int>();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.CurrencyCode).HasMaxLength(10);
        builder.Property(p => p.CurrencyRate).HasPrecision(18, 4);
    }
}