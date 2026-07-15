using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFE.Application.Interfaces;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public sealed class PaymentReconciliationOptions
{
    /// <summary>How often the loop wakes up.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// A transaction is considered "stuck" and eligible for reconciliation
    /// once this much time has passed since its last update.
    /// </summary>
    public TimeSpan StuckAfter { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Give up after this many query attempts and mark as Declined.</summary>
    public int MaxAttempts { get; set; } = 5;
}

public sealed class PaymentReconciliationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PaymentReconciliationOptions _opts;
    private readonly ILogger<PaymentReconciliationService> _log;

    public PaymentReconciliationService(
        IServiceScopeFactory scopes,
        IOptions<PaymentReconciliationOptions> opts,
        ILogger<PaymentReconciliationService> log)
    {
        _scopes = scopes;
        _opts = opts.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "Reconciler started. poll={Poll}s stuckAfter={Stuck}s maxAttempts={Max}",
            _opts.PollInterval.TotalSeconds,
            _opts.StuckAfter.TotalSeconds,
            _opts.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await TickAsync(scope, stoppingToken);
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _log.LogError(ex, "Reconciler tick failed; will retry.");
            }

            try { await Task.Delay(_opts.PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(IServiceScope scope, CancellationToken ct)
    {
        var repo = scope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();

        var cutoff = DateTime.UtcNow - _opts.StuckAfter;
        var stuck = await repo.GetStuckAsync(cutoff, ct);

        foreach (var tx in stuck)
        {
            ct.ThrowIfCancellationRequested();
            if (tx.IsTerminal) continue; // defensive

            var result = await provider.QueryAsync(tx.IdempotencyKey, ct);

            switch (result.Status)
            {
                case PaymentTransactionStatus.Approved:
                case PaymentTransactionStatus.Declined:
                    tx.Reconcile(result.Status, result.ProviderRef, result.Reason);
                    await repo.SaveAsync(ct);
                    _log.LogInformation(
                        "Reconciled {Key} -> {Final}", tx.IdempotencyKey, result.Status);
                    break;

                case PaymentTransactionStatus.Processing:
                    // Provider still thinking. Bump attempts; give up if exhausted.
                    if (tx.Attempts + 1 >= _opts.MaxAttempts)
                    {
                        tx.Reconcile(
                            PaymentTransactionStatus.Declined,
                            tx.ProviderRef,
                            "Exceeded max reconciliation attempts");
                        await repo.SaveAsync(ct);
                        _log.LogWarning(
                            "Giving up on {Key} after {N} attempts -> Declined",
                            tx.IdempotencyKey, tx.Attempts + 1);
                    }
                    else
                    {
                        await repo.BumpAttemptAsync(tx.IdempotencyKey, ct);
                    }
                    break;

                case PaymentTransactionStatus.TimedOut:
                    // Provider also lost it. Treat as Declined for safety
                    // (never assume money moved).
                    tx.Reconcile(
                        PaymentTransactionStatus.Declined,
                        tx.ProviderRef,
                        result.Reason ?? "Provider reported TimedOut on query");
                    await repo.SaveAsync(ct);
                    _log.LogWarning("Provider lost {Key}; marked Declined", tx.IdempotencyKey);
                    break;

                default:
                    _log.LogWarning(
                        "Unexpected query status {S} for {Key}; leaving alone",
                        result.Status, tx.IdempotencyKey);
                    break;
            }
        }
    }
}