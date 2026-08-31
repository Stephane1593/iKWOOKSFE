using System.Text.Json;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class UserService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ILicenseService _license;
    private static readonly char[] BarcodeAlphabet =
    "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToCharArray();

    // ═══════ CONSTANTES DE PROTECTION ═══════
    public const string SuperAdminUsername = "superadmin";
    public const string SuperAdminRoleName = "SuperAdmin";
    public const string ITTechRoleName = "IT Tech";
    public const string InspecteurDGIRoleName = "Inspecteur DGI";

    private static readonly HashSet<string> SuperAdminOnlyRoleNames =
        new(StringComparer.OrdinalIgnoreCase) { SuperAdminRoleName, ITTechRoleName };

    private static readonly HashSet<string> ElevatedRoleNames =
        new(StringComparer.OrdinalIgnoreCase) { InspecteurDGIRoleName };

    public static readonly List<(string Key, string Label)> AllPermissions = new()
{
    // ─── Accès aux modules ───
    ("dashboard",      "Tableau de bord"),
    ("pos",            "Point de vente (caisse)"),
    ("invoicing",      "Facturation"),
    ("clients",        "Clients"),
    ("salesHistory",   "Historique des ventes"),
    ("products",       "Produits"),
    ("stock",          "Stock"),
    ("transfers",      "Transferts stock"),
    ("loyalty",        "Programme fidélité"),
    ("reports",        "Rapports (X, A, historique)"),
    ("closeZ",         "Clôture Z (fin de session)"),
    ("settings",       "Paramètres"),
    ("users",          "Gestion utilisateurs"),
    ("audit",          "Journal d'audit"),
    ("bypassPosCheck", "Accès sans POS"),

    // ─── Autorisations manager (override caisse) ───
    ("authorize.removeCartLine",       "Autoriser : retirer une ligne du panier"),
    ("authorize.clearCart",            "Autoriser : vider le panier"),
    ("authorize.largeDiscount",        "Autoriser : remise supérieure au seuil"),
    ("authorize.overridePrice",        "Autoriser : modification de prix"),
    ("authorize.cancelInvoice",        "Autoriser : annulation de facture"),
    ("authorize.issueCreditNote",      "Autoriser : émission d'un avoir"),
    ("authorize.reopenSession",        "Autoriser : réouverture de session"),
    ("authorize.noSaleDrawer",         "Autoriser : ouverture tiroir sans vente"),
    ("authorize.negativeStockSale",    "Autoriser : vente en stock négatif"),
    ("authorize.deleteProduct",        "Autoriser : suppression de produit"),
    ("authorize.changeExchangeRate",   "Autoriser : modification taux de change"),
    ("authorize.reprintFiscalReceipt", "Autoriser : réimpression fiscale"),
};

    public UserService(IUnitOfWork unitOfWork, IAuditService audit, ILicenseService license)
    {
        _uow = unitOfWork;
        _audit = audit;
        _license = license;
    }

    // ═══════ QUERIES ═══════

    public async Task<List<User>> GetAllWithRolesAsync()
        => await _uow.Users.GetAllWithRolesAsync();

    public async Task<User?> GetByIdAsync(int id)
        => await _uow.Users.GetByIdAsync(id);

    public async Task<List<Role>> GetAllRolesAsync()
        => await _uow.GetRepository<Role>().GetAllAsync();

    public async Task<List<Role>> GetAssignableRolesAsync(User currentUser)
    {
        var allRoles = await _uow.GetRepository<Role>().GetAllAsync();
        var (isSA, isIT) = await ResolvePrivilegesAsync(currentUser.Id);

        return allRoles
            .Where(r =>
            {
                if (IsSuperAdminRole(r)) return false;
                if (SuperAdminOnlyRoleNames.Contains(r.Name) && !isSA) return false;
                if (ElevatedRoleNames.Contains(r.Name) && !isSA && !isIT) return false;
                return true;
            })
            .OrderBy(r => r.Name)
            .ToList();
    }

    public async Task<List<PointOfSale>> GetAllPointsOfSaleAsync()
        => await _uow.PointsOfSale.GetAllAsync();

    // ═══════ STATIC GUARDS ═══════

    public static bool IsSuperAdminUser(User user)
        => user.Username.Equals(SuperAdminUsername, StringComparison.OrdinalIgnoreCase);

    public static bool IsSuperAdminRole(Role role)
        => role.Name.Equals(SuperAdminRoleName, StringComparison.OrdinalIgnoreCase);

    public static bool IsITTechRole(Role role)
        => role.Name.Equals(ITTechRoleName, StringComparison.OrdinalIgnoreCase);

    public static bool IsInspecteurDGIRole(Role role)
        => role.Name.Equals(InspecteurDGIRoleName, StringComparison.OrdinalIgnoreCase);

    public static bool IsRestrictedRole(Role role)
        => SuperAdminOnlyRoleNames.Contains(role.Name)
        || ElevatedRoleNames.Contains(role.Name);

    public static bool IsSuperAdminOnlyRole(Role role)
        => SuperAdminOnlyRoleNames.Contains(role.Name);

    public static bool IsElevatedRole(Role role)
        => ElevatedRoleNames.Contains(role.Name);

    // ═══════ PRIVILEGE RESOLVER ═══════

    private async Task<(bool isSA, bool isIT)> ResolvePrivilegesAsync(int userId)
    {
        var cu = await _uow.Users.GetByIdAsync(userId);
        if (cu == null) return (false, false);
        if (IsSuperAdminUser(cu)) return (true, false);
        var cuRole = await _uow.GetRepository<Role>().GetByIdAsync(cu.RoleId);
        return (false, cuRole != null && IsITTechRole(cuRole));
    }

    // ═══════ USER-LEVEL AUTHORIZATION ═══════

    private async Task<string?> AuthorizeUserManagementAsync(int currentUserId, Role targetRole)
    {
        if (IsSuperAdminRole(targetRole))
            return "Le rôle SuperAdmin ne peut pas être attribué manuellement.";

        var (isSA, isIT) = await ResolvePrivilegesAsync(currentUserId);

        if (IsSuperAdminOnlyRole(targetRole) && !isSA)
            return $"Seul le SuperAdmin peut gérer les utilisateurs avec le rôle « {targetRole.Name} ».";

        if (IsElevatedRole(targetRole) && !isSA && !isIT)
            return $"Seul le SuperAdmin ou un IT Tech peut gérer les utilisateurs avec le rôle « {targetRole.Name} ».";

        return null;
    }

    // ═══════ USER CRUD ═══════

    public async Task<ServiceResult> CreateUserAsync(User user, string plainPassword, int currentUserId)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            return ServiceResult.Fail("Le nom d'utilisateur est obligatoire.");

        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 4)
            return ServiceResult.Fail("Le mot de passe doit contenir au moins 4 caractères.");

        if (string.IsNullOrWhiteSpace(user.FullName))
            return ServiceResult.Fail("Le nom complet est obligatoire.");

        if (user.RoleId <= 0)
            return ServiceResult.Fail("Veuillez sélectionner un rôle.");

        if (user.Username.Equals(SuperAdminUsername, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Le nom d'utilisateur « superadmin » est réservé au système.");

        var existing = await _uow.Users.GetByUsernameAsync(user.Username.Trim());
        if (existing != null)
            return ServiceResult.Fail($"Le nom d'utilisateur « {user.Username} » est déjà pris.");

        var targetRole = await _uow.GetRepository<Role>().GetByIdAsync(user.RoleId);
        if (targetRole == null)
            return ServiceResult.Fail("Rôle introuvable.");

        var authError = await AuthorizeUserManagementAsync(currentUserId, targetRole);
        if (authError != null)
            return ServiceResult.Fail(authError);

        // -- LIMITE DE LICENCE : nombre d'utilisateurs --
        var maxUsers = _license.MaxUsers;
        if (maxUsers > 0)
        {
            var allUsers = await _uow.Users.GetAllAsync();
            var seatCount = allUsers.Count(u => !IsSuperAdminUser(u));
            if (seatCount >= maxUsers)
                return ServiceResult.Fail(
                    $"Limite de licence atteinte : votre licence autorise {maxUsers} utilisateur(s). " +
                    "Supprimez un utilisateur existant ou mettez votre licence à niveau.");
        }

        user.Username = user.Username.Trim().ToLower();
        user.FullName = user.FullName.Trim();
        user.PasswordHash = AuthService.HashPassword(plainPassword);
        user.IsActive = true;

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserCreated, user.Id.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        await _audit.LogAsync(
            AuditAction.UserCreated,
            AuditModule.Users,
            $"Utilisateur « {user.Username} » ({user.FullName}) · Rôle « {targetRole.Name} »" +
                (user.PointOfSaleId.HasValue ? $" · POS #{user.PointOfSaleId}" : ""),
            entityType: "User",
            entityId: user.Id.ToString());

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateUserAsync(User user, int currentUserId, string? newPlainPassword = null)
    {
        var existing = await _uow.Users.GetByUsernameAsync(user.Username);
        if (existing == null)
            existing = await _uow.Users.GetByIdAsync(user.Id);
        if (existing == null)
            return ServiceResult.Fail("Utilisateur introuvable.");

        // ── SuperAdmin: only themselves ──
        if (IsSuperAdminUser(existing))
        {
            if (currentUserId != existing.Id)
                return ServiceResult.Fail("Seul le SuperAdmin peut modifier son propre compte.");
            if (user.RoleId != existing.RoleId)
                return ServiceResult.Fail("Le rôle du SuperAdmin ne peut pas être modifié.");
            if (!user.IsActive)
                return ServiceResult.Fail("Le compte SuperAdmin ne peut pas être désactivé.");
            if (!user.Username.Equals(existing.Username, StringComparison.OrdinalIgnoreCase))
                return ServiceResult.Fail("Le nom d'utilisateur du SuperAdmin ne peut pas être modifié.");
        }

        var existingRole = await _uow.GetRepository<Role>().GetByIdAsync(existing.RoleId);
        if (existingRole != null && IsRestrictedRole(existingRole) && !IsSuperAdminRole(existingRole))
        {
            var authError = await AuthorizeUserManagementAsync(currentUserId, existingRole);
            if (authError != null)
                return ServiceResult.Fail(authError);
        }

        if (string.IsNullOrWhiteSpace(user.FullName))
            return ServiceResult.Fail("Le nom complet est obligatoire.");
        if (user.RoleId <= 0)
            return ServiceResult.Fail("Veuillez sélectionner un rôle.");

        string? newRoleName = null;
        if (user.RoleId != existing.RoleId)
        {
            var newRole = await _uow.GetRepository<Role>().GetByIdAsync(user.RoleId);
            if (newRole == null)
                return ServiceResult.Fail("Rôle introuvable.");

            var authError = await AuthorizeUserManagementAsync(currentUserId, newRole);
            if (authError != null)
                return ServiceResult.Fail(authError);

            newRoleName = newRole.Name;
        }

        if (!existing.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
        {
            var dup = await _uow.Users.GetByUsernameAsync(user.Username.Trim());
            if (dup != null)
                return ServiceResult.Fail($"Le nom d'utilisateur « {user.Username} » est déjà pris.");
        }

        var changes = new List<string>();
        if (existing.FullName != user.FullName.Trim())
            changes.Add($"Nom : « {existing.FullName} » → « {user.FullName.Trim()} »");
        if (existing.RoleId != user.RoleId)
            changes.Add($"Rôle : « {existingRole?.Name} » → « {newRoleName} »");
        if (existing.IsActive != user.IsActive)
            changes.Add(user.IsActive ? "Réactivé" : "Désactivé");
        if (existing.PointOfSaleId != user.PointOfSaleId)
            changes.Add($"POS : #{existing.PointOfSaleId} → #{user.PointOfSaleId}");
        if (!string.IsNullOrWhiteSpace(newPlainPassword))
            changes.Add("Mot de passe modifié");

        // 🆕 Auto-revoke any manager card if role changes or user is being deactivated.
        bool autoRevokeCard =
            existing.ManagerBarcodeHash != null &&
            (existing.RoleId != user.RoleId || (existing.IsActive && !user.IsActive));

        if (autoRevokeCard)
            changes.Add("Carte manager révoquée (contexte modifié)");

        existing.FullName = user.FullName.Trim();
        existing.RoleId = user.RoleId;
        existing.IsActive = user.IsActive;
        existing.PointOfSaleId = user.PointOfSaleId;

        if (autoRevokeCard)
            existing.ManagerBarcodeHash = null;

        if (!string.IsNullOrWhiteSpace(newPlainPassword))
        {
            if (newPlainPassword.Length < 4)
                return ServiceResult.Fail("Le mot de passe doit contenir au moins 4 caractères.");
            existing.PasswordHash = AuthService.HashPassword(newPlainPassword);
        }

        await _uow.Users.UpdateAsync(existing);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserUpdated, existing.Id.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        var detail = changes.Count > 0
            ? $"Utilisateur « {existing.Username} » · {string.Join(" · ", changes)}"
            : $"Utilisateur « {existing.Username} » · Aucune modification détectée";
        await _audit.LogAsync(
            AuditAction.UserUpdated,
            AuditModule.Users,
            detail,
            entityType: "User",
            entityId: existing.Id.ToString());

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteUserAsync(int userId, int currentUserId)
    {
        if (userId == currentUserId)
            return ServiceResult.Fail("Vous ne pouvez pas supprimer votre propre compte.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null)
            return ServiceResult.Fail("Utilisateur introuvable.");

        if (IsSuperAdminUser(user))
            return ServiceResult.Fail("Le compte SuperAdmin ne peut pas être supprimé.");

        var userRole = await _uow.GetRepository<Role>().GetByIdAsync(user.RoleId);
        if (userRole != null && IsRestrictedRole(userRole))
        {
            var authError = await AuthorizeUserManagementAsync(currentUserId, userRole);
            if (authError != null)
                return ServiceResult.Fail(authError);
        }

        var username = user.Username;
        var fullName = user.FullName;
        var roleName = userRole?.Name ?? "?";

        await _uow.Users.DeleteAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserDeleted, userId.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        await _audit.LogAsync(
            AuditAction.UserDeleted,
            AuditModule.Users,
            $"Utilisateur « {username} » ({fullName}) · Rôle « {roleName} » · Supprimé",
            entityType: "User",
            entityId: userId.ToString());

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ToggleActiveAsync(int userId, int currentUserId)
    {
        if (userId == currentUserId)
            return ServiceResult.Fail("Vous ne pouvez pas désactiver votre propre compte.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null)
            return ServiceResult.Fail("Utilisateur introuvable.");

        if (IsSuperAdminUser(user))
            return ServiceResult.Fail("Le compte SuperAdmin ne peut pas être désactivé.");

        var userRole = await _uow.GetRepository<Role>().GetByIdAsync(user.RoleId);
        if (userRole != null && IsRestrictedRole(userRole))
        {
            var authError = await AuthorizeUserManagementAsync(currentUserId, userRole);
            if (authError != null)
                return ServiceResult.Fail(authError);
        }

        user.IsActive = !user.IsActive;
        // 🆕 If we just deactivated the user, revoke the manager card too.
        if (!user.IsActive && user.ManagerBarcodeHash != null)
            user.ManagerBarcodeHash = null;

        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserUpdated, userId.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        var action = user.IsActive ? AuditAction.UserActivated : AuditAction.UserDeactivated;
        await _audit.LogAsync(
            action,
            AuditModule.Users,
            $"Utilisateur « {user.Username} » ({user.FullName}) · " +
                (user.IsActive ? "Activé" : "Désactivé"),
            entityType: "User",
            entityId: userId.ToString());

        return ServiceResult.Ok();
    }

    // ═══════ ROLE CRUD ═══════

    public async Task<ServiceResult> CreateRoleAsync(
        string name, Dictionary<string, bool> permissions, int currentUserId)
    {
        var (isSA, _) = await ResolvePrivilegesAsync(currentUserId);
        if (!isSA)
            return ServiceResult.Fail("Seul le SuperAdmin peut créer de nouveaux rôles.");

        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult.Fail("Le nom du rôle est obligatoire.");

        if (name.Equals(SuperAdminRoleName, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Le nom « SuperAdmin » est réservé au système.");

        var roleRepo = _uow.GetRepository<Role>();
        var existing = await roleRepo.FindAsync(r => r.Name == name.Trim());
        if (existing.Any())
            return ServiceResult.Fail($"Le rôle « {name} » existe déjà.");

        var enabledPerms = permissions
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        var role = new Role
        {
            Name = name.Trim(),
            Permissions = JsonSerializer.Serialize(permissions)
        };

        await roleRepo.AddAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleCreated, role.Id.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        await _audit.LogAsync(
            AuditAction.RoleCreated,
            AuditModule.Users,
            $"Rôle « {role.Name} » · Permissions : {(enabledPerms.Count > 0 ? string.Join(", ", enabledPerms) : "aucune")}",
            entityType: "Role",
            entityId: role.Id.ToString());

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateRoleAsync(
        int roleId, string name, Dictionary<string, bool> permissions, int currentUserId)
    {
        var roleRepo = _uow.GetRepository<Role>();
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role == null)
            return ServiceResult.Fail("Rôle introuvable.");

        var (isSA, _) = await ResolvePrivilegesAsync(currentUserId);
        if (!isSA)
            return ServiceResult.Fail("Seul le SuperAdmin peut modifier les rôles.");

        if (IsSuperAdminRole(role))
            return ServiceResult.Fail("Le rôle SuperAdmin ne peut pas être modifié.");

        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult.Fail("Le nom du rôle est obligatoire.");

        if (name.Equals(SuperAdminRoleName, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Le nom « SuperAdmin » est réservé au système.");

        if (!role.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var dup = await roleRepo.FindAsync(r => r.Name == name.Trim());
            if (dup.Any(r => r.Id != roleId))
                return ServiceResult.Fail($"Le rôle « {name} » existe déjà.");
        }

        var changes = new List<string>();
        if (role.Name != name.Trim())
            changes.Add($"Nom : « {role.Name} » → « {name.Trim()} »");

        var oldPerms = ParsePermissions(role.Permissions);
        var addedPerms = permissions.Where(kv => kv.Value && (!oldPerms.TryGetValue(kv.Key, out var old) || !old)).Select(kv => kv.Key).ToList();
        var removedPerms = oldPerms.Where(kv => kv.Value && (!permissions.TryGetValue(kv.Key, out var nw) || !nw)).Select(kv => kv.Key).ToList();
        if (addedPerms.Count > 0)
            changes.Add($"+Permissions : {string.Join(", ", addedPerms)}");
        if (removedPerms.Count > 0)
            changes.Add($"-Permissions : {string.Join(", ", removedPerms)}");

        role.Name = name.Trim();
        role.Permissions = JsonSerializer.Serialize(permissions);

        await roleRepo.UpdateAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleUpdated, role.Id.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        var detail = changes.Count > 0
            ? $"Rôle « {role.Name} » · {string.Join(" · ", changes)}"
            : $"Rôle « {role.Name} » · Aucune modification détectée";
        await _audit.LogAsync(
            AuditAction.RoleUpdated,
            AuditModule.Users,
            detail,
            entityType: "Role",
            entityId: role.Id.ToString());

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRoleAsync(int roleId, int currentUserId)
    {
        var roleRepo = _uow.GetRepository<Role>();
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role == null)
            return ServiceResult.Fail("Rôle introuvable.");

        var (isSA, _) = await ResolvePrivilegesAsync(currentUserId);
        if (!isSA)
            return ServiceResult.Fail("Seul le SuperAdmin peut modifier les rôles.");

        if (IsSuperAdminRole(role))
            return ServiceResult.Fail("Le rôle SuperAdmin ne peut pas être supprimé.");

        var usersWithRole = await _uow.Users.FindAsync(u => u.RoleId == roleId);
        if (usersWithRole.Any())
            return ServiceResult.Fail(
                $"Ce rôle est encore attribué à {usersWithRole.Count} utilisateur(s). " +
                "Réassignez-les avant de supprimer le rôle.");

        var roleName = role.Name;

        await roleRepo.DeleteAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleDeleted, roleId.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ── (fixed argument order)
        await _audit.LogAsync(
            AuditAction.RoleDeleted,
            AuditModule.Users,
            $"Rôle « {roleName} » · Supprimé",
            entityType: "Role",
            entityId: roleId.ToString());

        return ServiceResult.Ok();
    }

    // ═══════ MANAGER BARCODE (override card) ═══════

    /// <summary>
    /// Generates a fresh manager barcode for <paramref name="targetUserId"/>.
    /// Returns the plain payload ONCE for printing — it is not stored, only its SHA-256 hash is.
    /// Any previously issued card for that user is invalidated.
    /// </summary>
    public async Task<(ServiceResult Result, string? PlainCode)>
        GenerateManagerBarcodeAsync(int targetUserId, int currentUserId)
    {
        // Only SuperAdmin can issue cards.
        var (isSA, _) = await ResolvePrivilegesAsync(currentUserId);
        if (!isSA)
            return (ServiceResult.Fail("Seul le SuperAdmin peut générer une carte manager."), null);

        var target = await _uow.Users.GetByIdAsync(targetUserId);
        if (target == null)
            return (ServiceResult.Fail("Utilisateur introuvable."), null);
        if (!target.IsActive)
            return (ServiceResult.Fail("Impossible d'émettre une carte pour un compte inactif."), null);
        if (IsSuperAdminUser(target))
            return (ServiceResult.Fail("Le SuperAdmin n'utilise pas de carte manager."), null);

        var targetRole = await _uow.GetRepository<Role>().GetByIdAsync(target.RoleId);
        if (!IsManagerEligibleRole(targetRole))
            return (ServiceResult.Fail(
                $"Le rôle « {targetRole?.Name} » n'autorise pas l'émission d'une carte manager."), null);

        // MGR-XXXX-XXXX (8 chars from a 31-symbol alphabet ≈ 39 bits entropy).
        // Retry on the astronomically rare hash collision.
        string plain, hash;
        int attempt = 0;
        while (true)
        {
            var bytes = new byte[8];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            var sb = new System.Text.StringBuilder("MGR-");
            for (int i = 0; i < 8; i++)
            {
                if (i == 4) sb.Append('-');
                sb.Append(BarcodeAlphabet[bytes[i] % BarcodeAlphabet.Length]);
            }
            plain = sb.ToString();
            hash = Sha256Hex(plain);

            var clash = await _uow.Users.FindAsync(u => u.ManagerBarcodeHash == hash);
            if (!clash.Any()) break;
            if (++attempt > 5)
                return (ServiceResult.Fail("Impossible de générer un code unique. Réessayez."), null);
        }

        target.ManagerBarcodeHash = hash;
        await _uow.Users.UpdateAsync(target);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserUpdated, target.Id.ToString());
        await _uow.FlushEventsAsync();

        await _audit.LogAsync(
            AuditAction.ManagerCardIssued,      // or AuditAction.UserUpdated if you didn't add the enum
            AuditModule.Users,
            $"Carte manager émise pour « {target.Username} » ({target.FullName}) · Rôle « {targetRole?.Name} »",
            entityType: "User",
            entityId: target.Id.ToString());

        return (ServiceResult.Ok(), plain);
    }

    /// <summary>
    /// Revokes the manager barcode for a user. Idempotent — safe to call even if no card exists.
    /// </summary>
    public async Task<ServiceResult> RevokeManagerBarcodeAsync(int targetUserId, int currentUserId)
    {
        var (isSA, _) = await ResolvePrivilegesAsync(currentUserId);
        if (!isSA)
            return ServiceResult.Fail("Seul le SuperAdmin peut révoquer une carte manager.");

        var target = await _uow.Users.GetByIdAsync(targetUserId);
        if (target == null)
            return ServiceResult.Fail("Utilisateur introuvable.");

        if (target.ManagerBarcodeHash == null)
            return ServiceResult.Ok();  // already revoked — no-op

        target.ManagerBarcodeHash = null;
        await _uow.Users.UpdateAsync(target);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserUpdated, target.Id.ToString());
        await _uow.FlushEventsAsync();

        await _audit.LogAsync(
            AuditAction.ManagerCardRevoked,     // fallback: AuditAction.UserUpdated
            AuditModule.Users,
            $"Carte manager révoquée pour « {target.Username} » ({target.FullName})",
            entityType: "User",
            entityId: target.Id.ToString());

        return ServiceResult.Ok();
    }

    /// <summary>
    /// Resolves a scanned/typed manager barcode payload to the owning user.
    /// O(1) indexed lookup on ManagerBarcodeHash. Returns null if no active user matches.
    /// </summary>
    public async Task<User?> ResolveManagerBarcodeAsync(string scannedCode)
    {
        if (string.IsNullOrWhiteSpace(scannedCode)) return null;

        var normalized = scannedCode.Trim().ToUpperInvariant();
        var hash = Sha256Hex(normalized);

        var matches = await _uow.Users.FindAsync(u =>
            u.IsActive && u.ManagerBarcodeHash == hash);

        // Expected: 0 or 1. If somehow more, take the first.
        return matches.FirstOrDefault();
    }

    // ═══════ HELPERS ═══════

    public static Dictionary<string, bool> ParsePermissions(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(json ?? "{}") ?? new(); }
        catch { return new(); }
    }

    public static int[] ParseAssignedPosIds(string json)
    {
        try { return JsonSerializer.Deserialize<int[]>(json ?? "[]") ?? []; }
        catch { return []; }
    }

    // ═══════ MANAGER BARCODE HELPERS ═══════

    /// <summary>SHA-256 hex (lowercase). Same format as ManagerBarcodeHash column.</summary>
    private static string Sha256Hex(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Roles that may carry an override barcode. Excludes SuperAdmin (never gets a card),
    /// IT Tech, and Inspecteur DGI (those aren't "cashier managers").
    /// </summary>
    public static bool IsManagerEligibleRole(Role? r)
    {
        if (r == null) return false;
        if (IsSuperAdminRole(r) || IsITTechRole(r) || IsInspecteurDGIRole(r)) return false;

        var n = r.Name?.Trim().ToLowerInvariant() ?? "";
        return n is "admin" or "administrateur"
                 or "gestionnaire" or "manager"
                 or "responsable" or "chef de caisse";
    }

}

// ═══════ SERVICE RESULT ═══════

public class ServiceResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = "";

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}