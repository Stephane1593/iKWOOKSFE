using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Repositories;

public sealed class PaymentTransactionRepository(AppDbContext db) : IPaymentTransactionRepository
{
    public Task<PaymentTransaction?> FindAsync(string key, CancellationToken ct) =>
        db.PaymentTransactions.FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);

    public async Task AddAsync(PaymentTransaction tx, CancellationToken ct) =>
        await db.PaymentTransactions.AddAsync(tx, ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<PaymentTransaction>> GetStuckAsync(
    DateTime olderThanUtc, CancellationToken ct)
    {
        return await db.PaymentTransactions
            .Where(t =>
                (t.Status == PaymentTransactionStatus.Processing ||
                 t.Status == PaymentTransactionStatus.TimedOut) &&
                t.UpdatedUtc < olderThanUtc)
            .OrderBy(t => t.UpdatedUtc)
            .Take(50)                         // batch cap; adjust to taste
            .ToListAsync(ct);
    }

    public async Task BumpAttemptAsync(string idempotencyKey, CancellationToken ct)
    {
        var tx = await db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
        if (tx is null) return;
        tx.BumpAttempt();
        await db.SaveChangesAsync(ct);
    }
}