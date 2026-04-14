// File: SFE.Application/Interfaces/IPosStockRepository.cs
using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IPosStockRepository : IRepository<PosStock>
{
    /// <summary>Stock d'un produit dans un POS donné</summary>
    Task<PosStock?> GetByProductAndPosAsync(int productId, int pointOfSaleId);

    /// <summary>Tous les stocks d'un POS</summary>
    Task<List<PosStock>> GetByPosAsync(int pointOfSaleId);

    /// <summary>Tous les stocks d'un produit (tous POS)</summary>
    Task<List<PosStock>> GetByProductAsync(int productId);

    /// <summary>Produits en stock bas dans un POS</summary>
    Task<List<PosStock>> GetLowStockByPosAsync(int pointOfSaleId);

    /// <summary>Produits en rupture dans un POS</summary>
    Task<List<PosStock>> GetOutOfStockByPosAsync(int pointOfSaleId);

    /// <summary>Tous les stocks bas (tous POS)</summary>
    Task<List<PosStock>> GetAllLowStockAsync();

    /// <summary>Stock total d'un produit (somme de tous les POS)</summary>
    Task<decimal> GetTotalStockAsync(int productId);

    /// <summary>Recherche produits avec stock dans un POS</summary>
    Task<List<PosStock>> SearchInPosAsync(int pointOfSaleId, string query, int maxResults = 30);

    /// <summary>Nombre d'alertes stock pour un POS</summary>
    Task<int> GetLowStockCountAsync(int pointOfSaleId);

    /// <summary>Nombre d'alertes stock global</summary>
    Task<int> GetTotalLowStockCountAsync();
}