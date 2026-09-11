using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Application.Services;

public class TableService(
    IRepository<Table> tableRepository,
    IUnitOfWork unitOfWork,
    ILogger<TableService> logger) : ITableService
{
    public Task<List<Table>> GetAllAsync()
    {
        return tableRepository.GetAllAsync();
    }

    public Task<Table?> GetByIdAsync(int tableId)
    {
        return tableRepository.GetByIdAsync(tableId);
    }

    public async Task<Table> ChangeStatusAsync(int tableId, TableStatus newStatus)
    {
        var table = await tableRepository.GetByIdAsync(tableId)
            ?? throw new KeyNotFoundException($"La table avec l'ID {tableId} est introuvable.");

        if (table.Status == newStatus)
        {
            return table;
        }

        table.Status = newStatus;

        await tableRepository.UpdateAsync(table);
        await unitOfWork.SaveChangesAsync();

        return table;
    }

    public async Task<Order?> GetActiveOrderAsync(int tableId)
    {
        var orderRepo = unitOfWork.GetRepository<Order>();

        // Trouver toute commande liée à cette table qui n'est pas payée, fermée ou annulée
        var activeOrders = await orderRepo.FindAsync(o =>
            o.TableId == tableId &&
            (o.Status == OrderStatus.Open ||
             o.Status == OrderStatus.InKitchen ||
             o.Status == OrderStatus.Served));

        return activeOrders.FirstOrDefault();
    }

    public async Task TransferTableAsync(int sourceTableId, int targetTableId)
    {
        var sourceTable = await tableRepository.GetByIdAsync(sourceTableId)
            ?? throw new KeyNotFoundException($"La table d'origine {sourceTableId} est introuvable.");

        var targetTable = await tableRepository.GetByIdAsync(targetTableId)
            ?? throw new KeyNotFoundException($"La table de destination {targetTableId} est introuvable.");

        if (targetTable.Status == TableStatus.Occupied)
            throw new InvalidOperationException("La table de destination est déjà occupée.");

        var activeOrder = await GetActiveOrderAsync(sourceTableId)
            ?? throw new InvalidOperationException($"Aucune commande active trouvée sur la table {sourceTableId}.");

        // Déplacer la commande
        activeOrder.TableId = targetTableId;
        activeOrder.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var orderRepo = unitOfWork.GetRepository<Order>();
        await orderRepo.UpdateAsync(activeOrder);

        // Mettre à jour le statut des tables
        sourceTable.Status = TableStatus.Cleaning; // La table d'origine doit être nettoyée après le transfert
        targetTable.Status = TableStatus.Occupied;

        await tableRepository.UpdateAsync(sourceTable);
        await tableRepository.UpdateAsync(targetTable);

        await unitOfWork.SaveChangesAsync();

        logger.LogInformation("Transfert de la commande {OrderId} de la table {SourceId} vers la table {TargetId}",
            activeOrder.Id, sourceTableId, targetTableId);
    }
}