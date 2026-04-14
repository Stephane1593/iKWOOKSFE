using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IProductCategoryRepository : IRepository<ProductCategory>
{
    Task<List<ProductCategory>> GetActiveCategoriesAsync();
    Task<ProductCategory?> GetWithProductsAsync(int id);
}