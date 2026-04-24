using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// CRUD complet pour les Points de Vente (jusqu'à 20+ par entreprise).
/// </summary>
public class PointOfSaleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly StockService _stockService;
    private readonly IAuditService _audit;

    public PointOfSaleService(IUnitOfWork unitOfWork, StockService stockService, IAuditService audit)
    {
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        _audit = audit;
    }

    public async Task<List<PointOfSale>> GetAllAsync(int companyId)
    {
        return await _unitOfWork.PointsOfSale.GetByCompanyIdAsync(companyId);
    }

    public async Task<List<PointOfSale>> GetActiveAsync()
    {
        return await _unitOfWork.PointsOfSale.GetActiveAsync();
    }

    public async Task<PointOfSale?> GetByIdAsync(int posId)
    {
        return await _unitOfWork.PointsOfSale.GetByIdAsync(posId);
    }

    public async Task<PosSaveResult> CreateAsync(PointOfSale pos)
    {
        var validation = Validate(pos);
        if (!validation.IsValid)
            return new PosSaveResult { Success = false, ErrorMessage = validation.ErrorMessage };

        // Vérifier unicité du code
        var existing = await _unitOfWork.PointsOfSale.GetActiveByCodeAsync(pos.Code);
        if (existing != null)
            return new PosSaveResult
            {
                Success = false,
                ErrorMessage = $"Le code « {pos.Code} » est déjà utilisé."
            };

        await _unitOfWork.PointsOfSale.AddAsync(pos);
        await _unitOfWork.SaveChangesAsync();

        // Initialiser les entrées PosStock pour tous les produits existants
        if (pos.ManagesStock)
        {
            await _stockService.InitializeAllProductsInPosAsync(pos.Id, "Système");
        }

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.PosCreated, AuditModule.PointOfSale,
            pos.Id.ToString(),
            $"{pos.Code} · « {pos.Name} » · Entreprise #{pos.CompanyId}" +
            (pos.ManagesStock ? " · Gestion de stock activée" : ""));

        return new PosSaveResult { Success = true, PointOfSaleId = pos.Id };
    }

    public async Task<PosSaveResult> UpdateAsync(PointOfSale pos)
    {
        var validation = Validate(pos);
        if (!validation.IsValid)
            return new PosSaveResult { Success = false, ErrorMessage = validation.ErrorMessage };

        // Vérifier unicité du code
        var existing = await _unitOfWork.PointsOfSale.GetActiveByCodeAsync(pos.Code);
        if (existing != null && existing.Id != pos.Id)
            return new PosSaveResult
            {
                Success = false,
                ErrorMessage = $"Le code « {pos.Code} » est déjà utilisé."
            };

        await _unitOfWork.PointsOfSale.UpdateAsync(pos);
        await _unitOfWork.SaveChangesAsync();

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.PosUpdated, AuditModule.PointOfSale,
            pos.Id.ToString(),
            $"{pos.Code} · « {pos.Name} »");

        return new PosSaveResult { Success = true, PointOfSaleId = pos.Id };
    }

    public async Task<PosSaveResult> DeactivateAsync(int posId)
    {
        var pos = await _unitOfWork.PointsOfSale.GetByIdAsync(posId);
        if (pos == null)
            return new PosSaveResult { Success = false, ErrorMessage = "POS introuvable." };

        // Vérifier qu'il n'y a pas de stock résiduel
        var stocks = await _unitOfWork.PosStocks.GetByPosAsync(posId);
        var totalStock = stocks.Sum(s => s.Quantity);

        if (totalStock > 0)
        {
            return new PosSaveResult
            {
                Success = false,
                ErrorMessage = $"Impossible de désactiver : il reste {totalStock:G} " +
                    $"articles en stock. Transférez le stock vers un autre POS d'abord."
            };
        }

        pos.IsActive = false;
        await _unitOfWork.PointsOfSale.UpdateAsync(pos);
        await _unitOfWork.SaveChangesAsync();

        // ── AUDIT ──
        await _audit.LogAsync(AuditAction.PosDeactivated, AuditModule.PointOfSale,
            pos.Id.ToString(),
            $"{pos.Code} · « {pos.Name} » · Désactivé");

        return new PosSaveResult { Success = true, PointOfSaleId = posId };
    }

    /// <summary>Génère le prochain code POS (POS-001, POS-002, ...)</summary>
    public async Task<string> GenerateNextCodeAsync(int companyId)
    {
        var allPos = await _unitOfWork.PointsOfSale.GetByCompanyIdAsync(companyId);
        int maxNumber = 0;

        foreach (var pos in allPos)
        {
            if (pos.Code.StartsWith("POS-") &&
                int.TryParse(pos.Code.Replace("POS-", ""), out var num))
            {
                if (num > maxNumber) maxNumber = num;
            }
        }

        return $"POS-{(maxNumber + 1):D3}";
    }

    private static ValidationResult Validate(PointOfSale pos)
    {
        if (string.IsNullOrWhiteSpace(pos.Code))
            return new ValidationResult("Le code du POS est obligatoire.");
        if (string.IsNullOrWhiteSpace(pos.Name))
            return new ValidationResult("Le nom du POS est obligatoire.");
        if (pos.CompanyId <= 0)
            return new ValidationResult("L'entreprise n'est pas définie.");
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Returns the POS assigned to the user if it exists and is active,
    /// otherwise returns the first active POS, or null.
    /// </summary>
    public async Task<PointOfSale?> GetDefaultForUserAsync(int? userPointOfSaleId)
    {
        var activeList = await _unitOfWork.PointsOfSale.GetActiveAsync();

        if (activeList.Count == 0)
            return null;

        // 1️⃣ Try the POS assigned to the user
        if (userPointOfSaleId.HasValue)
        {
            var userPos = activeList.FirstOrDefault(p => p.Id == userPointOfSaleId.Value);
            if (userPos != null)
                return userPos;
        }

        // 2️⃣ Fallback: first active POS
        return activeList.First();
    }
}

public class PosSaveResult
{
    public bool Success { get; set; }
    public int PointOfSaleId { get; set; }
    public string ErrorMessage { get; set; } = "";
}