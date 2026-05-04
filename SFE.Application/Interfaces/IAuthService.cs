using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Authenticate user. Returns null if credentials invalid.</summary>
    Task<User?> LoginAsync(string username, string password);

    /// <summary>Clear current session.</summary>
    void Logout();

    User? CurrentUser { get; }
    bool IsLoggedIn { get; }

    /// <summary>Check a permission key against the current user's role JSON.</summary>
    bool HasPermission(string permission);

    /// <summary>Get initials (e.g. "AS" for "Admin Système").</summary>
    string GetUserInitials();

    /// <summary>Fired after login or logout.</summary>
    event Action? UserChanged;
    bool IsInRole(params string[] roleNames);   // ← ADD THIS
}