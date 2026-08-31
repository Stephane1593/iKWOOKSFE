using Microsoft.Extensions.Logging;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;
using SFE.Licensing.Domain;
using SFE.Licensing.Local.MachineFingerprintProviders;
using SFE.Licensing.Local.Signing;
using SFE.Licensing.Local.Storage;
using SFE.Licensing.Local.Time;

namespace SFE.Licensing.Local;

public sealed class LicenseGuard : ILicenseGuard
{
    private readonly ILicenseVerifier _verifier;
    private readonly ILocalLicenseStore _store;
    private readonly IMachineFingerprintProvider _fingerprint;
    private readonly IAntiClockTamper _clock;
    private readonly ITimeProvider _time;
    private readonly IAuditService _audit;
    private readonly ITrialIssuer _trialIssuer;
    private readonly ILogger<LicenseGuard> _log;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private LicenseSnapshot _current = new(
        Status: LicenseStatus.Unknown,
        Claims: null,
        EvaluatedAtUtc: DateTimeOffset.MinValue,
        LastSuccessfulContactUtc: null,
        GraceStartedAtUtc: null,
        DaysUntilExpiry: null,
        DaysOfGraceRemaining: null,
        Reason: null);

    public LicenseSnapshot Current => _current;
    public event Action<LicenseSnapshot>? StatusChanged;

    public LicenseGuard(
        ILicenseVerifier verifier,
        ILocalLicenseStore store,
        IMachineFingerprintProvider fingerprint,
        IAntiClockTamper clock,
        ITimeProvider time,
        IAuditService audit,
        ITrialIssuer trialIssuer,
        ILogger<LicenseGuard> log)
    {
        _verifier = verifier;
        _store = store;
        _fingerprint = fingerprint;
        _clock = clock;
        _time = time;
        _audit = audit;
        _trialIssuer = trialIssuer;
        _log = log;
    }

    // -------------------------------------------------------
    //  Boot
    // -------------------------------------------------------

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var state = await _store.ReadStateAsync(ct);
            var fp = _fingerprint.Compute();

            if (string.IsNullOrEmpty(state.Fingerprint))
            {
                state.Fingerprint = fp.Value;
                await _store.WriteStateAsync(state, ct);
            }
            else if (!string.Equals(state.Fingerprint, fp.Value, StringComparison.Ordinal))
            {
                _log.LogWarning(
                    "Machine fingerprint changed. Was {Old}, now {New}. This install must be re-activated.",
                    state.Fingerprint, fp.Value);
                state.TamperSuspected = true;
                await _store.WriteStateAsync(state, ct);
            }

