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

        // Relation avec la Commande (Order)
        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade); // Si on supprime la commande, on supprime ses lignes.

        // 🆕 Relation avec le MenuItem (Optionnel)
        builder.HasOne(oi => oi.MenuItem)
            .WithMany()
            .HasForeignKey(oi => oi.MenuItemId)
            .OnDelete(DeleteBehavior.SetNull); // Ne casse pas l'historique si on modifie la carte.

        // 🆕 Relation VITALE avec le Produit pour la facturation et le stock
        builder.HasOne(oi => oi.Product)
            .WithMany()
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict); // INTERDIT la suppression d'un produit s'il est lié à une commande. 

        builder.HasIndex(oi => oi.OrderId);
        builder.HasIndex(oi => oi.MenuItemId);
        builder.HasIndex(oi => oi.ProductId); // 🆕
    }
}