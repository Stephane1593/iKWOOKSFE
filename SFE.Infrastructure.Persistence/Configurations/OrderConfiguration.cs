using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Table name
        builder.ToTable("Orders");

        // Primary key / base SyncableRootEntity pattern uses Id
        builder.HasKey(o => o.Id);

        // Relations
        builder.HasMany(o => o.Items)
               .WithOne(i => i.Order)
               .HasForeignKey(i => i.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);

        builder.Property(o => o.DiningOption).HasConversion<int>();
        builder.Property(o => o.DeliveryAddress).HasMaxLength(300);

        // 🚨 NOUVEAU: Limit the WaiterName string length for DB optimization
        builder.Property(o => o.WaiterName).HasMaxLength(100);

        // Timestamps stored via the global DateTimeOffset converters already present in AppDbContext
    }
}