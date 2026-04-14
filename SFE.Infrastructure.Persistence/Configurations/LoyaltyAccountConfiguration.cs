using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.ToTable("LoyaltyAccounts");

        builder.HasKey(la => la.Id);

        builder.Property(la => la.CardNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(la => la.CardNumber)
            .IsUnique();

        builder.Property(la => la.TierLevel)
            .HasConversion<int>();

        builder.HasMany(la => la.Transactions)
            .WithOne(lt => lt.LoyaltyAccount)
            .HasForeignKey(lt => lt.LoyaltyAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}