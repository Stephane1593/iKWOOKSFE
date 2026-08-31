using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IManagerAuthorizationService
{
    /// <summary>Validate a scanned/typed barcode payload.</summary>
    Task<AuthorizationResult> VerifyBarcodeAsync(
        string barcodePayload, ManagerAction action, AuthorizationContext ctx);

    /// <summary>Validate a manager PIN (username optional — if null, tries all users with a PIN set).</summary>
    Task<AuthorizationResult> VerifyPinAsync(
        string? username, string pin, ManagerAction action, AuthorizationContext ctx);

    /// <summary>Validate full username+password. Highest assurance.</summary>
    Task<AuthorizationResult> VerifyCredentialsAsync(
        string username, string password, ManagerAction action, AuthorizationContext ctx);

    /// <summary>Consume a ticket. Returns true if valid & not yet consumed. Single-use.</summary>
    bool TryConsumeTicket(Guid ticketId, ManagerAction action);
}

public sealed class AuthorizationContext
{
    public string? InvoiceNumber { get; init; }
    public int? InvoiceId { get; init; }
    public decimal Amount { get; init; }
    public string? Reason { get; init; }
    /// <summary>The cashier requesting authorization (usually the current logged-in user).</summary>
    public int? RequestingUserId { get; init; }
    public string? RequestingUserName { get; init; }
}

public sealed class AuthorizationResult
{
    public bool Granted { get; init; }
    public Guid TicketId { get; init; }
    public int? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public string? ErrorMessage { get; init; }

    public static AuthorizationResult Ok(Guid id, int mgrId, string mgrName)
        => new() { Granted = true, TicketId = id, ManagerId = mgrId, ManagerName = mgrName };

    public static AuthorizationResult Fail(string msg)
        => new() { Granted = false, ErrorMessage = msg };
}