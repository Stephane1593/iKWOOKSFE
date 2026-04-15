using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Application.Services;

public class AuthService : IAuthService
{
    private readonly IServiceProvider _sp;
    private User? _currentUser;
    private Dictionary<string, bool>? _permCache;

    public event Action? UserChanged;

    public User? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null;

    public AuthService(IServiceProvider serviceProvider)
    {
        _sp = serviceProvider;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        var uow = _sp.GetRequiredService<IUnitOfWork>();
        var hash = HashPassword(password);
        var user = await uow.Users.AuthenticateAsync(username, hash);

        if (user == null) return null;

        _currentUser = user;
        _permCache = null;

        user.LastLoginAt = DateTime.Now;
        await uow.SaveChangesAsync();

        UserChanged?.Invoke();
        return user;
    }

    public void Logout()
    {
        _currentUser = null;
        _permCache = null;
        UserChanged?.Invoke();
    }

    public bool HasPermission(string permission)
    {
        if (_currentUser?.Role == null) return false;

        _permCache ??= JsonSerializer.Deserialize<Dictionary<string, bool>>(
            _currentUser.Role.Permissions ?? "{}");

        return _permCache != null
            && _permCache.TryGetValue(permission, out var allowed)
            && allowed;
    }

    public string GetUserInitials()
    {
        if (string.IsNullOrWhiteSpace(_currentUser?.FullName)) return "?";
        var parts = _currentUser.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
        return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
    }

    // ── SHA-256 — upgrade to BCrypt/Argon2 for production ──
    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}