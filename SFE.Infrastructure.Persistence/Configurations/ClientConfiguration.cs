using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Type)
            .HasConversion<int>();

        builder.Property(c => c.NIF)
            .HasMaxLength(50);

        builder.HasIndex(c => c.NIF);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.Address)
            .HasMaxLength(300);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.Property(c => c.RCCM)
            .HasMaxLength(100);

        builder.HasIndex(c => c.Name);

        // Relation : un Client a un LoyaltyAccount (optionnel)
        builder.HasOne(c => c.LoyaltyAccount)
            .WithOne(la => la.Client)
            .HasForeignKey<LoyaltyAccount>(la => la.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}