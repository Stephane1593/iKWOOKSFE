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
        // Query settings directly from UOW to ensure fresh data
        var settings = await _uow.AppSettings.GetCurrentAsync();
        if (settings == null || !settings.LoyaltyEnabled || invoice.ClientType != ClientType.PP)
            return;

        if (string.IsNullOrWhiteSpace(invoice.ClientPhone))
            return;

        string phoneToMatch = invoice.ClientPhone.Trim();
        var matchingClients = await _uow.Clients.FindAsync(c => c.Phone == phoneToMatch);
        var client = matchingClients.FirstOrDefault();

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

            // Save immediately so the new account gets an Id from the database
            await _uow.SaveChangesAsync();
        }

        // Use explicit repository to guarantee EF Core tracks the new transactions
        var transactionRepo = _uow.GetRepository<LoyaltyTransaction>();

        // 1. Process Redemptions
        if (pointsToRedeem > 0 && account.CurrentBalance >= pointsToRedeem)
        {
            account.CurrentBalance -= pointsToRedeem;
            account.LastActivityAt = _time.UtcNow;

            await transactionRepo.AddAsync(new LoyaltyTransaction
            {
                LoyaltyAccountId = account.Id,
                InvoiceId = invoice.Id,
                Type = "REDEEM",
                Points = -pointsToRedeem,
                Description = $"Points utilisés sur facture {invoice.InvoiceNumber}",
                Timestamp = _time.UtcNow
            });

            await _audit.LogAsync(
                AuditAction.ClientUpdated,
                AuditModule.Clients,
                $"{pointsToRedeem} points utilisés par {client.Name} sur {invoice.InvoiceNumber}.",
                entityType: "LoyaltyAccount",
                entityId: account.Id.ToString());
        }

        // 2. Process Earnings
        if (settings.LoyaltyEarnRate > 0)
        {
            int pointsEarned = (int)(invoice.TotalTTC / settings.LoyaltyEarnRate);

            if (pointsEarned > 0)
            {
                account.CurrentBalance += pointsEarned;
                account.TotalPointsEarned += pointsEarned;
                account.LastActivityAt = _time.UtcNow;

                await transactionRepo.AddAsync(new LoyaltyTransaction
                {
                    LoyaltyAccountId = account.Id,
                    InvoiceId = invoice.Id,
                    Type = "EARN",
                    Points = pointsEarned,
                    Description = $"Points acquis sur facture {invoice.InvoiceNumber}",
                    Timestamp = _time.UtcNow
                });
            }
        }

        EvaluateTier(account);
        await _uow.LoyaltyAccounts.UpdateAsync(account);
        // FIX: Force the save here! This guarantees the points are committed 
        // even if the DI container gave this service a different UOW instance.
        await _uow.SaveChangesAsync();

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