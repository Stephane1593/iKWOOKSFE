using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
        .IsRequired()
        .HasMaxLength(200);

        builder.Property(m => m.Description)
        .HasMaxLength(1000);

        builder.HasOne(m => m.Restaurant)
        .WithMany(r => r.Menus)
        .HasForeignKey(m => m.RestaurantId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Items)
        .WithOne(i => i.Menu)
        .HasForeignKey(i => i.MenuId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.RestaurantId);
        builder.HasIndex(m => new { m.RestaurantId, m.Name });
    }
}