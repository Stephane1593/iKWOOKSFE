using SFE.Licensing.Domain;

namespace SFE.Licensing.Local;

public interface ITrialIssuer
{
    /// <summary>
    /// Produce a signed trial license blob for this machine. Returns null if
    /// the app was built without trial support (production builds may choose
    /// to disable trials entirely).
    /// </summary>
    Task<string?> IssueTrialAsync(MachineFingerprint fp, CancellationToken ct = default);
}

/// <summary>
/// Null issuer — used in production builds where you'd rather have the customer
/// call you than start a trial. Registered by default; swap in DI to enable trials.
/// </summary>
public sealed class NoTrialIssuer : ITrialIssuer
{
    public Task<string?> IssueTrialAsync(MachineFingerprint fp, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}