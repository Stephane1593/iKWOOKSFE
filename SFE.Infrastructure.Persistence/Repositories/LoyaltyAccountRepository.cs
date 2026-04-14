using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class LoyaltyAccountRepository : Repository<LoyaltyAccount>, ILoyaltyAccountRepository
{
    public LoyaltyAccountRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<LoyaltyAccount?> GetByClientIdAsync(int clientId)
    {
        return await _dbSet.FirstOrDefaultAsync(la => la.ClientId == clientId);
    }

    public async Task<LoyaltyAccount?> GetByCardNumberAsync(string cardNumber)
    {
        return await _dbSet.FirstOrDefaultAsync(la => la.CardNumber == cardNumber);
    }

    public async Task<LoyaltyAccount?> GetWithTransactionsAsync(int accountId)
    {
        return await _dbSet
            .Include(la => la.Transactions.OrderByDescending(t => t.Timestamp).Take(50))
            .FirstOrDefaultAsync(la => la.Id == accountId);
    }
}