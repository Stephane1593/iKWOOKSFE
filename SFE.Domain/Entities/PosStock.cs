// File: SFE.Domain/Entities/PosStock.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace SFE.Domain.Entities;

/// <summary>
/// Stock d'un produit dans un point de vente spécifique.
/// Clé métier unique : (ProductId, PointOfSaleId)
/// </summary>
public class PosStock
{
    public int Id { get; set; }

    // === Clés ===
    public int ProductId { get; set; }
    public int PointOfSaleId { get; set; }

    // === Quantités ===
    public decimal Quantity { get; set; }

    /// <summary>
    /// Seuil minimum spécifique à ce POS.
    /// Si null → utilise Product.MinStockLevel
    /// </summary>
    public decimal? MinStockLevel { get; set; }

    /// <summary>
    /// Seuil maximum (capacité de stockage de ce POS)
    /// </summary>
    public decimal? MaxStockLevel { get; set; }

    // === Audit ===
    public DateTimeOffset LastMovementAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    // === Navigation ===
    public Product Product { get; set; } = null!;
    public PointOfSale PointOfSale { get; set; } = null!;

    // === Propriétés calculées ===
    [NotMapped]
    public decimal EffectiveMinStock => MinStockLevel ?? Product?.MinStockLevel ?? 0;

    [NotMapped]
    public bool IsLowStock => Quantity <= EffectiveMinStock;

    [NotMapped]
    public bool IsOutOfStock => Quantity <= 0;

    [NotMapped]
    public string StockStatusDisplay => IsOutOfStock ? "Rupture"
        : IsLowStock ? "Stock bas"
        : "OK";

    [NotMapped]
    public string StockStatusColor => IsOutOfStock ? "#EF4444"
        : IsLowStock ? "#F59E0B"
        : "#10B981";
}