using System.Collections.Concurrent;
using SFE.Application.Interfaces;
using SFE.Application.Payments;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// World B test double. QueryAsync is a *pure read* of a state machine that
/// only advances when someone calls ReportResult — normally that's the
/// POST /payments/{id}/result endpoint the paired Sunmi calls after the
/// porteur presses Approve/Decline on the terminal.
///
/// In dev, the two "Simuler le terminal" buttons on the till call
/// ReportResult directly, bypassing the HTTP hop. No time-based approval.
///
/// AutoMode (opt-in) is preserved for unit tests / smoke runs that need
/// the old fire-and-forget behaviour: set Mode ≠ ExternallyDriven and the
/// provider will resolve itself after SimulatedPayDelay. Production and
/// interactive dev keep the default (ExternallyDriven).
/// </summary>
public sealed class MockPaymentProvider : IPaymentProvider
{
    public enum Behaviour
    {
        /// <summary>Default. QueryAsync returns Processing until ReportResult is called.</summary>
        ExternallyDriven,
        /// <summary>Legacy: auto-approve after SimulatedPayDelay. For headless tests only.</summary>
        Approve,
        Decline,
        Timeout,
        Silent
    }

    public Behaviour Mode { get; set; } = Behaviour.ExternallyDriven;

    /// <summary>Only used when Mode ≠ ExternallyDriven.</summary>
    public TimeSpan SimulatedPayDelay { get; set; } = TimeSpan.FromSeconds(6);

    // key = idempotencyKey = InvoiceNumber
    private readonly ConcurrentDictionary<string, DateTimeOffset> _firstSeen = new();
    private readonly ConcurrentDictionary<string, ProviderResult> _reported = new();

    // ─────────────────────────────────────────────────────────────────
    // Called by the till (or Sunmi HTTP hop) to advance the state machine.
    // ─────────────────────────────────────────────────────────────────

    public void ReportResult(
        string key,
        PaymentTransactionStatus status,
        string? providerRef = null,
        string? reason = null)
    {
        _firstSeen.TryRemove(key, out _);

        _reported[key] = new ProviderResult(
            status,
            providerRef ?? $"MOCK-{key[..Math.Min(6, key.Length)]}",
            reason
        );
    }

    public void Reset(string key)
    {
        _firstSeen.TryRemove(key, out _);
        _reported.TryRemove(key, out _);
    }

    // ─────────────────────────────────────────────────────────────────
    // IPaymentProvider
    // ─────────────────────────────────────────────────────────────────

    public async Task<ProviderResult> ChargeAsync(InitiatePaymentRequest req, CancellationToken ct)
    {
        // ChargeAsync is the "till proactively pushes to a provider" path.
        // Under World B the till doesn't do that for card payments — the
        // Sunmi is the actor. Keep the legacy synchronous outcome for the
        // few code paths (e.g. Mobile Money in the future) that still call it.
        await Task.Delay(1200, ct);
        return Mode switch
        {
            Behaviour.Approve => new(PaymentTransactionStatus.Approved,
                                     $"MOCK-{Guid.NewGuid():N}"[..12], null),
            Behaviour.Decline => new(PaymentTransactionStatus.Declined,
                                     null, "Insufficient funds (mock)"),
            Behaviour.Timeout => new(PaymentTransactionStatus.TimedOut, null, null),
            Behaviour.Silent => throw new TimeoutException("Provider silent (mock)"),
            // ExternallyDriven doesn't make sense for a synchronous charge —
            // fall back to Processing so the caller can poll if it wants.
            _ => new(PaymentTransactionStatus.Processing, null, null),
        };
    }

public Task<ProviderResult> QueryAsync(string key, CancellationToken ct)
{
    // 1) Terminal state reported externally? Consume it once.
    if (_reported.TryRemove(key, out var reported))
    {
        _firstSeen.TryRemove(key, out _);
        return Task.FromResult(reported);
    }

    // 2) Externally-driven mode → stay Processing forever.
    // Only ReportResult can end this.
    if (Mode == Behaviour.ExternallyDriven)
    {
        _firstSeen.GetOrAdd(key, _ => DateTimeOffset.UtcNow);
        return Task.FromResult(
            new ProviderResult(PaymentTransactionStatus.Processing, null, null)
        );
    }

    // 3) Legacy auto-modes.
    var first = _firstSeen.GetOrAdd(key, _ => DateTimeOffset.UtcNow);
    var elapsed = DateTimeOffset.UtcNow - first;

    if (elapsed < SimulatedPayDelay)
    {
        return Task.FromResult(
            new ProviderResult(PaymentTransactionStatus.Processing, null, null)
        );
    }

    var providerRef = $"MOCK-{key[..Math.Min(6, key.Length)]}";

    var result = Mode switch
    {
        Behaviour.Approve => new ProviderResult(
            PaymentTransactionStatus.Approved,
            providerRef,
            null
        ),

        Behaviour.Decline => new ProviderResult(
            PaymentTransactionStatus.Declined,
            null,
            "Insufficient funds (mock)"
        ),

        Behaviour.Timeout => new ProviderResult(
            PaymentTransactionStatus.TimedOut,
            null,
            null
        ),

        Behaviour.Silent => new ProviderResult(
            PaymentTransactionStatus.Processing,
            null,
            null
        ),

        _ => new ProviderResult(
            PaymentTransactionStatus.Processing,
            null,
            null
        ),
    };

    if (result.Status is PaymentTransactionStatus.Approved
        or PaymentTransactionStatus.Declined
        or PaymentTransactionStatus.TimedOut)
    {
        _firstSeen.TryRemove(key, out _);
    }

    return Task.FromResult(result);
}
}