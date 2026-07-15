using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> FindAsync(string idempotencyKey, CancellationToken ct);
    Task AddAsync(PaymentTransaction tx, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);

    /// <summary>
    /// Returns transactions whose Status is Processing or TimedOut AND whose
    /// UpdatedUtc is older than <paramref name="olderThanUtc"/>. Initiated is
    /// excluded because PaymentService flips to Processing in the same unit
    /// of work as creation, so a persisted Initiated is a bug, not a stuck tx.
    /// </summary>
    Task<IReadOnlyList<PaymentTransaction>> GetStuckAsync(DateTime olderThanUtc, CancellationToken ct);

    /// <summary>Persist an Attempts++ for the given transaction. No status change.</summary>
    Task BumpAttemptAsync(string idempotencyKey, CancellationToken ct);
}