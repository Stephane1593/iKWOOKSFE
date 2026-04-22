// SFE.Infrastructure/Persistence/DatabaseSeeder.cs
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
                // ── 0. SuperAdmin (protégé — ne peut être ni supprimé ni modifié) ──
                new Role
                {
                    Name = "SuperAdmin",
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
                        "users": true,
                        "bypassPosCheck": true
                    }
                    """
                },

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
                        "users": true,
                        "bypassPosCheck": false
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
                        "users": false,
                        "bypassPosCheck": false
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
                        "users": false,
                        "bypassPosCheck": false
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
                        "users": false,
                        "bypassPosCheck": false
                    }
                    """
                },

                // ── 5. IT Tech (configuration technique / accès sans POS) ──
                new Role
                {
                    Name = "IT Tech",
                    Permissions = """
                    {
                        "dashboard": true,
                        "pos": false,
                        "invoicing": false,
                        "clients": false,
                        "salesHistory": false,
                        "products": true,
                        "stock": true,
                        "transfers": false,
                        "loyalty": false,
                        "reports": false,
                        "settings": true,
                        "users": true,
                        "bypassPosCheck": true
                    }
                    """
                }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // ══════════════ SUPER-ADMIN GARANTI (idempotent) ══════════════
        await EnsureSuperAdminAsync(context);

        // ══════════════ UTILISATEURS PAR DÉFAUT ══════════════
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            var itTechRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "IT Tech");

            var defaultUsers = new List<User>();

            if (adminRole != null)
            {
                defaultUsers.Add(new User
                {
                    Username = "admin",
                    PasswordHash = AuthService.HashPassword("admin123"),
                    FullName = "Administrateur Système",
                    RoleId = adminRole.Id,
                    IsActive = true
                });
            }

            if (itTechRole != null)
            {
                defaultUsers.Add(new User
                {
                    Username = "tech",
                    PasswordHash = AuthService.HashPassword("tech123"),
                    FullName = "Technicien IT",
                    RoleId = itTechRole.Id,
                    IsActive = true
                });
            }

            if (defaultUsers.Count > 0)
            {
                await context.Users.AddRangeAsync(defaultUsers);
                await context.SaveChangesAsync();
            }
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

    /// <summary>
    /// Ensures the SuperAdmin role + superadmin user exist.
    /// Runs on EVERY startup (idempotent) — works for both new and existing databases.
    /// </summary>
    public static async Task EnsureSuperAdminAsync(AppDbContext context)
    {
        // ── 1. Guarantee SuperAdmin role ──
        var saRole = await context.Roles.FirstOrDefaultAsync(
            r => r.Name == UserService.SuperAdminRoleName);

        if (saRole == null)
        {
            saRole = new Role
            {
                Name = UserService.SuperAdminRoleName,
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
                    "users": true,
                    "bypassPosCheck": true
                }
                """
            };
            await context.Roles.AddAsync(saRole);
            await context.SaveChangesAsync();
        }
        else
        {
            // Ensure permissions are always ALL true (self-healing)
            var expected = """
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
                "users": true,
                "bypassPosCheck": true
            }
            """;
            if (saRole.Permissions != expected)
            {
                saRole.Permissions = expected;
                await context.SaveChangesAsync();
            }
        }

        // ── 2. Guarantee superadmin user ──
        var saUser = await context.Users.FirstOrDefaultAsync(
            u => u.Username == UserService.SuperAdminUsername);

        if (saUser == null)
        {
            saUser = new User
            {
                Username = UserService.SuperAdminUsername,
                PasswordHash = AuthService.HashPassword("superadmin"),
                FullName = "Super Administrateur",
                RoleId = saRole.Id,
                IsActive = true
            };
            await context.Users.AddAsync(saUser);
            await context.SaveChangesAsync();
        }
        else
        {
            // Self-healing: ensure role is always SuperAdmin and active
            bool changed = false;
            if (saUser.RoleId != saRole.Id) { saUser.RoleId = saRole.Id; changed = true; }
            if (!saUser.IsActive) { saUser.IsActive = true; changed = true; }
            if (changed) await context.SaveChangesAsync();
        }
    }
}