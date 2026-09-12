using Microsoft.EntityFrameworkCore;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SFE.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    private const string SuperAdminPermissionsJson = """
        {
            "dashboard": true, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": true, "products": true, "stock": true, "transfers": true,
            "loyalty": true, "reports": true, "closeZ": true, "settings": true,
            "users": true, "audit": true, "bypassPosCheck": true,
            "tables": true, "menus": true, "kitchen": true,

            "authorize.removeCartLine": true,
            "authorize.clearCart": true,
            "authorize.largeDiscount": true,
            "authorize.overridePrice": true,
            "authorize.cancelInvoice": true,
            "authorize.issueCreditNote": true,
            "authorize.reopenSession": true,
            "authorize.noSaleDrawer": true,
            "authorize.negativeStockSale": true,
            "authorize.deleteProduct": true,
            "authorize.changeExchangeRate": true,
            "authorize.reprintFiscalReceipt": true,
            "authorize.transferTable": true,
            "authorize.splitBill": true
        }
        """;

    private const string AdminPermissionsJson = """
        {
            "dashboard": true, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": true, "products": true, "stock": true, "transfers": true,
            "loyalty": true, "reports": true, "closeZ": true, "settings": true,
            "users": true, "audit": true, "bypassPosCheck": false,
            "tables": true, "menus": true, "kitchen": true,

            "authorize.removeCartLine": true,
            "authorize.clearCart": true,
            "authorize.largeDiscount": true,
            "authorize.overridePrice": true,
            "authorize.cancelInvoice": true,
            "authorize.issueCreditNote": true,
            "authorize.reopenSession": true,
            "authorize.noSaleDrawer": true,
            "authorize.negativeStockSale": true,
            "authorize.deleteProduct": true,
            "authorize.changeExchangeRate": true,
            "authorize.reprintFiscalReceipt": true,
            "authorize.transferTable": true,
            "authorize.splitBill": true
        }
        """;

    private const string GestionnairePermissionsJson = """
        {
            "dashboard": true, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": true, "products": true, "stock": true, "transfers": true,
            "loyalty": true, "reports": true, "closeZ": true, "settings": false,
            "users": false, "audit": false, "bypassPosCheck": false,
            "tables": true, "menus": true, "kitchen": true,

            "authorize.removeCartLine": true,
            "authorize.clearCart": true,
            "authorize.largeDiscount": true,
            "authorize.overridePrice": true,
            "authorize.cancelInvoice": true,
            "authorize.issueCreditNote": true,
            "authorize.reopenSession": true,
            "authorize.noSaleDrawer": true,
            "authorize.negativeStockSale": true,
            "authorize.deleteProduct": false,
            "authorize.changeExchangeRate": false,
            "authorize.reprintFiscalReceipt": true,
            "authorize.transferTable": true,
            "authorize.splitBill": true
        }
        """;

    private const string OperateurPermissionsJson = """
        {
            "dashboard": false, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": false, "products": false, "stock": false, "transfers": false,
            "loyalty": true, "reports": false, "closeZ": true, "settings": false,
            "users": false, "audit": false, "bypassPosCheck": false,
            "tables": true, "menus": false, "kitchen": false,

            "authorize.removeCartLine": false,
            "authorize.clearCart": false,
            "authorize.largeDiscount": false,
            "authorize.overridePrice": false,
            "authorize.cancelInvoice": false,
            "authorize.issueCreditNote": false,
            "authorize.reopenSession": false,
            "authorize.noSaleDrawer": false,
            "authorize.negativeStockSale": false,
            "authorize.deleteProduct": false,
            "authorize.changeExchangeRate": false,
            "authorize.reprintFiscalReceipt": false,
            "authorize.transferTable": false,
            "authorize.splitBill": false
        }
        """;

    private const string InspecteurDGIPermissionsJson = """
        {
            "dashboard": true, "pos": false, "invoicing": false, "clients": false,
            "salesHistory": true, "products": false, "stock": false, "transfers": false,
            "loyalty": false, "reports": true, "closeZ": false, "settings": false,
            "users": false, "audit": true, "bypassPosCheck": false,
            "tables": false, "menus": false, "kitchen": false,

            "authorize.removeCartLine": false,
            "authorize.clearCart": false,
            "authorize.largeDiscount": false,
            "authorize.overridePrice": false,
            "authorize.cancelInvoice": false,
            "authorize.issueCreditNote": false,
            "authorize.reopenSession": false,
            "authorize.noSaleDrawer": false,
            "authorize.negativeStockSale": false,
            "authorize.deleteProduct": false,
            "authorize.changeExchangeRate": false,
            "authorize.reprintFiscalReceipt": false,
            "authorize.transferTable": false,
            "authorize.splitBill": false
        }
        """;

    private const string ITTechPermissionsJson = """
        {
            "dashboard": true, "pos": false, "invoicing": false, "clients": false,
            "salesHistory": false, "products": true, "stock": true, "transfers": false,
            "loyalty": false, "reports": false, "closeZ": false, "settings": true,
            "users": true, "audit": true, "bypassPosCheck": true,
            "tables": false, "menus": false, "kitchen": false,

            "authorize.removeCartLine": false,
            "authorize.clearCart": false,
            "authorize.largeDiscount": false,
            "authorize.overridePrice": false,
            "authorize.cancelInvoice": false,
            "authorize.issueCreditNote": false,
            "authorize.reopenSession": false,
            "authorize.noSaleDrawer": false,
            "authorize.negativeStockSale": false,
            "authorize.deleteProduct": false,
            "authorize.changeExchangeRate": false,
            "authorize.reprintFiscalReceipt": false,
            "authorize.transferTable": false,
            "authorize.splitBill": false
        }
        """;

    private static readonly (string Key, bool DefaultValue)[] AuthorizeKeys = new[]
    {
        ("authorize.removeCartLine",       false),
        ("authorize.clearCart",            false),
        ("authorize.largeDiscount",        false),
        ("authorize.overridePrice",        false),
        ("authorize.cancelInvoice",        false),
        ("authorize.issueCreditNote",      false),
        ("authorize.reopenSession",        false),
        ("authorize.noSaleDrawer",         false),
        ("authorize.negativeStockSale",    false),
        ("authorize.deleteProduct",        false),
        ("authorize.changeExchangeRate",   false),
        ("authorize.reprintFiscalReceipt", false),
        ("authorize.transferTable",        false),
        ("authorize.splitBill",            false),
    };

    private static readonly Dictionary<string, Dictionary<string, bool>> AuthorizeDefaultsByRole =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [UserService.SuperAdminRoleName] = AllTrue(),
            ["Admin"] = AllTrue(),
            ["Gestionnaire"] = new()
            {
                ["authorize.removeCartLine"] = true,
                ["authorize.clearCart"] = true,
                ["authorize.largeDiscount"] = true,
                ["authorize.overridePrice"] = true,
                ["authorize.cancelInvoice"] = true,
                ["authorize.issueCreditNote"] = true,
                ["authorize.reopenSession"] = true,
                ["authorize.noSaleDrawer"] = true,
                ["authorize.negativeStockSale"] = true,
                ["authorize.deleteProduct"] = false,
                ["authorize.changeExchangeRate"] = false,
                ["authorize.reprintFiscalReceipt"] = true,
                ["authorize.transferTable"] = true,
                ["authorize.splitBill"] = true,
            },
        };

    private static Dictionary<string, bool> AllTrue()
        => AuthorizeKeys.ToDictionary(k => k.Key, _ => true);

    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
        await EnsureSuperAdminAsync(context);

        if (!await context.Roles.AnyAsync(r => r.Name != UserService.SuperAdminRoleName))
        {
            var roles = new List<Role>
            {
                new Role { Name = "Admin",          Permissions = AdminPermissionsJson         },
                new Role { Name = "Gestionnaire",   Permissions = GestionnairePermissionsJson  },
                new Role { Name = "Opérateur",      Permissions = OperateurPermissionsJson     },
                new Role { Name = "Inspecteur DGI", Permissions = InspecteurDGIPermissionsJson },
                new Role { Name = "IT Tech",        Permissions = ITTechPermissionsJson        },
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        if (!await context.Companies.AnyAsync())
        {
            var company = new Company
            {
                Name = "Assium",
                NIF = "A1823910K",
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

        // ── AJOUT DES TABLES PAR DÉFAUT ──
        var tableSet = context.Set<Table>();
        if (!await tableSet.AnyAsync())
        {
            var firstCompany = await context.Companies.FirstOrDefaultAsync();
            int companyId = firstCompany?.Id ?? 1;

            var firstRestaurant = await context.Set<Restaurant>().FirstOrDefaultAsync();
            int restaurantId = firstRestaurant?.Id ?? 1;

            if (firstRestaurant == null)
            {
                firstRestaurant = new Restaurant { CompanyId = companyId, Name = "Mon Restaurant", IsActive = true };
                await context.Set<Restaurant>().AddAsync(firstRestaurant);
                await context.SaveChangesAsync();
                restaurantId = firstRestaurant.Id;
            }

            var defaultTables = new List<Table>();

            for (int i = 1; i <= 15; i++)
            {
                defaultTables.Add(new Table
                {
                    RestaurantId = restaurantId,
                    CompanyId = companyId,
                    Number = i,
                    Seats = i <= 5 ? 4 : (i <= 10 ? 2 : 6),
                    Status = TableStatus.Free
                });
            }

            await tableSet.AddRangeAsync(defaultTables);
            await context.SaveChangesAsync();
        }

        // ── AJOUT DES PROFILS D'IMPRESSION (KOT) PAR DÉFAUT ──
        var printerSet = context.Set<PrinterProfile>();
        if (!await printerSet.AnyAsync())
        {
            var firstCompany = await context.Companies.FirstOrDefaultAsync();
            int companyId = firstCompany?.Id ?? 1;

            await printerSet.AddRangeAsync(
                new PrinterProfile { CompanyId = companyId, Name = "Bar (Boissons)" },
                new PrinterProfile { CompanyId = companyId, Name = "Cuisine Chaude" },
                new PrinterProfile { CompanyId = companyId, Name = "Cuisine Froide (Entrées/Desserts)" }
            );
            await context.SaveChangesAsync();
        }

        // ── AJOUT DES CATÉGORIES DE MENUS PAR DÉFAUT ──
        var menuSet = context.Set<Menu>();
        if (!await menuSet.AnyAsync())
        {
            var firstCompany = await context.Companies.FirstOrDefaultAsync();
            int companyId = firstCompany?.Id ?? 1;

            var firstRestaurant = await context.Set<Restaurant>().FirstOrDefaultAsync();
            int restaurantId = firstRestaurant?.Id ?? 1;

            await menuSet.AddRangeAsync(
                new Menu { RestaurantId = restaurantId, CompanyId = companyId, Name = "Boissons" },
                new Menu { RestaurantId = restaurantId, CompanyId = companyId, Name = "Entrées" },
                new Menu { RestaurantId = restaurantId, CompanyId = companyId, Name = "Plats Principaux" },
                new Menu { RestaurantId = restaurantId, CompanyId = companyId, Name = "Desserts" }
            );
            await context.SaveChangesAsync();
        }

        await BackfillAuthorizationPermissionsAsync(context);
    }

    public static async Task BackfillAuthorizationPermissionsAsync(AppDbContext context)
    {
        var roles = await context.Roles.ToListAsync();
        bool anyChanged = false;

        foreach (var role in roles)
        {
            Dictionary<string, bool> current;
            try
            {
                current = JsonSerializer.Deserialize<Dictionary<string, bool>>(role.Permissions ?? "{}") ?? new();
            }
            catch { current = new(); }

            var baseKeys = new[] { "tables", "menus", "kitchen" };
            foreach (var bk in baseKeys)
            {
                if (!current.ContainsKey(bk))
                {
                    if (bk == "tables")
                    {
                        current[bk] = role.Name == "Admin" || role.Name == "Gestionnaire" || role.Name == "Opérateur" || role.Name == UserService.SuperAdminRoleName;
                    }
                    else
                    {
                        current[bk] = role.Name == "Admin" || role.Name == "Gestionnaire" || role.Name == UserService.SuperAdminRoleName;
                    }
                    anyChanged = true;
                }
            }

            AuthorizeDefaultsByRole.TryGetValue(role.Name, out var defaults);
            defaults ??= AuthorizeKeys.ToDictionary(k => k.Key, k => k.DefaultValue);

            bool roleChanged = false;
            foreach (var (key, _) in AuthorizeKeys)
            {
                if (current.ContainsKey(key)) continue;
                current[key] = defaults.TryGetValue(key, out var d) && d;
                roleChanged = true;
            }

            if (roleChanged || anyChanged)
            {
                role.Permissions = JsonSerializer.Serialize(current);
                anyChanged = true;
            }
        }

        if (anyChanged) await context.SaveChangesAsync();
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
                PasswordHash = AuthService.HashPassword("super_*admin"),
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
            ("admin", "admin_*123", "Administrateur Système", adminRole),
            ("tech",  "tech_*123",  "Technicien IT",          itTechRole),
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