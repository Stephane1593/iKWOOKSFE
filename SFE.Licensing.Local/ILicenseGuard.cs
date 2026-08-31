using SFE.Licensing.Domain;

namespace SFE.Licensing.Local;

public interface ILicenseGuard
{
    LicenseSnapshot Current { get; }
    event Action<LicenseSnapshot>? StatusChanged;

    /// <summary>
    /// Called once at app boot. Loads the license from disk, or issues a trial
    /// if none exists, computes the current status, and wires up the state file.
    /// Idempotent — safe to call from multiple places.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Re-evaluates status. Call this after installing a new license, after
    /// a heartbeat, or on a timer. Returns the new snapshot.
    /// </summary>
    Task<LicenseSnapshot> ReevaluateAsync(CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="FeatureBlockedException"/> if the feature is not
    /// allowed by the current license OR if status is fatal. Use at the
    /// ViewModel command entry point.
    /// </summary>
    void Require(Feature feature);

    /// <summary>Same as <see cref="Require"/> but non-throwing.</summary>
    bool TryUse(Feature feature, out string? reason);

    /// <summary>Install a new blob (from portal activation or manual .lic file).</summary>
    Task<LicenseSnapshot> InstallLicenseAsync(string blob, CancellationToken ct = default);

    /// <summary>Called by the heartbeat client after each successful contact.</summary>
    Task NoteSuccessfulContactAsync(DateTimeOffset atUtc, CancellationToken ct = default);

    /// <summary>Called by the heartbeat client when the portal reports revocation.</summary>
    Task MarkRevokedAsync(string? portalMessage, CancellationToken ct = default);
}