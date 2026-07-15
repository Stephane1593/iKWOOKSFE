using SFE.Application.Interfaces;
using SFE.Application.Payments;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public sealed class PaymentService(
    IPaymentTransactionRepository repo,
    IPaymentProvider provider)
{
    /// <summary>Idempotent: a repeated key returns the same transaction, never a second charge.</summary>
    public async Task<PaymentTransaction> InitiateAsync(InitiatePaymentRequest req, CancellationToken ct)
    {
        var existing = await repo.FindAsync(req.IdempotencyKey, ct);
        if (existing is not null) return existing;

        var tx = PaymentTransaction.Start(req.IdempotencyKey, req.OrderId, req.Amount, req.Method);
        await repo.AddAsync(tx, ct);
        tx.MarkProcessing();          // now awaiting the terminal's POST /result (or provider-driven charge)
        await repo.SaveAsync(ct);
        return tx;
    }

    public Task<PaymentTransaction?> GetAsync(string key, CancellationToken ct) => repo.FindAsync(key, ct);

    /// <summary>Terminal reports the outcome back here.</summary>
    public async Task<PaymentTransaction?> ReportResultAsync(string key, PaymentResultReport r, CancellationToken ct)
    {
        var tx = await repo.FindAsync(key, ct);
        if (tx is null || tx.IsTerminal) return tx;

        switch (r.Status)
        {
            case PaymentTransactionStatus.Approved: tx.MarkApproved(r.ProviderRef ?? "unknown"); break;
            case PaymentTransactionStatus.Declined: tx.MarkDeclined(r.Reason ?? "declined"); break;
            case PaymentTransactionStatus.TimedOut: tx.MarkTimedOut(); break;
        }
        await repo.SaveAsync(ct);
        return tx;
    }

    /// <summary>Safety net: ask the provider what really happened to a stuck/timed-out transaction.</summary>
    public async Task<PaymentTransaction?> ReconcileAsync(string key, CancellationToken ct)
    {
        var tx = await repo.FindAsync(key, ct);
        if (tx is null || tx.IsTerminal) return tx;
        var r = await provider.QueryAsync(key, ct);
        tx.Reconcile(r.Status, r.ProviderRef, r.Reason);
        await repo.SaveAsync(ct);
        return tx;
    }

    public static PaymentTransactionDto ToDto(PaymentTransaction t) => new(
        t.IdempotencyKey, t.OrderId, t.Amount, t.Method,
        t.Status.ToString(), t.ProviderRef, t.FailureReason);
}