using SFE.Domain.Enums;

namespace SFE.Application.Payments;

// Wire DTOs (what crosses the LAN)
public record OrderDto(string OrderId, string Label, decimal Amount, string Currency);

public record InitiatePaymentRequest(string IdempotencyKey, string OrderId, decimal Amount, string Method);

public record PaymentResultReport(PaymentTransactionStatus Status, string? ProviderRef, string? Reason);

public record PaymentTransactionDto(
    string IdempotencyKey, string OrderId, decimal Amount, string Method,
    string Status, string? ProviderRef, string? FailureReason);

// Provider-side result (used by reconciliation / future real SDKs)
public record ProviderResult(PaymentTransactionStatus Status, string? ProviderRef, string? Reason);