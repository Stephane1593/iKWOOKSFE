using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IClientRepository : IRepository<Client>
{
    Task<Client?> GetByNIFAsync(string nif);
    Task<List<Client>> SearchAsync(string searchTerm);
    Task<Client?> GetWithLoyaltyAccountAsync(int clientId);
    Task<List<Client>> GetLoyaltyMembersAsync();
}