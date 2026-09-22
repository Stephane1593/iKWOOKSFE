using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class LoyaltyService
{
    private readonly IUnitOfWork _uow;
    private readonly ITimeProvider _time;
    private readonly IAuditService _audit;
    private readonly SettingsService _settings;

    public LoyaltyService(IUnitOfWork uow, ITimeProvider time, IAuditService audit, SettingsService settings)
    {
        _uow = uow;
        _time = time;
        _audit = audit;
        _settings = settings;
    }

    /// <summary>
    /// Processes points earned and points redeemed for a successfully normalized invoice.
    /// MUST be called inside the SaveChanges block of Invoice normalization.
    /// </summary>
    public async Task ProcessInvoiceLoyaltyAsync(Invoice invoice, int pointsToRedeem)
    {
        var settings = await _settings.LoadSettingsAsync();
        if (!settings.LoyaltyEnabled || invoice.ClientType != ClientType.PP)
            return; // Loyalty only applies to physical persons by default

        // We need the client to look up or create the account
        var client = await _uow.Clients.GetByNIFAsync(invoice.ClientNIF ?? "")
                     ?? await _uow.Clients.SearchAsync(invoice.ClientName).ContinueWith(t => t.Result.FirstOrDefault());

        if (client == null) return;

        var account = await _uow.LoyaltyAccounts.GetByClientIdAsync(client.Id);
        if (account == null)
        {
            account = new LoyaltyAccount
            {
                ClientId = client.Id,
                CardNumber = GenerateCardNumber(client.Id),
                EnrolledAt = _time.UtcNow,
                CurrentBalance = 0,
                TotalPointsEarned = 0
            };
            await _uow.LoyaltyAccounts.AddAsync(account);
        }

        // 1. Process Redemptions (if they asked to use points)
        if (pointsToRedeem > 0 && account.CurrentBalance >= pointsToRedeem)
        {
            account.CurrentBalance -= pointsToRedeem;
            account.LastActivityAt = _time.UtcNow;

            account.Transactions.Add(new LoyaltyTransaction
            {
                InvoiceId = invoice.Id,
                Type = "REDEEM",
                Points = -pointsToRedeem,
                Description = $"Points utilisés sur facture {invoice.InvoiceNumber}",
                Timestamp = _time.UtcNow
            });

            await _audit.LogAsync(
                AuditAction.ClientUpdated, // Standardize your audit actions as needed
                AuditModule.Clients,
                $"{pointsToRedeem} points utilisés par {client.Name} sur {invoice.InvoiceNumber}.",
                entityType: "LoyaltyAccount",
                entityId: account.Id.ToString());
        }

        // 2. Process Earnings (Points earned on the final TTC paid)
        if (settings.LoyaltyEarnRate > 0)
        {
            // E.g., Earn 1 point per 1000 CDF spent
            int pointsEarned = (int)(invoice.TotalTTC / settings.LoyaltyEarnRate);

            if (pointsEarned > 0)
            {
                account.CurrentBalance += pointsEarned;
                account.TotalPointsEarned += pointsEarned;
                account.LastActivityAt = _time.UtcNow;

                account.Transactions.Add(new LoyaltyTransaction
                {
                    InvoiceId = invoice.Id,
                    Type = "EARN",
                    Points = pointsEarned,
                    Description = $"Points acquis sur facture {invoice.InvoiceNumber}",
                    Timestamp = _time.UtcNow
                });
            }
        }

        // Tier evaluation
        EvaluateTier(account);
    }

    private void EvaluateTier(LoyaltyAccount account)
    {
        if (account.TotalPointsEarned >= 10000) account.TierLevel = LoyaltyTierLevel.Platinum;
        else if (account.TotalPointsEarned >= 5000) account.TierLevel = LoyaltyTierLevel.Gold;
        else if (account.TotalPointsEarned >= 1000) account.TierLevel = LoyaltyTierLevel.Silver;
        else account.TierLevel = LoyaltyTierLevel.Bronze;
    }

    private string GenerateCardNumber(int clientId)
    {
        return $"LC-{_time.UtcNow.Year}-{clientId:D5}";
    }
}