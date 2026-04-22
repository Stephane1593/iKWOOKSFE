using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;

namespace SFE.WPF.ViewModels;

public partial class UsersViewModel : BaseViewModel
{
    private readonly UserService _userService;
    private readonly IAuthService _authService;

    // ═══════ TAB ═══════
    [ObservableProperty] private bool _isUsersTab = true;

    // ═══════ USER LIST ═══════
    public ObservableCollection<UserListItem> Users { get; } = new();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _userCount;

    // ═══════ USER FORM ═══════
    [ObservableProperty] private bool _isEditingUser;
    [ObservableProperty] private bool _isCreatingUser;
    [ObservableProperty] private string _userFormTitle = "";
    [ObservableProperty] private bool _isProtectedUser;
    private int _editingUserId;

    [ObservableProperty] private string _formUsername = "";
    [ObservableProperty] private string _formPassword = "";
    [ObservableProperty] private string _formFullName = "";
    [ObservableProperty] private Role? _formRole;
    [ObservableProperty] private bool _formIsActive = true;

    public ObservableCollection<Role> AvailableRoles { get; } = new();
    public ObservableCollection<PosAssignmentItem> AvailablePos { get; } = new();

    // ═══════ ROLE LIST ═══════
    public ObservableCollection<RoleListItem> Roles { get; } = new();
    [ObservableProperty] private int _roleCount;

    // ═══════ ROLE FORM ═══════
    [ObservableProperty] private bool _isEditingRole;
    [ObservableProperty] private bool _isCreatingRole;
    [ObservableProperty] private string _roleFormTitle = "";
    [ObservableProperty] private bool _isProtectedRole;
    private int _editingRoleId;

    [ObservableProperty] private string _formRoleName = "";
    public ObservableCollection<PermissionItem> FormPermissions { get; } = new();

    // ═══════ CACHED ═══════
    private List<User> _allUsers = new();

    // ═══════ HELPERS ═══════
    private int CurrentUserId => _authService.CurrentUser?.Id ?? 0;
    private User? CurrentUser => _authService.CurrentUser;
    private bool CurrentUserIsSuperAdmin =>
        CurrentUser != null && UserService.IsSuperAdminUser(CurrentUser);

    // ═══════ CONSTRUCTOR ═══════

