using Microsoft.EntityFrameworkCore;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace SFE.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    /// <summary>
    /// Crée la base de données et insère les données initiales si vide.
    /// Appelé une seule fois au démarrage de l'application.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context)
    {
        // Créer la base de données et appliquer le schéma
        await context.Database.EnsureCreatedAsync();

        // --- Rôles ---
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role
                {
                    Name = "Administrateur",
                    Permissions = """
                    {
                        "invoicing": true,
                        "products": true,
                        "clients": true,
                        "stock": true,
                        "loyalty": true,
                        "reports": true,
                        "cash": true,
                        "settings": true,
                        "users": true
                    }
                    """
                },
                new Role
                {
                    Name = "Caissier",
                    Permissions = """
                    {
                        "invoicing": true,
                        "products": false,
                        "clients": true,
                        "stock": false,
                        "loyalty": true,
                        "reports": false,
                        "cash": true,
                        "settings": false,
                        "users": false
                    }
                    """
                },
                new Role
                {
                    Name = "Gestionnaire",
                    Permissions = """
                    {
                        "invoicing": true,
                        "products": true,
                        "clients": true,
                        "stock": true,
                        "loyalty": true,
                        "reports": true,
                        "cash": false,
                        "settings": false,
                        "users": false
                    }
                    """
                }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // --- Utilisateur Admin par défaut ---
        if (!await context.Users.AnyAsync())
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Administrateur");

            var admin = new User
            {
                Username = "admin",
                PasswordHash = HashPassword("admin123"), // TODO: Changer en production !
                FullName = "Administrateur Système",
                RoleId = adminRole.Id,
                IsActive = true
            };

            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
        }

        // --- Entreprise par défaut ---
        if (!await context.Companies.AnyAsync())
        {
            var company = new Company
            {
                Name = "Assium",
                NIF = "Z9090232",
                RCCM = "RCCM1223123",
                Address = "Boulevard",
                City = "Kinshasa",
                Phone = "0818105702",
                Email = "assium@gmail.com",
                DefaultPriceMode = PriceMode.TTC,
                LoyaltyEnabled = false,
                LoyaltyEarnRate = 1000m,
                LoyaltyRedeemRate = 500m,
                DeploymentMode = DeploymentMode.Standalone
            };

            await context.Companies.AddAsync(company);
            await context.SaveChangesAsync();

            // POS par défaut
            //var pos = new PointOfSale
            //{
            //    CompanyId = company.Id,
            //    Code = "POS-001",
            //    Name = "Point de vente principal",
            //    Address = "",
            //    City = "Kinshasa",
            //    Phone = "",
            //    IsActive = true,
            //    DeviceType = DeviceType.EMcf,
            //    EmcfApiUrl = "",
            //    EmcfToken = "",
            //    EmcfNIM = "",
            //    McfBaudRate = 115200
            //};

            //await context.PointsOfSale.AddAsync(pos);
            //await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Hash simple SHA256 pour le mot de passe.
    /// En production, utiliser BCrypt ou Argon2.
    /// </summary>
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}