using System.Collections.Generic;
using System.Threading.Tasks;
using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface ITableService
{
    Task<List<Table>> GetAllAsync();
    Task<Table?> GetByIdAsync(int tableId);
    Task<Table> ChangeStatusAsync(int tableId, TableStatus newStatus);
    Task<Order?> GetActiveOrderAsync(int tableId);
    Task TransferTableAsync(int sourceTableId, int targetTableId);
}