    public UsersViewModel(UserService userService, IAuthService authService)
    {
        _userService = userService;
        _authService = authService;
        PageTitle = "Utilisateurs & Rôles";

        Subscribe(async () => await LoadAllAsync(),
            AppEvent.UserCreated, AppEvent.UserUpdated, AppEvent.UserDeleted,
            AppEvent.RoleCreated, AppEvent.RoleUpdated, AppEvent.RoleDeleted);

        _ = LoadAllAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyUserFilter();

    // ═══════ TAB SWITCHING ═══════

    [RelayCommand]
    private void SwitchToUsersTab()
    {
        IsUsersTab = true;
        CancelRoleEdit();
    }

    [RelayCommand]
    private void SwitchToRolesTab()
    {
        IsUsersTab = false;
        CancelUserEdit();
    }

    // ═══════ LOAD ═══════

    private async Task LoadAllAsync()
    {
        try
        {
            IsBusy = true;

            _allUsers = await _userService.GetAllWithRolesAsync();
            var allPos = await _userService.GetAllPointsOfSaleAsync();

            // ── Assignable roles depend on who is logged in ──
            AvailableRoles.Clear();
            if (CurrentUser != null)
            {
                var assignable = await _userService.GetAssignableRolesAsync(CurrentUser);
                foreach (var r in assignable)
                    AvailableRoles.Add(r);
            }

            // POS list
            AvailablePos.Clear();
            foreach (var p in allPos)
                AvailablePos.Add(new PosAssignmentItem
                {
                    PosId = p.Id,
                    DisplayName = $"{p.Name} ({p.Code})"
                });

            // All roles (for roles tab)
            var allRoles = await _userService.GetAllRolesAsync();
            Roles.Clear();
            foreach (var r in allRoles.OrderBy(r => r.Name))
            {
                var count = _allUsers.Count(u => u.RoleId == r.Id);
                Roles.Add(new RoleListItem(r, count));
            }
            RoleCount = Roles.Count;

            ApplyUserFilter();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur de chargement : {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyUserFilter()
    {
        var filtered = _allUsers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLower();
            filtered = filtered.Where(u =>
                u.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                u.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (u.Role?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Users.Clear();
        foreach (var u in filtered)
            Users.Add(new UserListItem(u));
        UserCount = Users.Count;
    }

    // ══════════════════════════════════════════════════════
    //  USER COMMANDS
    // ══════════════════════════════════════════════════════

    [RelayCommand]
    private void NewUser()
    {
        ClearUserForm();
        IsCreatingUser = true;
        IsEditingUser = true;
        IsProtectedUser = false;
        UserFormTitle = "Nouvel utilisateur";
        FormIsActive = true;
    }

    [RelayCommand]
    private void EditUser(UserListItem? item)
    {
        if (item == null) return;

        var user = _allUsers.FirstOrDefault(u => u.Id == item.Id);
        if (user == null) return;

        _editingUserId = user.Id;
        IsCreatingUser = false;
        IsEditingUser = true;
        IsProtectedUser = UserService.IsSuperAdminUser(user);
        UserFormTitle = $"Modifier — {user.FullName}";

        FormUsername = user.Username;
        FormPassword = "";
        FormFullName = user.FullName;
        FormIsActive = user.IsActive;
        FormRole = AvailableRoles.FirstOrDefault(r => r.Id == user.RoleId);

        // POS assignment
        var assignedIds = UserService.ParseAssignedPosIds(user.AssignedPosIds);
        foreach (var pos in AvailablePos)
            pos.IsAssigned = assignedIds.Contains(pos.PosId);

        ClearStatus();
    }

    [RelayCommand]
    private async Task SaveUser()
    {
        ClearStatus();

        var selectedPosIds = AvailablePos
            .Where(p => p.IsAssigned)
            .Select(p => p.PosId)
            .ToArray();
        var posJson = JsonSerializer.Serialize(selectedPosIds);

        ServiceResult result;

        if (IsCreatingUser)
        {
            var user = new User
            {
                Username = FormUsername,
                FullName = FormFullName,
                RoleId = FormRole?.Id ?? 0,
                IsActive = FormIsActive,
                AssignedPosIds = posJson
            };
            result = await _userService.CreateUserAsync(user, FormPassword, CurrentUserId);
        }
        else
        {
            var user = new User
            {
                Id = _editingUserId,
                Username = FormUsername,
                FullName = FormFullName,
                RoleId = FormRole?.Id ?? _allUsers.First(u => u.Id == _editingUserId).RoleId,
                IsActive = FormIsActive,
                AssignedPosIds = posJson
            };
            var newPwd = string.IsNullOrWhiteSpace(FormPassword) ? null : FormPassword;
            result = await _userService.UpdateUserAsync(user, CurrentUserId, newPwd);
        }

        if (result.Success)
        {
            var msg = IsCreatingUser
                ? "✓ Utilisateur créé avec succès."
                : "✓ Utilisateur mis à jour.";
            IsEditingUser = false;
            await LoadAllAsync();
            _ = ShowSuccessAsync(msg);
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task DeleteUser(UserListItem? item)
    {
        if (item == null) return;
        ClearStatus();

        var result = await _userService.DeleteUserAsync(item.Id, CurrentUserId);

        if (result.Success)
        {
            if (IsEditingUser && _editingUserId == item.Id)
                CancelUserEdit();
            await LoadAllAsync();
            _ = ShowSuccessAsync("✓ Utilisateur supprimé.");
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task ToggleUserActive(UserListItem? item)
    {
        if (item == null) return;
        ClearStatus();

        var result = await _userService.ToggleActiveAsync(item.Id, CurrentUserId);

        if (result.Success)
        {
            await LoadAllAsync();
            _ = ShowSuccessAsync(item.IsActive
                ? "✓ Utilisateur désactivé."
                : "✓ Utilisateur activé.");
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private void CancelUserEdit()
    {
        IsEditingUser = false;
        IsCreatingUser = false;
        ClearUserForm();
        ClearStatus();
    }

    private void ClearUserForm()
    {
        _editingUserId = 0;
        FormUsername = "";
        FormPassword = "";
        FormFullName = "";
        FormRole = null;
        FormIsActive = true;
        IsProtectedUser = false;
        UserFormTitle = "";
        foreach (var pos in AvailablePos)
            pos.IsAssigned = false;
    }

    // ══════════════════════════════════════════════════════
    //  ROLE COMMANDS
    // ══════════════════════════════════════════════════════

    [RelayCommand]
    private void NewRole()
    {
        ClearRoleForm();
        IsCreatingRole = true;
        IsEditingRole = true;
        IsProtectedRole = false;
        RoleFormTitle = "Nouveau rôle";
        InitPermissionItems(new Dictionary<string, bool>());
    }

    [RelayCommand]
    private void EditRole(RoleListItem? item)
    {
        if (item == null) return;

        _editingRoleId = item.Id;
        IsCreatingRole = false;
        IsEditingRole = true;
        IsProtectedRole = item.IsSuperAdmin;
        RoleFormTitle = item.IsSuperAdmin
            ? $"🔒 {item.Name} (protégé)"
            : $"Modifier — {item.Name}";

        FormRoleName = item.Name;
        InitPermissionItems(UserService.ParsePermissions(item.PermissionsJson));

        ClearStatus();
    }

    [RelayCommand]
    private async Task SaveRole()
    {
        ClearStatus();

        var perms = FormPermissions.ToDictionary(p => p.Key, p => p.IsGranted);
        ServiceResult result;

        if (IsCreatingRole)
            result = await _userService.CreateRoleAsync(FormRoleName, perms);
        else
            result = await _userService.UpdateRoleAsync(_editingRoleId, FormRoleName, perms);

        if (result.Success)
        {
            var msg = IsCreatingRole
                ? "✓ Rôle créé avec succès."
                : "✓ Rôle mis à jour.";
            IsEditingRole = false;
            await LoadAllAsync();
            _ = ShowSuccessAsync(msg);
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task DeleteRole(RoleListItem? item)
    {
        if (item == null) return;
        ClearStatus();

        var result = await _userService.DeleteRoleAsync(item.Id, CurrentUserId);

        if (result.Success)
        {
            if (IsEditingRole && _editingRoleId == item.Id)
                CancelRoleEdit();
            await LoadAllAsync();
            _ = ShowSuccessAsync("✓ Rôle supprimé.");
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private void CancelRoleEdit()
    {
        IsEditingRole = false;
        IsCreatingRole = false;
        ClearRoleForm();
        ClearStatus();
    }

    private void ClearRoleForm()
    {
        _editingRoleId = 0;
        FormRoleName = "";
        IsProtectedRole = false;
        RoleFormTitle = "";
        FormPermissions.Clear();
    }

    private void InitPermissionItems(Dictionary<string, bool> current)
    {
        FormPermissions.Clear();
        foreach (var (key, label) in UserService.AllPermissions)
        {
            current.TryGetValue(key, out var granted);
            FormPermissions.Add(new PermissionItem
            {
                Key = key,
                Label = label,
                IsGranted = granted
            });
        }
    }
}

// ══════════════════════════════════════════════════════
//  HELPER CLASSES
// ══════════════════════════════════════════════════════

public class UserListItem
{
    public int Id { get; }
    public string Username { get; }
    public string FullName { get; }
    public string RoleName { get; }
    public bool IsActive { get; }
    public string ActiveDisplay { get; }
    public string LastLoginDisplay { get; }
    public bool IsSuperAdmin { get; }

    public UserListItem(User u)
    {
        Id = u.Id;
        Username = u.Username;
        FullName = u.FullName;
        RoleName = u.Role?.Name ?? "—";
        IsActive = u.IsActive;
        ActiveDisplay = u.IsActive ? "Actif" : "Inactif";
        LastLoginDisplay = u.LastLoginAt?.ToString("dd/MM/yyyy HH:mm") ?? "Jamais";
        IsSuperAdmin = UserService.IsSuperAdminUser(u);
    }
}

public class RoleListItem
{
    public int Id { get; }
    public string Name { get; }
    public int UserCount { get; }
    public bool IsSuperAdmin { get; }
    public string PermissionsJson { get; }

    public RoleListItem(Role r, int userCount)
    {
        Id = r.Id;
        Name = r.Name;
        UserCount = userCount;
        IsSuperAdmin = UserService.IsSuperAdminRole(r);
        PermissionsJson = r.Permissions;
    }
}

public partial class PosAssignmentItem : ObservableObject
{
    public int PosId { get; set; }
    public string DisplayName { get; set; } = "";

    [ObservableProperty]
    private bool _isAssigned;
}

public partial class PermissionItem : ObservableObject
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";

    [ObservableProperty]
    private bool _isGranted;
}