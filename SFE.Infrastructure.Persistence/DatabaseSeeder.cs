using Microsoft.EntityFrameworkCore;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // ══════════════ RÔLES ══════════════
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                // ── 1. Administrateur ──
                new Role
                {
                    Name = "Admin",
                    Permissions = """
                    {
                        "dashboard": true,
                        "pos": true,
                        "invoicing": true,
                        "clients": true,
                        "salesHistory": true,
                        "products": true,
                        "stock": true,
                        "transfers": true,
                        "loyalty": true,
                        "reports": true,
                        "settings": true,
                        "users": true
                    }
                    """
                },

                // ── 2. Gestionnaire ──
                new Role
                {
                    Name = "Gestionnaire",
                    Permissions = """
                    {
                        "dashboard": true,
                        "pos": true,
                        "invoicing": true,
                        "clients": true,
                        "salesHistory": true,
                        "products": true,
                        "stock": true,
                        "transfers": true,
                        "loyalty": true,
                        "reports": true,
                        "settings": false,
                        "users": false
                    }
                    """
                },

                // ── 3. Opérateur (caissier / agent de saisie) ──
                new Role
                {
                    Name = "Opérateur",
                    Permissions = """
                    {
                        "dashboard": false,
                        "pos": true,
                        "invoicing": true,
                        "clients": true,
                        "salesHistory": false,
                        "products": false,
                        "stock": false,
                        "transfers": false,
                        "loyalty": true,
                        "reports": false,
                        "settings": false,
                        "users": false
                    }
                    """
                },

                // ── 4. Inspecteur DGI (lecture seule / audit) ──
                new Role
                {
                    Name = "Inspecteur DGI",
                    Permissions = """
                    {
                        "dashboard": true,
                        "pos": false,
                        "invoicing": false,
                        "clients": false,
                        "salesHistory": true,
                        "products": false,
                        "stock": false,
                        "transfers": false,
                        "loyalty": false,
                        "reports": true,
                        "settings": false,
                        "users": false
                    }
                    """
                }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // ══════════════ UTILISATEUR ADMIN PAR DÉFAUT ══════════════
        if (!await context.Users.AnyAsync())
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");

            var admin = new User
            {
                Username = "admin",
                PasswordHash = AuthService.HashPassword("admin123"),
                FullName = "Administrateur Système",
                RoleId = adminRole.Id,
                IsActive = true
            };

            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
        }

        // ══════════════ ENTREPRISE PAR DÉFAUT ══════════════
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
        }
    }
}