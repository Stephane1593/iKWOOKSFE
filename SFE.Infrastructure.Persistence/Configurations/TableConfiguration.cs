using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.ToTable("Tables");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Number)
        .IsRequired();

        builder.Property(t => t.Seats)
        .IsRequired();

        builder.Property(t => t.Status)
        .HasConversion<string>()
        .HasMaxLength(32);

        builder.HasOne(t => t.Restaurant)
        .WithMany(r => r.Tables)
        .HasForeignKey(t => t.RestaurantId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.RestaurantId);

        // Table number must be unique within a restaurant
        builder.HasIndex(t => new { t.RestaurantId, t.Number })
        .IsUnique();

        builder.HasIndex(t => t.Status);
    }
}