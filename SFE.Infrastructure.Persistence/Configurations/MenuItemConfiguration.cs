using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");

        builder.HasKey(mi => mi.Id);

        builder.Property(mi => mi.Code)
        .HasMaxLength(100);

        builder.Property(mi => mi.Name)
        .IsRequired()
        .HasMaxLength(200);

        builder.Property(mi => mi.Description)
        .HasMaxLength(1000);

        builder.Property(mi => mi.UnitPrice)
        .HasPrecision(18, 2);

        builder.Property(mi => mi.IsAvailable)
        .HasDefaultValue(true);

        builder.HasOne(mi => mi.Menu)
        .WithMany(m => m.Items)
        .HasForeignKey(mi => mi.MenuId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mi => mi.MenuId);

        // Product code lookups
        builder.HasIndex(mi => new { mi.MenuId, mi.Code });

        builder.HasIndex(mi => mi.Name);
    }
}