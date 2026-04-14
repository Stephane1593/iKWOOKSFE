using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetByCodeAsync(string code);
    Task<Product?> GetByBarcodeAsync(string barcode);
    Task<List<Product>> GetActiveAsync();
    Task<List<Product>> SearchAsync(string searchTerm);
    Task<List<Product>> GetByTaxGroupAsync(TaxGroup taxGroup);
    Task<List<Product>> GetActiveProductsAsync();
    Task<List<Product>> SearchAsync(string query, int maxResults = 20); 
    Task<List<Product>> GetByCategoryAsync(int categoryId);
    Task<List<Product>> GetFavoritesAsync();
    Task<List<Product>> GetLowStockAsync();
    Task<int> GetActiveCountAsync();
}