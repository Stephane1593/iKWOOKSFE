using System.Text.Json;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Application.Services;

public class UserService
{
    private readonly IUnitOfWork _uow;

    // ═══════ CONSTANTES DE PROTECTION ═══════
    public const string SuperAdminUsername = "superadmin";
    public const string SuperAdminRoleName = "SuperAdmin";
    public const string ITTechRoleName = "IT Tech";

    /// <summary>
    /// Rôles que seul le SuperAdmin peut attribuer.
    /// </summary>
    private static readonly HashSet<string> RestrictedRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        SuperAdminRoleName,
        ITTechRoleName
    };

    /// <summary>All known permission keys with French labels.</summary>
    public static readonly List<(string Key, string Label)> AllPermissions = new()
    {
        ("dashboard",       "Tableau de bord"),
        ("pos",             "Point de vente (caisse)"),
        ("invoicing",       "Facturation"),
        ("clients",         "Clients"),
        ("salesHistory",    "Historique des ventes"),
        ("products",        "Produits"),
        ("stock",           "Stock"),
        ("transfers",       "Transferts stock"),
        ("loyalty",         "Programme fidélité"),
        ("reports",         "Rapports"),
        ("settings",        "Paramètres"),
        ("users",           "Gestion utilisateurs"),
        ("bypassPosCheck",  "Accès sans POS")
    };

    public UserService(IUnitOfWork unitOfWork)
    {
        _uow = unitOfWork;
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
    /// SuperAdmin sees all roles (except SuperAdmin itself).
    /// Others see all except SuperAdmin + IT Tech.
    /// </summary>
    public async Task<List<Role>> GetAssignableRolesAsync(User currentUser)
    {
        var allRoles = await _uow.GetRepository<Role>().GetAllAsync();
        bool isSuperAdmin = IsSuperAdminUser(currentUser);

        return allRoles
            .Where(r =>
            {
                // Nobody can assign SuperAdmin via UI
                if (IsSuperAdminRole(r)) return false;

                // Only SuperAdmin can assign restricted roles (IT Tech)
                if (!isSuperAdmin && IsRestrictedRole(r)) return false;

                return true;
            })
            .OrderBy(r => r.Name)
            .ToList();
    }

    public async Task<List<PointOfSale>> GetAllPointsOfSaleAsync()
        => await _uow.PointsOfSale.GetAllAsync();

    // ═══════ GUARDS ═══════

    public static bool IsSuperAdminUser(User user)
        => user.Username.Equals(SuperAdminUsername, StringComparison.OrdinalIgnoreCase);

    public static bool IsSuperAdminRole(Role role)
        => role.Name.Equals(SuperAdminRoleName, StringComparison.OrdinalIgnoreCase);

    public static bool IsRestrictedRole(Role role)
        => RestrictedRoleNames.Contains(role.Name);

    public static bool IsITTechRole(Role role)
        => role.Name.Equals(ITTechRoleName, StringComparison.OrdinalIgnoreCase);

    // ═══════ USER CRUD ═══════

    public async Task<ServiceResult> CreateUserAsync(User user, string plainPassword, int currentUserId)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(user.Username))
            return ServiceResult.Fail("Le nom d'utilisateur est obligatoire.");

        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 4)
            return ServiceResult.Fail("Le mot de passe doit contenir au moins 4 caractères.");

        if (string.IsNullOrWhiteSpace(user.FullName))
            return ServiceResult.Fail("Le nom complet est obligatoire.");

        if (user.RoleId <= 0)
            return ServiceResult.Fail("Veuillez sélectionner un rôle.");

        // Prevent creating another "superadmin" username
        if (user.Username.Equals(SuperAdminUsername, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Le nom d'utilisateur « superadmin » est réservé au système.");

        // Uniqueness
        var existing = await _uow.Users.GetByUsernameAsync(user.Username.Trim());
        if (existing != null)
            return ServiceResult.Fail($"Le nom d'utilisateur « {user.Username} » est déjà pris.");

        // ── Role assignment check ──
        var targetRole = await _uow.GetRepository<Role>().GetByIdAsync(user.RoleId);
        if (targetRole == null)
            return ServiceResult.Fail("Rôle introuvable.");

        if (IsSuperAdminRole(targetRole))
            return ServiceResult.Fail("Le rôle SuperAdmin ne peut pas être attribué manuellement.");

        // Only SuperAdmin can assign IT Tech (or other restricted roles)
        if (IsRestrictedRole(targetRole))
        {
            var currentUser = await _uow.Users.GetByIdAsync(currentUserId);
            if (currentUser == null || !IsSuperAdminUser(currentUser))
                return ServiceResult.Fail(
                    $"Seul le SuperAdmin peut attribuer le rôle « {targetRole.Name} ».");
        }

        user.Username = user.Username.Trim().ToLower();
        user.FullName = user.FullName.Trim();
        user.PasswordHash = AuthService.HashPassword(plainPassword);
        user.IsActive = true;

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserCreated, user.Id.ToString());
        await _uow.FlushEventsAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateUserAsync(User user, int currentUserId, string? newPlainPassword = null)
    {
        var existing = await _uow.Users.GetByUsernameAsync(user.Username);
        if (existing == null)
        {
            // Might have been fetched by ID; try fallback
            existing = await _uow.Users.GetByIdAsync(user.Id);
        }
        if (existing == null)
            return ServiceResult.Fail("Utilisateur introuvable.");

        // ── SuperAdmin guards ──
        if (IsSuperAdminUser(existing))
        {
            if (user.RoleId != existing.RoleId)
                return ServiceResult.Fail("Le rôle du SuperAdmin ne peut pas être modifié.");
            if (!user.IsActive)
                return ServiceResult.Fail("Le compte SuperAdmin ne peut pas être désactivé.");
            if (!user.Username.Equals(existing.Username, StringComparison.OrdinalIgnoreCase))
                return ServiceResult.Fail("Le nom d'utilisateur du SuperAdmin ne peut pas être modifié.");
        }

        // Validation
        if (string.IsNullOrWhiteSpace(user.FullName))
            return ServiceResult.Fail("Le nom complet est obligatoire.");

        if (user.RoleId <= 0)
            return ServiceResult.Fail("Veuillez sélectionner un rôle.");

        // ── Role assignment check (if role changed) ──
        if (user.RoleId != existing.RoleId)
        {
            var newRole = await _uow.GetRepository<Role>().GetByIdAsync(user.RoleId);
            if (newRole == null)
                return ServiceResult.Fail("Rôle introuvable.");

            if (IsSuperAdminRole(newRole))
                return ServiceResult.Fail("Le rôle SuperAdmin ne peut pas être attribué manuellement.");

            if (IsRestrictedRole(newRole))
            {
                var currentUser = await _uow.Users.GetByIdAsync(currentUserId);
                if (currentUser == null || !IsSuperAdminUser(currentUser))
                    return ServiceResult.Fail(
                        $"Seul le SuperAdmin peut attribuer le rôle « {newRole.Name} ».");
            }
        }

        // Username uniqueness (if changed)
        if (!existing.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
        {
            var dup = await _uow.Users.GetByUsernameAsync(user.Username.Trim());
            if (dup != null)
                return ServiceResult.Fail($"Le nom d'utilisateur « {user.Username} » est déjà pris.");
        }

        // Apply changes
        existing.FullName = user.FullName.Trim();
        existing.RoleId = user.RoleId;
        existing.IsActive = user.IsActive;
        existing.AssignedPosIds = user.AssignedPosIds;

        // Password change (optional)
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

        await _uow.Users.DeleteAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserDeleted, userId.ToString());
        await _uow.FlushEventsAsync();

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

        user.IsActive = !user.IsActive;
        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.UserUpdated, userId.ToString());
        await _uow.FlushEventsAsync();

        return ServiceResult.Ok();
    }

    // ═══════ ROLE CRUD ═══════

    public async Task<ServiceResult> CreateRoleAsync(string name, Dictionary<string, bool> permissions)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult.Fail("Le nom du rôle est obligatoire.");

        if (name.Equals(SuperAdminRoleName, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Le nom « SuperAdmin » est réservé au système.");

        var roleRepo = _uow.GetRepository<Role>();
        var existing = await roleRepo.FindAsync(r => r.Name == name.Trim());
        if (existing.Any())
            return ServiceResult.Fail($"Le rôle « {name} » existe déjà.");

        var role = new Role
        {
            Name = name.Trim(),
            Permissions = JsonSerializer.Serialize(permissions)
        };

        await roleRepo.AddAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleCreated, role.Id.ToString());
        await _uow.FlushEventsAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateRoleAsync(int roleId, string name, Dictionary<string, bool> permissions)
    {
        var roleRepo = _uow.GetRepository<Role>();
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role == null)
            return ServiceResult.Fail("Rôle introuvable.");

        if (IsSuperAdminRole(role))
            return ServiceResult.Fail("Le rôle SuperAdmin ne peut pas être modifié.");

        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult.Fail("Le nom du rôle est obligatoire.");

        if (name.Equals(SuperAdminRoleName, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Le nom « SuperAdmin » est réservé au système.");

        // Uniqueness (if name changed)
        if (!role.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var dup = await roleRepo.FindAsync(r => r.Name == name.Trim());
            if (dup.Any(r => r.Id != roleId))
                return ServiceResult.Fail($"Le rôle « {name} » existe déjà.");
        }

        role.Name = name.Trim();
        role.Permissions = JsonSerializer.Serialize(permissions);

        await roleRepo.UpdateAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleUpdated, role.Id.ToString());
        await _uow.FlushEventsAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRoleAsync(int roleId, int currentUserId)
    {
        var roleRepo = _uow.GetRepository<Role>();
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role == null)
            return ServiceResult.Fail("Rôle introuvable.");

        if (IsSuperAdminRole(role))
            return ServiceResult.Fail("Le rôle SuperAdmin ne peut pas être supprimé.");

        // Only SuperAdmin can delete restricted roles (IT Tech, etc.)
        if (IsRestrictedRole(role))
        {
            var currentUser = await _uow.Users.GetByIdAsync(currentUserId);
            if (currentUser == null || !IsSuperAdminUser(currentUser))
                return ServiceResult.Fail(
                    $"Seul le SuperAdmin peut supprimer le rôle « {role.Name} ».");
        }

        var usersWithRole = await _uow.Users.FindAsync(u => u.RoleId == roleId);
        if (usersWithRole.Any())
            return ServiceResult.Fail(
                $"Ce rôle est encore attribué à {usersWithRole.Count} utilisateur(s). " +
                "Réassignez-les avant de supprimer le rôle.");

        await roleRepo.DeleteAsync(role);
        await _uow.SaveChangesAsync();

        _uow.EnqueueEvent(AppEvent.RoleDeleted, roleId.ToString());
        await _uow.FlushEventsAsync();

        return ServiceResult.Ok();
    }

    // ═══════ HELPERS ═══════

    public static Dictionary<string, bool> ParsePermissions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json ?? "{}") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static int[] ParseAssignedPosIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(json ?? "[]") ?? [];
        }
        catch
        {
            return [];
        }
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