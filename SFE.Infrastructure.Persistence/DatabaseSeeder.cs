using Microsoft.EntityFrameworkCore;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Text.Json;

namespace SFE.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    // ══════════════════════════════════════════════════════════════════
    //  DEFAULT PERMISSION MATRIX
    //  Every role's JSON must include ALL keys (module access + authorize.*)
    //  so the role editor UI shows every checkbox with its correct state.
    // ══════════════════════════════════════════════════════════════════

    private const string SuperAdminPermissionsJson = """
        {
            "dashboard": true, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": true, "products": true, "stock": true, "transfers": true,
            "loyalty": true, "reports": true, "closeZ": true, "settings": true,
            "users": true, "audit": true, "bypassPosCheck": true,

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
            "authorize.reprintFiscalReceipt": true
        }
        """;

    private const string AdminPermissionsJson = """
        {
            "dashboard": true, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": true, "products": true, "stock": true, "transfers": true,
            "loyalty": true, "reports": true, "closeZ": true, "settings": true,
            "users": true, "audit": true, "bypassPosCheck": false,

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
            "authorize.reprintFiscalReceipt": true
        }
        """;

    private const string GestionnairePermissionsJson = """
        {
            "dashboard": true, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": true, "products": true, "stock": true, "transfers": true,
            "loyalty": true, "reports": true, "closeZ": true, "settings": false,
            "users": false, "audit": false, "bypassPosCheck": false,

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
            "authorize.reprintFiscalReceipt": true
        }
        """;

    private const string OperateurPermissionsJson = """
        {
            "dashboard": false, "pos": true, "invoicing": true, "clients": true,
            "salesHistory": false, "products": false, "stock": false, "transfers": false,
            "loyalty": true, "reports": false, "closeZ": true, "settings": false,
            "users": false, "audit": false, "bypassPosCheck": false,

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
            "authorize.reprintFiscalReceipt": false
        }
        """;

    private const string InspecteurDGIPermissionsJson = """
        {
            "dashboard": true, "pos": false, "invoicing": false, "clients": false,
            "salesHistory": true, "products": false, "stock": false, "transfers": false,
            "loyalty": false, "reports": true, "closeZ": false, "settings": false,
            "users": false, "audit": true, "bypassPosCheck": false,

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
            "authorize.reprintFiscalReceipt": false
        }
        """;

    private const string ITTechPermissionsJson = """
        {
            "dashboard": true, "pos": false, "invoicing": false, "clients": false,
            "salesHistory": false, "products": true, "stock": true, "transfers": false,
            "loyalty": false, "reports": false, "closeZ": false, "settings": true,
            "users": true, "audit": true, "bypassPosCheck": true,

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
            "authorize.reprintFiscalReceipt": false
        }
        """;

    // Which authorize.* keys exist. Used by the backfill to know what to add.
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
    };

    // Per-role default for the backfill (role name → dict of authorize.* → bool).
    // Only used to seed keys that don't yet exist on an existing role.
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
            },
        };

    private static Dictionary<string, bool> AllTrue()
        => AuthorizeKeys.ToDictionary(k => k.Key, _ => true);

    // ══════════════════════════════════════════════════════════════════
    //  SEED (public entry)
    // ══════════════════════════════════════════════════════════════════

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

        // ⭐ Runs on every startup — idempotent. Adds missing authorize.* keys
        // to existing roles without touching any pre-existing values.
        await BackfillAuthorizationPermissionsAsync(context);
    }

    // ══════════════════════════════════════════════════════════════════
    //  BACKFILL — adds authorize.* keys to existing roles in prod DBs
    //  without overwriting any value the operator has already set.
    // ══════════════════════════════════════════════════════════════════

    public static async Task BackfillAuthorizationPermissionsAsync(AppDbContext context)
    {
        var roles = await context.Roles.ToListAsync();
        bool anyChanged = false;

        foreach (var role in roles)
        {
            Dictionary<string, bool> current;
            try
            {
                current = JsonSerializer.Deserialize<Dictionary<string, bool>>(
                              role.Permissions ?? "{}")
                          ?? new();
            }
            catch { current = new(); }

            // Pick the right defaults for this role — fall back to "all false"
            // for unknown role names (safest for custom roles the operator created).
            AuthorizeDefaultsByRole.TryGetValue(role.Name, out var defaults);
            defaults ??= AuthorizeKeys.ToDictionary(k => k.Key, k => k.DefaultValue);

            bool roleChanged = false;
            foreach (var (key, _) in AuthorizeKeys)
            {
                if (current.ContainsKey(key)) continue;      // never overwrite
                current[key] = defaults.TryGetValue(key, out var d) && d;
                roleChanged = true;
            }

            if (roleChanged)
            {
                role.Permissions = JsonSerializer.Serialize(current);
                anyChanged = true;
            }
        }

        if (anyChanged) await context.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════════════
    //  SUPERADMIN — unchanged logic, just uses the new JSON constant.
    // ══════════════════════════════════════════════════════════════════

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