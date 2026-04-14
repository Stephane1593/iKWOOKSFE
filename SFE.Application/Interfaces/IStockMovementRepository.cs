// File: SFE.Application/Interfaces/IStockMovementRepository.cs
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IStockMovementRepository : IRepository<StockMovement>
{
    /// <summary>Mouvements d'un produit dans un POS</summary>
    Task<List<StockMovement>> GetByProductAndPosAsync(int productId, int pointOfSaleId, int maxResults = 50);

    /// <summary>Mouvements d'un POS</summary>
    Task<List<StockMovement>> GetByPosAsync(int pointOfSaleId, DateTime? from = null, DateTime? to = null);

    /// <summary>Mouvements par référence (ex: numéro de facture)</summary>
    Task<List<StockMovement>> GetByReferenceAsync(string reference);

    /// <summary>Mouvements par type</summary>
    Task<List<StockMovement>> GetByTypeAsync(StockMovementType type, int pointOfSaleId, DateTime? from = null, DateTime? to = null);

    /// <summary>Mouvements liés à un transfert</summary>
    Task<List<StockMovement>> GetByTransferReferenceAsync(string transferReference);

    /// <summary>Recherche paginée</summary>
    Task<(List<StockMovement> Items, int TotalCount)> SearchAsync(
        StockMovementSearchCriteria criteria, int page, int pageSize);
}

public class StockMovementSearchCriteria
{
    public int? PointOfSaleId { get; set; }
    public int? ProductId { get; set; }
    public StockMovementType? Type { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SearchText { get; set; }
    public string? OperatorName { get; set; }
}