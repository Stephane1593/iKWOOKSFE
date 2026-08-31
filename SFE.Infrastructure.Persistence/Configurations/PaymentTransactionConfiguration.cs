using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions");
        b.HasKey(x => x.IdempotencyKey);          // idempotency enforced at the DB level
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.Property(x => x.OrderId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Method).HasMaxLength(32).IsRequired();
        b.Property(x => x.Amount).HasColumnType("TEXT"); // SQLite decimal-as-text, matches your other money cols
        b.Property(x => x.ProviderRef).HasMaxLength(64);
        b.Property(x => x.FailureReason).HasMaxLength(256);
        b.HasIndex(x => x.OrderId);
        b.Property(x => x.Attempts)
       .IsRequired()
       .HasDefaultValue(0);
    }
}