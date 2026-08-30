using Microsoft.Extensions.Logging;
using SFE.Domain.Abstractions;
using SFE.Licensing.Local.Storage;

namespace SFE.Licensing.Local.Time;

public interface IAntiClockTamper
{
    /// <summary>
    /// Returns a trusted "now" that never moves backwards across observations
    /// and updates the persistent monotonic anchor. If the system clock is
    /// meaningfully earlier than the last observation OR earlier than the
    /// newest domain-persisted timestamp, marks state as tampered and returns
    /// the highest known instant instead.
    /// </summary>
    Task<(DateTimeOffset TrustedNow, bool Tampered)> ObserveAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Attempt to clear the sticky tamper flag. Only succeeds when the caller
    /// provides an authoritative timestamp (e.g. a signed portal heartbeat)
    /// AND that timestamp agrees with the domain anchor within slack.
    /// </summary>
    Task<bool> TryClearTamperAsync(
        DateTimeOffset authoritativeUtc,
        CancellationToken ct = default);
}

public sealed class AntiClockTamper : IAntiClockTamper
{
    private readonly ITimeProvider _time;
    private readonly ILocalLicenseStore _store;
    private readonly IMonotonicClockAnchor _anchor;
    private readonly ILogger<AntiClockTamper> _log;

    private static readonly TimeSpan SmallSlack = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HardTamperThreshold = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public AntiClockTamper(
        ITimeProvider time,
        ILocalLicenseStore store,
        IMonotonicClockAnchor anchor,
        ILogger<AntiClockTamper> log)
    {
        _time = time;
        _store = store;
        _anchor = anchor;
        _log = log;
    }

    public async Task<(DateTimeOffset TrustedNow, bool Tampered)> ObserveAsync(
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = _time.UtcNow;
            var state = await _store.ReadStateAsync(ct);
            var anchorUtc = await _anchor.GetLatestPersistedUtcAsync(ct);
            var highWater = Max(state.LastKnownUtc, anchorUtc);

            bool tampered = state.TamperSuspected;
            DateTimeOffset trusted;

            if (highWater is { } hw)
            {
                var backwardDrift = hw - now;

                if (backwardDrift > HardTamperThreshold)
                {
                    _log.LogWarning(
                        "Clock rollback detected. now={Now:o} highWater={High:o} delta={Delta}. Marking TamperSuspected.",
                        now, hw, backwardDrift);
                    tampered = true;
                    state.TamperSuspected = true;
                    trusted = hw;
                }
                else if (backwardDrift > SmallSlack)
                {
                    trusted = hw;
                }
                else
                {
                    trusted = now > hw ? now : hw;   // forward jumps are harmless
                }
            }
            else
            {
                trusted = now;
            }

            state.LastKnownUtc = trusted;
            await _store.WriteStateAsync(state, ct);
            return (trusted, tampered);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> TryClearTamperAsync(
        DateTimeOffset authoritativeUtc,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var state = await _store.ReadStateAsync(ct);
            if (!state.TamperSuspected) return true;

            var anchor = await _anchor.GetLatestPersistedUtcAsync(ct);
            if (anchor is { } a && (a - authoritativeUtc).Duration() > SmallSlack)
            {
                _log.LogWarning(
                    "Refusing to clear tamper: authoritative={Auth:o} disagrees with domain anchor={Anchor:o}.",
                    authoritativeUtc, a);
                return false;
            }

            state.TamperSuspected = false;
            state.LastKnownUtc = Max(state.LastKnownUtc, authoritativeUtc);
            await _store.WriteStateAsync(state, ct);

            _log.LogInformation(
                "Tamper flag cleared by authoritative timestamp {Auth:o}.",
                authoritativeUtc);
            return true;
        }
        finally { _gate.Release(); }
    }

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a > b ? a : b;
    }
}