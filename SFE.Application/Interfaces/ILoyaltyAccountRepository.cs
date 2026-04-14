using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface ILoyaltyAccountRepository : IRepository<LoyaltyAccount>
{
    Task<LoyaltyAccount?> GetByClientIdAsync(int clientId);
    Task<LoyaltyAccount?> GetByCardNumberAsync(string cardNumber);
    Task<LoyaltyAccount?> GetWithTransactionsAsync(int accountId);
}