            var blob = await _store.ReadLicenseBlobAsync(ct);
            if (string.IsNullOrEmpty(blob))
            {
                _log.LogInformation("No license file found; issuing trial.");
                blob = await _trialIssuer.IssueTrialAsync(fp, ct);
                if (blob is not null)
                {
                    await _store.WriteLicenseBlobAsync(blob, ct);
                    state = await _store.ReadStateAsync(ct);
                    state.TrialIssuedAtUtc = _time.UtcNow;
                    await _store.WriteStateAsync(state, ct);
                    _audit.Log(AuditAction.LicenseTrialIssued, AuditModule.Licensing,
                        "Licence d'essai (30 jours) émise.");
                }
            }
        }
        finally { _gate.Release(); }

        await ReevaluateAsync(ct);
    }

    // -------------------------------------------------------
    //  Reevaluate (the heart of the guard)
    // -------------------------------------------------------

    public async Task<LicenseSnapshot> ReevaluateAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var (trustedNow, tampered) = await _clock.ObserveAsync(ct);
            var state = await _store.ReadStateAsync(ct);

            var blob = await _store.ReadLicenseBlobAsync(ct);
            if (string.IsNullOrEmpty(blob))
            {
                return Update(new LicenseSnapshot(
                    LicenseStatus.Expired, null, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    null, null, "Aucune licence installée."));
            }

            LicenseClaims claims;
            try
            {
                claims = _verifier.Verify(blob);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "License verification failed.");
                return Update(new LicenseSnapshot(
                    LicenseStatus.Tampered, null, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    null, null, "La signature de la licence est invalide."));
            }

            // -- Machine binding check --
            var currentFp = _fingerprint.Compute();
            if (!string.IsNullOrEmpty(claims.BoundFingerprint) &&
                !string.Equals(claims.BoundFingerprint, currentFp.Value, StringComparison.Ordinal))
            {
                return Update(new LicenseSnapshot(
                    LicenseStatus.Tampered, claims, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    null, null, "Licence liée à une autre machine."));
            }

            // -- Sticky tamper / revocation --
            if (state.TamperSuspected || tampered)
            {
                return Update(new LicenseSnapshot(
                    LicenseStatus.Tampered, claims, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    null, null, "Anomalie d'horloge ou d'empreinte machine détectée."));
            }

            if (state.Revoked)
            {
                return Update(new LicenseSnapshot(
                    LicenseStatus.Suspended, claims, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    null, null, state.PortalMessage ?? "Licence suspendue par le fournisseur."));
            }

            // -- Time-based transitions --
            var daysUntilExpiry = (int)Math.Ceiling((claims.ExpiresAt - trustedNow).TotalDays);
            var pastExpiry = trustedNow > claims.ExpiresAt;
            var graceDeadline = claims.ExpiresAt.AddDays(claims.GraceDays);
            var pastGrace = trustedNow > graceDeadline;

            if (pastGrace)
            {
                return Update(new LicenseSnapshot(
                    LicenseStatus.Expired, claims, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    daysUntilExpiry, 0, "Licence expirée et délai de grâce écoulé."));
            }

            if (pastExpiry)
            {
                var graceLeft = (int)Math.Ceiling((graceDeadline - trustedNow).TotalDays);
                return Update(new LicenseSnapshot(
                    LicenseStatus.GracePeriod, claims, trustedNow,
                    state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                    daysUntilExpiry, graceLeft,
                    $"Licence expirée — délai de grâce ({graceLeft} j restants)."));
            }

            // -- Online freshness (does not apply to trial: trials are 100% offline) --
            var status = claims.IsTrial ? LicenseStatus.Trial : LicenseStatus.Active;

            // Only enforce online-freshness once this install has EVER contacted a portal.
            // Offline-only deployments (v1, no portal) never set LastSuccessfulContactUtc,
            // so a valid license stays Active instead of falsely showing "no recent contact".
            if (!claims.IsTrial && state.LastSuccessfulContactUtc is { } lc)
            {
                var maxOffline = TimeSpan.FromHours(claims.HeartbeatIntervalHours * 4);
                if ((trustedNow - lc) > maxOffline)
                {
                    state.OfflineSinceUtc ??= trustedNow;
                    await _store.WriteStateAsync(state, ct);
                    status = LicenseStatus.ActiveOffline;
                }
                else if (state.OfflineSinceUtc is not null)
                {
                    state.OfflineSinceUtc = null;
                    await _store.WriteStateAsync(state, ct);
                }
            }

            return Update(new LicenseSnapshot(
                status, claims, trustedNow,
                state.LastSuccessfulContactUtc, state.OfflineSinceUtc,
                daysUntilExpiry, null,
                status == LicenseStatus.ActiveOffline
                    ? "Aucun contact récent avec le portail — mode hors-ligne."
                    : null));
        }
        finally { _gate.Release(); }
    }

    // -------------------------------------------------------
    //  Enforcement
    // -------------------------------------------------------

    public void Require(Feature feature)
    {
        if (!TryUse(feature, out var reason))
            throw new FeatureBlockedException(_current.Status, feature, reason ?? "Feature blocked.");
    }

    public bool TryUse(Feature feature, out string? reason)
    {
        var snap = _current;

        if (snap.Status.IsFatal())
        {
            reason = snap.Reason ?? "Licence inactive.";
            _audit.Log(AuditAction.LicenseFeatureBlocked, AuditModule.Licensing,
                $"Fonctionnalité bloquée ({feature}) — statut {snap.Status}.");
            return false;
        }

        if (snap.Claims is null || !snap.HasFeature(feature))
        {
            reason = $"La fonctionnalité « {feature} » n'est pas incluse dans votre licence.";
            _audit.Log(AuditAction.LicenseFeatureBlocked, AuditModule.Licensing,
                $"Fonctionnalité non incluse : {feature}.");
            return false;
        }

        reason = null;
        return true;
    }

    // -------------------------------------------------------
    //  Mutations (called by Client + admin UI)
    // -------------------------------------------------------

    public async Task<LicenseSnapshot> InstallLicenseAsync(string blob, CancellationToken ct = default)
    {
        var claims = _verifier.Verify(blob); // throws if invalid — do NOT overwrite existing on bad input

        var fp = _fingerprint.Compute();
        if (!string.IsNullOrEmpty(claims.BoundFingerprint) &&
            !string.Equals(claims.BoundFingerprint, fp.Value, StringComparison.Ordinal))
            throw new InvalidOperationException("Licence liée à une autre machine.");

        await _store.WriteLicenseBlobAsync(blob, ct);

        var state = await _store.ReadStateAsync(ct);
        state.LicenseId = claims.LicenseId;
        state.Revoked = false;
        state.PortalMessage = null;
        state.TamperSuspected = false;   // a valid, correctly-bound license clears tamper
        state.Fingerprint = fp.Value;
        await _store.WriteStateAsync(state, ct);

        _audit.Log(AuditAction.LicenseInstalled, AuditModule.Settings,
            $"Nouvelle licence installée : {claims.LicenseId} (expire {claims.ExpiresAt:yyyy-MM-dd}).");

        return await ReevaluateAsync(ct);
    }

    public async Task NoteSuccessfulContactAsync(DateTimeOffset atUtc, CancellationToken ct = default)
    {
        var state = await _store.ReadStateAsync(ct);
        if (state.LastSuccessfulContactUtc is null || atUtc > state.LastSuccessfulContactUtc)
        {
            state.LastSuccessfulContactUtc = atUtc;
            state.OfflineSinceUtc = null;
            await _store.WriteStateAsync(state, ct);
        }
        await ReevaluateAsync(ct);
    }

    public async Task MarkRevokedAsync(string? portalMessage, CancellationToken ct = default)
    {
        var state = await _store.ReadStateAsync(ct);
        state.Revoked = true;
        state.PortalMessage = portalMessage;
        await _store.WriteStateAsync(state, ct);

        _audit.Log(AuditAction.LicenseRevokedByPortal, AuditModule.Licensing,
            $"Licence révoquée par le portail : {portalMessage ?? "(aucun motif)"}.");

        await ReevaluateAsync(ct);
    }

    // -------------------------------------------------------
    //  Internal
    // -------------------------------------------------------

    private LicenseSnapshot Update(LicenseSnapshot next)
    {
        var prev = _current;
        _current = next;
        if (prev.Status != next.Status)
        {
            _log.LogInformation("License status: {Prev} -> {Next} ({Reason})",
                prev.Status, next.Status, next.Reason);

            var action = next.Status switch
            {
                LicenseStatus.Expired => AuditAction.LicenseExpired,
                LicenseStatus.GracePeriod => AuditAction.LicenseEnteredGrace,
                LicenseStatus.ActiveOffline => AuditAction.LicenseEnteredOffline,
                LicenseStatus.Tampered => AuditAction.LicenseTamperDetected,
                LicenseStatus.Suspended => AuditAction.LicenseRevokedByPortal,
                _ => (AuditAction?)null
            };
            if (action is { } a)
            {
                _audit.Log(a, AuditModule.Licensing,
                    $"Statut de licence : {prev.Status} -> {next.Status}. {next.Reason}".Trim());
            }

            try { StatusChanged?.Invoke(next); } catch { /* subscriber errors are their problem */ }
        }
        return next;
    }
}