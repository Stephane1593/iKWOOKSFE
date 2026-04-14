// File: SFE.Application/Interfaces/IStockTransferRepository.cs
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IStockTransferRepository : IRepository<StockTransfer>
{
    Task<StockTransfer?> GetWithLinesAsync(int transferId);
    Task<List<StockTransfer>> GetByStatusAsync(TransferStatus status);
    Task<List<StockTransfer>> GetByPosAsync(int pointOfSaleId, bool asSender = true);
    Task<List<StockTransfer>> GetPendingForPosAsync(int pointOfSaleId);
    Task<string> GenerateNextNumberAsync(int year);
}