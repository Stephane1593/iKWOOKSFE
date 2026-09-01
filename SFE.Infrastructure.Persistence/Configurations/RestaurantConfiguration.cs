using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
        .IsRequired()
        .HasMaxLength(200);

        builder.Property(r => r.Address)
        .HasMaxLength(500);

        builder.Property(r => r.Phone)
        .HasMaxLength(50);

        builder.Property(r => r.IsActive)
        .HasDefaultValue(true);

        builder.HasMany(r => r.Menus)
        .WithOne(m => m.Restaurant)
        .HasForeignKey(m => m.RestaurantId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Tables)
        .WithOne(t => t.Restaurant)
        .HasForeignKey(t => t.RestaurantId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.Name);
    }
}