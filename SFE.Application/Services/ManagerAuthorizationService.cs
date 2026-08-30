using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public sealed class ManagerAuthorizationService : IManagerAuthorizationService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ITimeProvider _time;

    private static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<Guid, IssuedTicket> _tickets = new();

    private sealed record IssuedTicket(ManagerAction Action, int ManagerId, DateTimeOffset ExpiresAt);

    public ManagerAuthorizationService(IUnitOfWork uow, IAuditService audit, ITimeProvider time)
    {
        _uow = uow; _audit = audit; _time = time;
    }

    // -----------------------------------------------------------------
    //  BARCODE
    // -----------------------------------------------------------------
    public async Task<AuthorizationResult> VerifyBarcodeAsync(
        string barcodePayload, ManagerAction action, AuthorizationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(barcodePayload))
            return await DenyAsync(action, ctx, "Code-barres vide.", mgr: null);

        var hash = Hash(barcodePayload.Trim());
        var user = await _uow.Users.FindByManagerBarcodeHashAsync(hash);

        if (user == null || !user.IsActive)
            return await DenyAsync(action, ctx, "Code-barres non reconnu.", mgr: null);

        return await FinishAsync(user, action, ctx, method: "Barcode");
    }

    // -----------------------------------------------------------------
    //  PIN
    // -----------------------------------------------------------------
    public async Task<AuthorizationResult> VerifyPinAsync(
        string? username, string pin, ManagerAction action, AuthorizationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return await DenyAsync(action, ctx, "PIN vide.", mgr: null);

        var hash = Hash(pin.Trim());

        User? user = string.IsNullOrWhiteSpace(username)
            ? await _uow.Users.FindByManagerPinHashAsync(hash)
            : await _uow.Users.FindByUsernameAndPinHashAsync(username.Trim(), hash);

        if (user == null || !user.IsActive)
            return await DenyAsync(action, ctx, "PIN incorrect.", mgr: null);

        return await FinishAsync(user, action, ctx, method: "PIN");
    }

    // -----------------------------------------------------------------
    //  CREDENTIALS
    // -----------------------------------------------------------------
    public async Task<AuthorizationResult> VerifyCredentialsAsync(
        string username, string password, ManagerAction action, AuthorizationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return await DenyAsync(action, ctx, "Identifiants requis.", mgr: null);

        // Reuse the same hash function AuthService uses.
        var pwdHash = AuthService.HashPassword(password);
        var user = await _uow.Users.AuthenticateAsync(username.Trim(), pwdHash);

        if (user == null || !user.IsActive)
            return await DenyAsync(action, ctx, "Identifiants invalides.", mgr: null);

        return await FinishAsync(user, action, ctx, method: "Credentials");
    }

    // -----------------------------------------------------------------
    //  COMMON — check permission, mint ticket, audit
    // -----------------------------------------------------------------
    private async Task<AuthorizationResult> FinishAsync(
        User user, ManagerAction action, AuthorizationContext ctx, string method)
    {
        if (!HasPermission(user, action))
            return await DenyAsync(action, ctx,
                $"L'utilisateur « {user.FullName} » n'a pas la permission « {action.PermissionKey()} ».",
                mgr: user);

        var id = Guid.NewGuid();
        _tickets[id] = new IssuedTicket(action, user.Id, _time.UtcNow.Add(TicketTtl));
        PurgeExpired();

        await _audit.LogAsync(
            AuditAction.ManagerAuthorizationGranted,
            AuditModule.Authorization,
            $"Autorisation « {action.Label()} » accordée par {user.FullName} " +
            $"pour {ctx.RequestingUserName ?? "?"} — {method}. " +
            $"Facture: {ctx.InvoiceNumber ?? "-"} — Montant: {ctx.Amount:N2}",
            entityType: "ManagerAuthorization",
            entityId: id.ToString(),
            invoiceNumber: ctx.InvoiceNumber,
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                action = action.ToString(),
                method,
                managerId = user.Id,
                managerName = user.FullName,
                requesting = new { id = ctx.RequestingUserId, name = ctx.RequestingUserName },
                ctx.Amount,
                ctx.InvoiceId,
                ctx.InvoiceNumber,
                ctx.Reason
            }));

        return AuthorizationResult.Ok(id, user.Id, user.FullName);
    }

    private async Task<AuthorizationResult> DenyAsync(
        ManagerAction action, AuthorizationContext ctx, string reason, User? mgr)
    {
        await _audit.LogAsync(
            AuditAction.ManagerAuthorizationDenied,
            AuditModule.Authorization,
            $"Autorisation « {action.Label()} » refusée — {reason}",
            entityType: "ManagerAuthorization",
            invoiceNumber: ctx.InvoiceNumber,
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                action = action.ToString(),
                denyReason = reason,                 // <-- renamed
                attemptedManagerId = mgr?.Id,
                attemptedManagerName = mgr?.FullName,
                requesting = new { id = ctx.RequestingUserId, name = ctx.RequestingUserName },
                ctx.Amount,
                ctx.InvoiceId,
                ctx.InvoiceNumber,
                contextReason = ctx.Reason              // <-- renamed
            }));

        return AuthorizationResult.Fail(reason);
    }

    private static bool HasPermission(User user, ManagerAction action)
    {
        if (user.Role == null) return false;
        try
        {
            var perms = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, bool>>(user.Role.Permissions ?? "{}");
            return perms != null
                && perms.TryGetValue(action.PermissionKey(), out var ok) && ok;
        }
        catch { return false; }
    }

    // -----------------------------------------------------------------
    //  TICKETS
    // -----------------------------------------------------------------
    public bool TryConsumeTicket(Guid ticketId, ManagerAction action)
    {
        if (!_tickets.TryRemove(ticketId, out var t)) return false;
        if (t.Action != action) return false;
        if (t.ExpiresAt < _time.UtcNow) return false;
        return true;
    }

    private void PurgeExpired()
    {
        var now = _time.UtcNow;
        foreach (var kvp in _tickets)
            if (kvp.Value.ExpiresAt < now)
                _tickets.TryRemove(kvp.Key, out _);
    }

    // -----------------------------------------------------------------
    //  HASH — same shape as AuthService.HashPassword (SHA-256 lowercase hex)
    // -----------------------------------------------------------------
    public static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}