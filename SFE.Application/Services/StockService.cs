using System.Globalization;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class StockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ITimeProvider _time;

    public StockService(IUnitOfWork unitOfWork, IAuditService audit, ITimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
        _time = time;
    }

    // Single source of truth for "now" (DGI §1.1).
    private DateTime NowUtc => _time.UtcNow.UtcDateTime;

    // ══════════════════════════════════════════════
    //  CONSULTATION (read-only)
    // ══════════════════════════════════════════════

    public async Task<decimal> GetStockAsync(int productId, int posId)
    {
        var ps = await _unitOfWork.PosStocks.GetByProductAndPosAsync(productId, posId);
        return ps?.Quantity ?? 0;
    }

    public async Task<decimal> GetTotalStockAsync(int productId)
        => await _unitOfWork.PosStocks.GetTotalStockAsync(productId);

    public async Task<List<PosStock>> GetPosStocksAsync(int posId)
        => await _unitOfWork.PosStocks.GetByPosAsync(posId);

    public async Task<List<PosStock>> GetProductStocksAsync(int productId)
        => await _unitOfWork.PosStocks.GetByProductAsync(productId);

    public async Task<List<PosStock>> GetLowStockAlertsAsync(int posId)
        => await _unitOfWork.PosStocks.GetLowStockByPosAsync(posId);

    public async Task<List<PosStock>> GetAllLowStockAlertsAsync()
        => await _unitOfWork.PosStocks.GetAllLowStockAsync();

    public async Task<int> GetLowStockCountAsync(int posId)
        => await _unitOfWork.PosStocks.GetLowStockCountAsync(posId);

    public async Task<List<StockMovement>> GetMovementHistoryAsync(
        int productId, int posId, int maxResults = 50)
        => await _unitOfWork.StockMovements
            .GetByProductAndPosAsync(productId, posId, maxResults);

    // ══════════════════════════════════════════════
    //  MOUVEMENTS DE STOCK
    // ══════════════════════════════════════════════

    public async Task<StockOperationResult> AddStockEntryAsync(
        int productId, int posId, decimal quantity,
        string operatorName, string notes = "", string reference = "",
        decimal? unitCost = null)
    {
        if (quantity <= 0)
            return StockOperationResult.Fail("La quantité doit être positive.");

        return await ApplyMovementAsync(
            productId, posId, StockMovementType.Entry, quantity,
            operatorName, notes, reference, unitCost);
    }

    public async Task<StockOperationResult> AddStockExitAsync(
        int productId, int posId, decimal quantity,
        string operatorName, string notes = "", string reference = "")
    {
        if (quantity <= 0)
            return StockOperationResult.Fail("La quantité doit être positive.");

        return await ApplyMovementAsync(
            productId, posId, StockMovementType.Exit, -quantity,
            operatorName, notes, reference);
    }

    public async Task<StockOperationResult> AdjustStockAsync(
        int productId, int posId, decimal newQuantity,
        string operatorName, string notes = "")
    {
        var (posStock, _) = await GetOrCreatePosStockAsync(productId, posId);
        decimal delta = newQuantity - posStock.Quantity;

        return await ApplyMovementAsync(
            productId, posId, StockMovementType.Adjustment, delta,
            operatorName, notes,
            string.Format(CultureInfo.InvariantCulture, "Ajustement → {0:G}", newQuantity));
    }

    public async Task<StockOperationResult> SetPhysicalCountAsync(
        int productId, int posId, decimal countedQuantity,
        string operatorName, string notes = "")
    {
        var (posStock, _) = await GetOrCreatePosStockAsync(productId, posId);
        decimal delta = countedQuantity - posStock.Quantity;

        return await ApplyMovementAsync(
            productId, posId, StockMovementType.PhysicalCount, delta,
            operatorName, notes,
            string.Format(CultureInfo.InvariantCulture,
                "Inventaire: {0:G} → {1:G}", posStock.Quantity, countedQuantity));
    }

    public async Task<StockOperationResult> SetInitialStockAsync(
        int productId, int posId, decimal quantity,
        string operatorName, string notes = "")
    {
        return await ApplyMovementAsync(
            productId, posId, StockMovementType.Initial, quantity,
            operatorName, notes, "Stock initial");
    }

    // ══════════════════════════════════════════════
    //  VENTE / AVOIR
    // ══════════════════════════════════════════════

    /// <summary>
    /// Decrement stock for a sale.
    /// Wraps check + decrement in a single transaction to avoid race conditions
    /// on multi-caissier POS (DGI §3.4 — atomicity).
    /// </summary>
    public async Task<StockOperationResult> DecrementForSaleAsync(
        int productId, int posId, decimal quantity,
        string invoiceNumber, string operatorName)
    {
        if (quantity <= 0)
            return StockOperationResult.Ok(0);

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || !product.TrackStock)
            return StockOperationResult.Ok(0);

        var pos = await _unitOfWork.PointsOfSale.GetByIdAsync(posId);
        if (pos == null)
            return StockOperationResult.Fail($"POS #{posId} introuvable.");

        // ApplyMovementAsync now enforces the non-negative check atomically
        // (single read → single write, no TOCTOU window).
        return await ApplyMovementAsync(
            productId, posId, StockMovementType.Sale, -quantity,
            operatorName, "", invoiceNumber,
            enforceNonNegative: !pos.AllowNegativeStock,
            productNameForError: product.Name,
            posCodeForError: pos.Code);
    }

    public async Task<StockOperationResult> IncrementForCreditNoteAsync(
        int productId, int posId, decimal quantity,
        string invoiceNumber, string operatorName)
    {
        if (quantity <= 0)
            return StockOperationResult.Ok(0);

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null || !product.TrackStock)
            return StockOperationResult.Ok(0);

        return await ApplyMovementAsync(
            productId, posId, StockMovementType.CreditReturn, quantity,
            operatorName, "", invoiceNumber);
    }

    // ══════════════════════════════════════════════
    //  TRANSFERT INTER-POS
    // ══════════════════════════════════════════════

    public async Task<StockTransfer> CreateTransferAsync(
        int fromPosId, int toPosId, string createdBy,
        List<(int ProductId, decimal Quantity)> lines, string notes = "")
    {
        var number = await _unitOfWork.StockTransfers
            .GenerateNextNumberAsync(NowUtc.Year);

        var transfer = new StockTransfer
        {
            TransferNumber = number,
            FromPointOfSaleId = fromPosId,
            ToPointOfSaleId = toPosId,
            Status = TransferStatus.Draft,
            CreatedBy = createdBy,
            Notes = notes
        };

        foreach (var (productId, qty) in lines)
        {
            transfer.Lines.Add(new StockTransferLine
            {
                ProductId = productId,
                RequestedQuantity = qty
            });
        }

        await _unitOfWork.StockTransfers.AddAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        await _audit.LogAsync(
            AuditAction.TransferCreated,
            AuditModule.Stock,
            $"{transfer.TransferNumber} · POS #{fromPosId} → POS #{toPosId} · {lines.Count} ligne(s)",
            entityType: "StockTransfer",
            entityId: transfer.Id.ToString());

        _unitOfWork.EnqueueEvent(AppEvent.StockTransferCreated, transfer.Id.ToString());
        await _unitOfWork.FlushEventsAsync();

        return transfer;
    }

    public async Task<StockOperationResult> ShipTransferAsync(
        int transferId, string operatorName)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transfer = await _unitOfWork.StockTransfers.GetWithLinesAsync(transferId);
            if (transfer == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail("Transfert introuvable.");
            }

            if (transfer.Status != TransferStatus.Draft &&
                transfer.Status != TransferStatus.Pending)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail(
                    $"Transfert en statut '{transfer.StatusDisplay}', expédition impossible.");
            }

            // Load FROM POS once (avoid N+1 in the loop below).
            var fromPos = await _unitOfWork.PointsOfSale
                .GetByIdAsync(transfer.FromPointOfSaleId);
            if (fromPos == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail(
                    $"POS source #{transfer.FromPointOfSaleId} introuvable.");
            }

            var transferRef = $"TRF-{transfer.Id}-{NowUtc:yyyyMMddHHmmss}";

            foreach (var line in transfer.Lines)
            {
                var (posStock, _) = await GetOrCreatePosStockAsync(
                    line.ProductId, transfer.FromPointOfSaleId);

                if (!fromPos.AllowNegativeStock &&
                    posStock.Quantity < line.RequestedQuantity)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
                    return StockOperationResult.Fail(
                        $"Stock insuffisant pour « {product?.Name} ». " +
                        $"Disponible: {posStock.Quantity:G}, Demandé: {line.RequestedQuantity:G}");
                }

                await ApplyMovementInternalAsync(
                    line.ProductId, transfer.FromPointOfSaleId,
                    StockMovementType.TransferOut, -line.RequestedQuantity,
                    operatorName,
                    $"→ {transfer.ToPointOfSale?.Code ?? "?"}",
                    transfer.TransferNumber,
                    counterpartPosId: transfer.ToPointOfSaleId,
                    transferReference: transferRef);
            }

            transfer.Status = TransferStatus.InTransit;
            transfer.ShippedAt = NowUtc;
            await _unitOfWork.StockTransfers.UpdateAsync(transfer);

            await _unitOfWork.SaveChangesAsync();

            await UpdateGlobalStocksAsync(
                transfer.Lines.Select(l => l.ProductId).Distinct());
            await _unitOfWork.SaveChangesAsync();

            await _audit.LogAsync(
                AuditAction.TransferShipped,
                AuditModule.Stock,
                $"{transfer.TransferNumber} · POS #{transfer.FromPointOfSaleId} → POS #{transfer.ToPointOfSaleId} · {transfer.Lines.Count} ligne(s)",
                entityType: "StockTransfer",
                entityId: transfer.Id.ToString());

            _unitOfWork.EnqueueEvent(AppEvent.StockTransferShipped, transfer.Id.ToString());
            _unitOfWork.EnqueueEvent(AppEvent.StockUpdated);

            await _unitOfWork.CommitTransactionAsync();
            await _unitOfWork.FlushEventsAsync();   // ← FIX: post-commit flush

            return StockOperationResult.Ok(transfer.Lines.Count,
                $"Transfert {transfer.TransferNumber} expédié.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<StockOperationResult> ReceiveTransferAsync(
        int transferId, string operatorName,
        List<(int ProductId, decimal ReceivedQuantity)>? receivedQuantities = null)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transfer = await _unitOfWork.StockTransfers.GetWithLinesAsync(transferId);
            if (transfer == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail("Transfert introuvable.");
            }

            if (transfer.Status != TransferStatus.InTransit)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail(
                    $"Transfert en statut '{transfer.StatusDisplay}', réception impossible.");
            }

            var outMovements = await _unitOfWork.StockMovements
                .GetByReferenceAsync(transfer.TransferNumber);
            var transferRef = outMovements.FirstOrDefault()?.TransferReference
                              ?? $"TRF-{transfer.Id}-RCV";

            // FIX: use dictionary to avoid the `match != default` tuple trap.
            // Contract: if `receivedQuantities` is provided, ANY product NOT in the
            // dictionary is treated as "0 received" (explicit). If the list is null,
            // every line is received in full.
            var receivedMap = receivedQuantities?
                .GroupBy(r => r.ProductId)
                .ToDictionary(g => g.Key, g => g.Last().ReceivedQuantity);

            bool isPartial = false;

            foreach (var line in transfer.Lines)
            {
                decimal received;

                if (receivedMap != null)
                    received = receivedMap.TryGetValue(line.ProductId, out var q) ? q : 0m;
                else
                    received = line.RequestedQuantity;

                line.ReceivedQuantity = received;
                if (received != line.RequestedQuantity)
                    isPartial = true;

                if (received > 0)
                {
                    await ApplyMovementInternalAsync(
                        line.ProductId, transfer.ToPointOfSaleId,
                        StockMovementType.TransferIn, received,
                        operatorName,
                        $"← {transfer.FromPointOfSale?.Code ?? "?"}",
                        transfer.TransferNumber,
                        counterpartPosId: transfer.FromPointOfSaleId,
                        transferReference: transferRef);
                }
            }

            transfer.Status = isPartial
                ? TransferStatus.PartiallyReceived
                : TransferStatus.Received;
            transfer.ReceivedAt = NowUtc;
            transfer.ReceivedBy = operatorName;

            await _unitOfWork.StockTransfers.UpdateAsync(transfer);
            await _unitOfWork.SaveChangesAsync();

            await _audit.LogAsync(
                AuditAction.TransferReceived,
                AuditModule.Stock,
                $"{transfer.TransferNumber} · POS #{transfer.FromPointOfSaleId} → POS #{transfer.ToPointOfSaleId} · " +
                $"Réceptionné{(isPartial ? " (partiel)" : "")} · {transfer.Lines.Count} ligne(s)",
                entityType: "StockTransfer",
                entityId: transfer.Id.ToString());

            await UpdateGlobalStocksAsync(
                transfer.Lines.Select(l => l.ProductId).Distinct());
            await _unitOfWork.SaveChangesAsync();

            _unitOfWork.EnqueueEvent(AppEvent.StockTransferReceived, transfer.Id.ToString());
            _unitOfWork.EnqueueEvent(AppEvent.StockUpdated);

            await _unitOfWork.CommitTransactionAsync();
            await _unitOfWork.FlushEventsAsync();   // ← FIX: post-commit flush

            return StockOperationResult.Ok(transfer.Lines.Count,
                $"Transfert {transfer.TransferNumber} réceptionné" +
                $"{(isPartial ? " (partiel)" : "")}.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<StockOperationResult> CancelTransferAsync(
        int transferId, string operatorName, string reason = "")
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transfer = await _unitOfWork.StockTransfers.GetWithLinesAsync(transferId);
            if (transfer == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail("Transfert introuvable.");
            }

            if (transfer.Status == TransferStatus.Received ||
                transfer.Status == TransferStatus.Cancelled)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail(
                    $"Impossible d'annuler un transfert '{transfer.StatusDisplay}'.");
            }

            bool stockRestored = false;

            if (transfer.Status == TransferStatus.InTransit)
            {
                foreach (var line in transfer.Lines)
                {
                    await ApplyMovementInternalAsync(
                        line.ProductId, transfer.FromPointOfSaleId,
                        StockMovementType.Adjustment, line.RequestedQuantity,
                        operatorName,
                        $"Annulation transfert {transfer.TransferNumber}",
                        transfer.TransferNumber);
                }
                stockRestored = true;
            }

            transfer.Status = TransferStatus.Cancelled;
            transfer.CancelledAt = NowUtc;
            transfer.Notes += $"\n[Annulé] {reason}";

            await _unitOfWork.StockTransfers.UpdateAsync(transfer);
            await _unitOfWork.SaveChangesAsync();

            if (stockRestored)
            {
                await UpdateGlobalStocksAsync(
                    transfer.Lines.Select(l => l.ProductId).Distinct());
                await _unitOfWork.SaveChangesAsync();
            }

            await _audit.LogAsync(
                AuditAction.TransferCancelled,
                AuditModule.Stock,
                $"{transfer.TransferNumber} · Annulé" +
                    (stockRestored ? " (stock restauré)" : " (brouillon)") +
                    (string.IsNullOrEmpty(reason) ? "" : $" · Raison: {reason}"),
                entityType: "StockTransfer",
                entityId: transfer.Id.ToString());

            _unitOfWork.EnqueueEvent(AppEvent.StockTransferCancelled, transfer.Id.ToString());
            if (stockRestored)
                _unitOfWork.EnqueueEvent(AppEvent.StockUpdated);

            await _unitOfWork.CommitTransactionAsync();
            await _unitOfWork.FlushEventsAsync();   // ← FIX: post-commit flush

            return StockOperationResult.Ok(0,
                $"Transfert {transfer.TransferNumber} annulé.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════
    //  INITIALISATION
    // ══════════════════════════════════════════════

    public async Task<int> InitializePosStockFromProductsAsync(
        int posId, string operatorName)
    {
        var products = await _unitOfWork.Products.GetActiveProductsAsync();
        int count = 0;
        var now = NowUtc;

        foreach (var product in products.Where(p => p.TrackStock))
        {
            var existing = await _unitOfWork.PosStocks
                .GetByProductAndPosAsync(product.Id, posId);

            var qty = product.StockQuantity;

            if (existing != null)
            {
                if (existing.Quantity == 0 && qty > 0)
                {
                    existing.Quantity = qty;
                    existing.LastMovementAt = now;
                    existing.UpdatedAt = now;
                    await _unitOfWork.PosStocks.UpdateAsync(existing);

                    await _unitOfWork.StockMovements.AddAsync(new StockMovement
                    {
                        ProductId = product.Id,
                        PointOfSaleId = posId,
                        Type = StockMovementType.Initial,
                        Quantity = qty,
                        QuantityBefore = 0,
                        QuantityAfter = qty,
                        Reference = "INIT-FIX",
                        OperatorName = operatorName,
                        Notes = "Correction initialisation stock"
                    });
                    count++;
                }
                continue;
            }

            var posStock = new PosStock
            {
                ProductId = product.Id,
                PointOfSaleId = posId,
                Quantity = qty,
                LastMovementAt = now
            };
            await _unitOfWork.PosStocks.AddAsync(posStock);

            await _unitOfWork.StockMovements.AddAsync(new StockMovement
            {
                ProductId = product.Id,
                PointOfSaleId = posId,
                Type = StockMovementType.Initial,
                Quantity = qty,
                QuantityBefore = 0,
                QuantityAfter = qty,
                Reference = "INIT",
                OperatorName = operatorName,
                Notes = "Migration depuis stock global"
            });
            count++;
        }

        await _unitOfWork.SaveChangesAsync();

        if (count > 0)
        {
            _unitOfWork.EnqueueEvent(AppEvent.StockUpdated);
            await _unitOfWork.FlushEventsAsync();
        }

        return count;
    }

    public async Task<int> InitializeAllProductsInPosAsync(
        int posId, string operatorName)
    {
        var products = await _unitOfWork.Products.GetActiveProductsAsync();
        int count = 0;

        foreach (var product in products.Where(p => p.TrackStock))
        {
            var existing = await _unitOfWork.PosStocks
                .GetByProductAndPosAsync(product.Id, posId);

            if (existing == null)
            {
                await _unitOfWork.PosStocks.AddAsync(new PosStock
                {
                    ProductId = product.Id,
                    PointOfSaleId = posId,
                    Quantity = 0
                });
                count++;
            }
        }

        await _unitOfWork.SaveChangesAsync();

        if (count > 0)
        {
            _unitOfWork.EnqueueEvent(AppEvent.StockUpdated);
            await _unitOfWork.FlushEventsAsync();
        }

        return count;
    }

    // ══════════════════════════════════════════════
    //  PRIVATE — Core stock logic
    // ══════════════════════════════════════════════

    private async Task<(PosStock Stock, bool IsNew)> GetOrCreatePosStockAsync(int productId, int posId)
    {
        var posStock = await _unitOfWork.PosStocks
            .GetByProductAndPosAsync(productId, posId);

        if (posStock != null)
            return (posStock, false);

        posStock = new PosStock
        {
            ProductId = productId,
            PointOfSaleId = posId,
            Quantity = 0
        };
        await _unitOfWork.PosStocks.AddAsync(posStock);
        return (posStock, true);
    }

    /// <summary>
    /// Applies a movement with its own save + global-stock refresh + audit + event flush.
    /// If <paramref name="enforceNonNegative"/> is true, rolls back and returns a failure
    /// when the resulting quantity would be negative. The check and the write happen
    /// under a single transaction to avoid TOCTOU races.
    /// </summary>
    private async Task<StockOperationResult> ApplyMovementAsync(
        int productId, int posId, StockMovementType type, decimal quantity,
        string operatorName, string notes, string reference,
        decimal? unitCost = null,
        bool enforceNonNegative = false,
        string? productNameForError = null,
        string? posCodeForError = null)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var (posStock, isNew) = await GetOrCreatePosStockAsync(productId, posId);

            decimal before = posStock.Quantity;
            decimal after = before + quantity;

            if (enforceNonNegative && after < 0)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return StockOperationResult.Fail(
                    $"Stock insuffisant pour « {productNameForError ?? $"#{productId}"} » " +
                    $"au POS {posCodeForError ?? $"#{posId}"}. " +
                    $"Disponible: {before:G}, Demandé: {Math.Abs(quantity):G}");
            }

            var now = NowUtc;
            posStock.Quantity = after;
            posStock.LastMovementAt = now;
            posStock.UpdatedAt = now;

            if (!isNew)
                await _unitOfWork.PosStocks.UpdateAsync(posStock);

            var movement = new StockMovement
            {
                ProductId = productId,
                PointOfSaleId = posId,
                Type = type,
                Quantity = quantity,
                QuantityBefore = before,
                QuantityAfter = after,
                Reference = reference,
                OperatorName = operatorName,
                Notes = notes,
                UnitCost = unitCost
            };
            await _unitOfWork.StockMovements.AddAsync(movement);

            await _unitOfWork.SaveChangesAsync();

            await UpdateProductGlobalStockAsync(productId);
            await _unitOfWork.SaveChangesAsync();

            var auditAction = type switch
            {
                StockMovementType.Entry => AuditAction.StockEntry,
                StockMovementType.Exit => AuditAction.StockExit,
                StockMovementType.Adjustment => AuditAction.StockAdjustment,
                StockMovementType.PhysicalCount => AuditAction.StockPhysicalCount,
                StockMovementType.Initial => AuditAction.StockInitial,
                StockMovementType.Sale => AuditAction.StockSaleDecrement,
                StockMovementType.CreditReturn => AuditAction.StockCreditReturn,
                _ => AuditAction.StockAdjustment
            };

            await _audit.LogAsync(
                auditAction,
                AuditModule.Stock,
                $"Produit #{productId} · POS #{posId} · {type}: {before:G} → {after:G} ({quantity:+0.##;-0.##;0})" +
                    (string.IsNullOrEmpty(reference) ? "" : $" · Réf: {reference}"),
                entityType: "StockMovement",
                entityId: movement.Id.ToString());

            _unitOfWork.EnqueueEvent(AppEvent.StockUpdated);

            await _unitOfWork.CommitTransactionAsync();
            await _unitOfWork.FlushEventsAsync();   // post-commit flush

            return StockOperationResult.Ok(after);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Used INSIDE transactions (Ship/Receive/Cancel).
    /// Does NOT save, does NOT update global stock, does NOT publish events.
    /// The caller is responsible for those steps.
    /// </summary>
    private async Task ApplyMovementInternalAsync(
        int productId, int posId, StockMovementType type, decimal quantity,
        string operatorName, string notes, string reference,
        int? counterpartPosId = null, string? transferReference = null)
    {
        var (posStock, isNew) = await GetOrCreatePosStockAsync(productId, posId);

        decimal before = posStock.Quantity;
        decimal after = before + quantity;

        var now = NowUtc;
        posStock.Quantity = after;
        posStock.LastMovementAt = now;
        posStock.UpdatedAt = now;

        if (!isNew)
            await _unitOfWork.PosStocks.UpdateAsync(posStock);

        var movement = new StockMovement
        {
            ProductId = productId,
            PointOfSaleId = posId,
            Type = type,
            Quantity = quantity,
            QuantityBefore = before,
            QuantityAfter = after,
            Reference = reference,
            OperatorName = operatorName,
            Notes = notes,
            CounterpartPointOfSaleId = counterpartPosId,
            TransferReference = transferReference
        };
        await _unitOfWork.StockMovements.AddAsync(movement);
    }

    private async Task UpdateProductGlobalStockAsync(int productId)
    {
        var totalStock = await _unitOfWork.PosStocks.GetTotalStockAsync(productId);
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product != null)
        {
            product.StockQuantity = totalStock;
            product.UpdatedAtUtc = NowUtc;
            await _unitOfWork.Products.UpdateAsync(product);
        }
    }

    /// <summary>
    /// Batch-update global stocks for multiple products.
    /// Call AFTER SaveChangesAsync so the DB queries see the latest PosStock values.
    /// </summary>
    private async Task UpdateGlobalStocksAsync(IEnumerable<int> productIds)
    {
        foreach (var pid in productIds)
            await UpdateProductGlobalStockAsync(pid);
    }
}

public class StockOperationResult
{
    public bool Success { get; set; }
    public decimal NewQuantity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public static StockOperationResult Ok(decimal newQty, string message = "")
        => new() { Success = true, NewQuantity = newQty, Message = message };

    public static StockOperationResult Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}