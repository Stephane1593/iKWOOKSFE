using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.Name)
        .IsRequired()
        .HasMaxLength(200);

        builder.Property(oi => oi.UnitPrice)
        .HasPrecision(18, 2);

        builder.Property(oi => oi.LineTotal)
        .HasPrecision(18, 2);

        builder.Property(oi => oi.Quantity)
        .IsRequired();

        builder.Property(oi => oi.Notes)
        .HasMaxLength(1000);

        builder.HasOne(oi => oi.Order)
        .WithMany(o => o.Items)
        .HasForeignKey(oi => oi.OrderId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(oi => oi.OrderId);

        builder.HasIndex(oi => oi.MenuItemId);
    }
}