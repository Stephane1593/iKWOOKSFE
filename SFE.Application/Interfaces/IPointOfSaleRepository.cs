using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IPointOfSaleRepository : IRepository<PointOfSale>
{
    Task<List<PointOfSale>> GetByCompanyIdAsync(int companyId);
    Task<PointOfSale?> GetActiveByCodeAsync(string code);
    Task<List<PointOfSale>> GetActiveAsync();

    // 🆕 Load POS with operators count (for management page)
    Task<List<PointOfSale>> GetByCompanyWithOperatorsAsync(int companyId);
    Task<PointOfSale?> GetWithOperatorsAsync(int posId);
}