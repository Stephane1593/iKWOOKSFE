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

    // ═══════ CONSTANTES DE PROTECTION ═══════
    public const string SuperAdminUsername = "superadmin";
    public const string SuperAdminRoleName = "SuperAdmin";
    public const string ITTechRoleName = "IT Tech";
    public const string InspecteurDGIRoleName = "Inspecteur DGI";

    /// <summary>Roles only SuperAdmin can manage.</summary>
    private static readonly HashSet<string> SuperAdminOnlyRoleNames =
        new(StringComparer.OrdinalIgnoreCase) { SuperAdminRoleName, ITTechRoleName };

    /// <summary>Roles SuperAdmin OR IT Tech can manage.</summary>
    private static readonly HashSet<string> ElevatedRoleNames =
        new(StringComparer.OrdinalIgnoreCase) { InspecteurDGIRoleName };

    /// <summary>All known permission keys with French labels.</summary>
    public static readonly List<(string Key, string Label)> AllPermissions = new()
{
    ("dashboard",      "Tableau de bord"),
    ("pos",            "Point de vente (caisse)"),
    ("invoicing",      "Facturation"),
    ("clients",        "Clients"),
    ("salesHistory",   "Historique des ventes"),
    ("products",       "Produits"),
    ("stock",          "Stock"),
    ("transfers",      "Transferts stock"),
    ("loyalty",        "Programme fidélité"),
    ("reports",        "Rapports (X, A, historique)"),   // ← updated label
    ("closeZ",         "Clôture Z (fin de session)"),    // ★ NEW
    ("settings",       "Paramètres"),
    ("users",          "Gestion utilisateurs"),
    ("audit",          "Journal d'audit"),
    ("bypassPosCheck", "Accès sans POS")
};

    public UserService(IUnitOfWork unitOfWork, IAuditService audit)
    {
        _uow = unitOfWork;
        _audit = audit;
    }

    // ═══════ QUERIES ═══════

    public async Task<List<User>> GetAllWithRolesAsync()
        => await _uow.Users.GetAllWithRolesAsync();

    public async Task<User?> GetByIdAsync(int id)
        => await _uow.Users.GetByIdAsync(id);

    public async Task<List<Role>> GetAllRolesAsync()
        => await _uow.GetRepository<Role>().GetAllAsync();

    /// <summary>
    /// Returns roles the given user is allowed to assign.
    /// SuperAdmin  → all except SuperAdmin itself.
    /// IT Tech     → all except SuperAdmin and IT Tech (but INCLUDING Inspecteur DGI).
    /// Others      → all except SuperAdmin, IT Tech, and Inspecteur DGI.
    /// </summary>
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

    /// <summary>Returns (isSuperAdmin, isITTech) for the given user.</summary>
    private async Task<(bool isSA, bool isIT)> ResolvePrivilegesAsync(int userId)
    {
        var cu = await _uow.Users.GetByIdAsync(userId);
        if (cu == null) return (false, false);
        if (IsSuperAdminUser(cu)) return (true, false);
        var cuRole = await _uow.GetRepository<Role>().GetByIdAsync(cu.RoleId);
        return (false, cuRole != null && IsITTechRole(cuRole));
    }

    // ═══════ USER-LEVEL AUTHORIZATION ═══════

    /// <summary>
    /// Returns null when authorized, or an error message when not.
    /// Checks whether currentUserId may manage a USER whose role is targetRole.
    /// </summary>
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

        user.Username = user.Username.Trim().ToLower();
        user.FullName = user.FullName.Trim();
        user.PasswordHash = AuthService.HashPassword(plainPassword);
        user.IsActive = true;

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserCreated, user.Id.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.UserCreated, AuditModule.Users,
            user.Id.ToString(),
            $"Utilisateur « {user.Username} » ({user.FullName}) · Rôle « {targetRole.Name} »" +
            (user.PointOfSaleId.HasValue ? $" · POS #{user.PointOfSaleId}" : ""));

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

        // ── Guard on EXISTING role ──
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

        // ── Guard on NEW role (if changed) ──
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

        // Capture changes for audit before overwriting
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

        existing.FullName = user.FullName.Trim();
        existing.RoleId = user.RoleId;
        existing.IsActive = user.IsActive;
        existing.PointOfSaleId = user.PointOfSaleId;

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

        // ── AUDIT ──
        var detail = changes.Count > 0
            ? $"Utilisateur « {existing.Username} » · {string.Join(" · ", changes)}"
            : $"Utilisateur « {existing.Username} » · Aucune modification détectée";
        await _audit.LogAsync(AuditAction.UserUpdated, AuditModule.Users,
            existing.Id.ToString(), detail);

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

        // Capture info before deletion
        var username = user.Username;
        var fullName = user.FullName;
        var roleName = userRole?.Name ?? "?";

        await _uow.Users.DeleteAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserDeleted, userId.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.UserDeleted, AuditModule.Users,
            userId.ToString(),
            $"Utilisateur « {username} » ({fullName}) · Rôle « {roleName} » · Supprimé");

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
        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserUpdated, userId.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ──
        var action = user.IsActive ? AuditAction.UserActivated : AuditAction.UserDeactivated;
        await _audit.LogAsync(action, AuditModule.Users,
            userId.ToString(),
            $"Utilisateur « {user.Username} » ({user.FullName}) · " +
            (user.IsActive ? "Activé" : "Désactivé"));

        return ServiceResult.Ok();
    }

    // ═══════ ROLE CRUD ═══════

    public async Task<ServiceResult> CreateRoleAsync(
        string name, Dictionary<string, bool> permissions, int currentUserId)
    {
        // ── Only SuperAdmin can create roles ──
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

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.RoleCreated, AuditModule.Users,
            role.Id.ToString(),
            $"Rôle « {role.Name} » · Permissions : {(enabledPerms.Count > 0 ? string.Join(", ", enabledPerms) : "aucune")}");

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateRoleAsync(
        int roleId, string name, Dictionary<string, bool> permissions, int currentUserId)
    {
        var roleRepo = _uow.GetRepository<Role>();
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role == null)
            return ServiceResult.Fail("Rôle introuvable.");

        // ★ GLOBAL GUARD — only SuperAdmin can modify ANY role
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

        // Capture changes for audit
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

        // ── AUDIT ──
        var detail = changes.Count > 0
            ? $"Rôle « {role.Name} » · {string.Join(" · ", changes)}"
            : $"Rôle « {role.Name} » · Aucune modification détectée";
        await _audit.LogAsync(AuditAction.RoleUpdated, AuditModule.Users,
            role.Id.ToString(), detail);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRoleAsync(int roleId, int currentUserId)
    {
        var roleRepo = _uow.GetRepository<Role>();
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role == null)
            return ServiceResult.Fail("Rôle introuvable.");

        // ★ GLOBAL GUARD — only SuperAdmin can modify ANY role
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

        // Capture before deletion
        var roleName = role.Name;

        await roleRepo.DeleteAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleDeleted, roleId.ToString());
        await _uow.FlushEventsAsync();

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.RoleDeleted, AuditModule.Users,
            roleId.ToString(),
            $"Rôle « {roleName} » · Supprimé");

        return ServiceResult.Ok();
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
}

// ═══════ SERVICE RESULT ═══════

public class ServiceResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = "";

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}