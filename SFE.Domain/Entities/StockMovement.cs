// File: SFE.Domain/Entities/StockMovement.cs
using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// Trace chaque modification de stock. Immutable après création.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }

    // === Contexte ===
    public int ProductId { get; set; }
    public int PointOfSaleId { get; set; }
    public StockMovementType Type { get; set; }

    // === Quantités ===
    /// <summary>
    /// Quantité du mouvement (positive = entrée, négative = sortie)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>Stock AVANT le mouvement</summary>
    public decimal QuantityBefore { get; set; }

    /// <summary>Stock APRÈS le mouvement</summary>
    public decimal QuantityAfter { get; set; }

    // === Référence ===
    /// <summary>
    /// Référence traçable : numéro de facture, ID de transfert, etc.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Pour les transferts : ID du POS source ou destination
    /// </summary>
    public int? CounterpartPointOfSaleId { get; set; }

    /// <summary>
    /// Pour les transferts : identifiant unique partagé entre les 2 mouvements
    /// </summary>
    public string? TransferReference { get; set; }

    // === Coût (optionnel, pour valorisation) ===
    public decimal? UnitCost { get; set; }

    // === Audit ===
    public string OperatorName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // === Navigation ===
    public Product Product { get; set; } = null!;
    public PointOfSale PointOfSale { get; set; } = null!;

    // === Display helpers ===
    public string TypeDisplay => Type switch
    {
        StockMovementType.Entry => "📥 Entrée",
        StockMovementType.Exit => "📤 Sortie",
        StockMovementType.Adjustment => "🔧 Ajustement",
        StockMovementType.TransferOut => "🔄 Transfert →",
        StockMovementType.TransferIn => "🔄 → Transfert",
        StockMovementType.Sale => "🛒 Vente",
        StockMovementType.CreditReturn => "↩️ Avoir",
        StockMovementType.PhysicalCount => "📋 Inventaire",
        StockMovementType.Initial => "🏁 Initial",
        _ => Type.ToString()
    };

    public string QuantityDisplay => Quantity >= 0
        ? $"+{Quantity:G}" : $"{Quantity:G}";
}