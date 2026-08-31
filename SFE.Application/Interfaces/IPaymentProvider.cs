using SFE.Application.Payments;

namespace SFE.Application.Interfaces;

/// <summary>The seam real M-PESA / Orange / bank SDKs slot into later.</summary>
public interface IPaymentProvider
{
    Task<ProviderResult> ChargeAsync(InitiatePaymentRequest req, CancellationToken ct);
    Task<ProviderResult> QueryAsync(string idempotencyKey, CancellationToken ct); // reconciliation safety net

}