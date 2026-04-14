using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("LoyaltyTransactions");

        builder.HasKey(lt => lt.Id);

        builder.Property(lt => lt.Type)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(lt => lt.Description)
            .HasMaxLength(500);

        builder.HasIndex(lt => lt.Timestamp);
    }
}