using Microsoft.EntityFrameworkCore;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    private const string SuperAdminPermissionsJson = """
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
            "audit": true,
            "bypassPosCheck": true
        }
        """;

    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        await EnsureSuperAdminAsync(context);

        if (!await context.Roles.AnyAsync(r => r.Name != UserService.SuperAdminRoleName))
        {
            var roles = new List<Role>
            {
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
                        "audit": true,
                        "bypassPosCheck": false
                    }
                    """
                },
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
                        "audit": false,
                        "bypassPosCheck": false
                    }
                    """
                },
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
                        "audit": false,
                        "bypassPosCheck": false
                    }
                    """
                },
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
                        "audit": true,
                        "bypassPosCheck": false
                    }
                    """
                },
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
                        "audit": true,
                        "bypassPosCheck": true
                    }
                    """
                }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

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

        await EnsureDefaultUsersAsync(context);
    }

    public static async Task EnsureSuperAdminAsync(AppDbContext context)
    {
        var saRole = await context.Roles
            .FirstOrDefaultAsync(r => r.Name == UserService.SuperAdminRoleName);

        if (saRole is null)
        {
            saRole = new Role
            {
                Name = UserService.SuperAdminRoleName,
                Permissions = NormalizeJson(SuperAdminPermissionsJson)
            };
            await context.Roles.AddAsync(saRole);
            await context.SaveChangesAsync();
        }
        else
        {
            var expected = NormalizeJson(SuperAdminPermissionsJson);
            var current = NormalizeJson(saRole.Permissions);

            if (current != expected)
            {
                saRole.Permissions = expected;
                await context.SaveChangesAsync();
            }
        }

        var saUser = await context.Users
            .FirstOrDefaultAsync(u => u.Username == UserService.SuperAdminUsername);

        if (saUser is null)
        {
            saUser = new User
            {
                Username = UserService.SuperAdminUsername,
                PasswordHash = AuthService.HashPassword("superadmin"),
                FullName = "Super Administrateur",
                RoleId = saRole.Id,
                PointOfSaleId = null,
                IsActive = true
            };
            await context.Users.AddAsync(saUser);
            await context.SaveChangesAsync();
        }
        else
        {
            bool changed = false;
            if (saUser.RoleId != saRole.Id) { saUser.RoleId = saRole.Id; changed = true; }
            if (!saUser.IsActive) { saUser.IsActive = true; changed = true; }
            if (changed) await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureDefaultUsersAsync(AppDbContext context)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        var itTechRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "IT Tech");

        var seeds = new List<(string username, string password, string fullName, Role? role)>
        {
            ("admin", "admin123", "Administrateur Système", adminRole),
            ("tech",  "tech123",  "Technicien IT",          itTechRole),
        };

        bool added = false;

        foreach (var (username, password, fullName, role) in seeds)
        {
            if (role is null) continue;
            if (await context.Users.AnyAsync(u => u.Username == username)) continue;

            context.Users.Add(new User
            {
                Username = username,
                PasswordHash = AuthService.HashPassword(password),
                FullName = fullName,
                RoleId = role.Id,
                PointOfSaleId = null,
                IsActive = true
            });
            added = true;
        }

        if (added) await context.SaveChangesAsync();
    }

    private static string NormalizeJson(string json)
        => System.Text.RegularExpressions.Regex.Replace(json, @"\s+", "");
}