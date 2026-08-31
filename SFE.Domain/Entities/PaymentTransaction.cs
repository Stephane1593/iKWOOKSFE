using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// Authoritative record of a terminal payment. The idempotency key IS the primary key,
/// so a retried POST /payments can never create a second charge.
/// </summary>
public class PaymentTransaction
{
    public string IdempotencyKey { get; private set; } = default!;
    public string OrderId { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string Method { get; private set; } = default!;   // "Cash","Mpesa","CardVisa"... kept as string to stay decoupled
    public PaymentTransactionStatus Status { get; private set; }
    public string? ProviderRef { get; private set; }         // e.g. M-PESA receipt / card auth code
    public string? FailureReason { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    // Add near the other properties:
    public int Attempts { get; private set; }

    private PaymentTransaction() { }

    public static PaymentTransaction Start(string key, string orderId, decimal amount, string method) => new()
    {
        IdempotencyKey = key,
        OrderId = orderId,
        Amount = amount,
        Method = method,
        Status = PaymentTransactionStatus.Initiated,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow
    };

    // Guarded transitions: INITIATED -> PROCESSING -> APPROVED/DECLINED/TIMEOUT -> RECONCILED
    public void MarkProcessing() { Require(PaymentTransactionStatus.Initiated); Set(PaymentTransactionStatus.Processing); }
    public void MarkApproved(string reference)
    {
        Require(PaymentTransactionStatus.Processing, PaymentTransactionStatus.TimedOut);
        ProviderRef = reference; Set(PaymentTransactionStatus.Approved);
    }

    /// <summary>Increment the reconciliation attempt counter. Never transitions status.</summary>
    public void BumpAttempt()
    {
        Attempts += 1;
        UpdatedUtc = DateTime.UtcNow;
    }
    public void MarkDeclined(string reason)
    {
        Require(PaymentTransactionStatus.Processing, PaymentTransactionStatus.TimedOut);
        FailureReason = reason; Set(PaymentTransactionStatus.Declined);
    }
    public void MarkTimedOut() { Require(PaymentTransactionStatus.Processing); Set(PaymentTransactionStatus.TimedOut); }

    public void Reconcile(PaymentTransactionStatus resolved, string? reference, string? reason)
    {
        Require(PaymentTransactionStatus.TimedOut, PaymentTransactionStatus.Processing);
        ProviderRef = reference;
        FailureReason = reason;
        Set(resolved is PaymentTransactionStatus.Approved or PaymentTransactionStatus.Declined
            ? resolved : PaymentTransactionStatus.Reconciled);
    }

    public bool IsTerminal =>
        Status is PaymentTransactionStatus.Approved
               or PaymentTransactionStatus.Declined
               or PaymentTransactionStatus.Reconciled;

    private void Set(PaymentTransactionStatus s) { Status = s; UpdatedUtc = DateTime.UtcNow; }
    private void Require(params PaymentTransactionStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"Illegal payment transition from {Status}.");
    }
}