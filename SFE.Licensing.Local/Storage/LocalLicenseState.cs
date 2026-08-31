using SFE.Licensing.Domain;

namespace SFE.Licensing.Local.Storage;

/// <summary>
/// Sidecar file (<c>license.state.json</c>). This is NOT signed — it's our own
/// local bookkeeping. Nothing in here is a security decision on its own; the
/// signed <c>LicenseClaims</c> is always the source of truth. State exists to:
///  - detect clock rollback (LastKnownUtc must never move backwards)
///  - remember the fingerprint so we notice hardware swaps
///  - remember when we entered offline grace, without trusting the system clock
/// </summary>
public sealed class LocalLicenseState
{
    public string? Fingerprint { get; set; }
    public string? LicenseId { get; set; }

    /// <summary>Monotonic upper bound of any timestamp we've ever observed.</summary>
    public DateTimeOffset? LastKnownUtc { get; set; }

    /// <summary>Last time a heartbeat (or activation) succeeded against the portal.</summary>
    public DateTimeOffset? LastSuccessfulContactUtc { get; set; }

    /// <summary>Set the first time we notice we're offline past the tolerance.</summary>
    public DateTimeOffset? OfflineSinceUtc { get; set; }

    /// <summary>Set when portal replies with revoked=true.</summary>
    public bool Revoked { get; set; }

    /// <summary>Portal-provided message, shown in the banner (e.g., "Payment overdue").</summary>
    public string? PortalMessage { get; set; }

    /// <summary>Sticky flag — once we suspect tampering, we never silently clear it.</summary>
    public bool TamperSuspected { get; set; }

    public DateTimeOffset? TrialIssuedAtUtc { get; set; }
}