// File: SFE.Domain/Entities/StockTransfer.cs
using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// Transfert de stock entre deux points de vente.
/// Contient plusieurs lignes (produits).
/// </summary>
public class StockTransfer
{
    public int Id { get; set; }

    // === Identification ===
    public string TransferNumber { get; set; } = string.Empty; // ex: "TRF-2026/0001"
    public TransferStatus Status { get; set; } = TransferStatus.Draft;

    // === POS ===
    public int FromPointOfSaleId { get; set; }
    public int ToPointOfSaleId { get; set; }

    // === Notes ===
    public string Notes { get; set; } = string.Empty;

    // === Audit ===
    public string CreatedBy { get; set; } = string.Empty;
    public string? ReceivedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // === Navigation ===
    public PointOfSale FromPointOfSale { get; set; } = null!;
    public PointOfSale ToPointOfSale { get; set; } = null!;
    public List<StockTransferLine> Lines { get; set; } = new();

    // === Display ===
    public string StatusDisplay => Status switch
    {
        TransferStatus.Draft => "Brouillon",
        TransferStatus.Pending => "En attente",
        TransferStatus.InTransit => "En transit",
        TransferStatus.Received => "Reçu",
        TransferStatus.PartiallyReceived => "Reçu partiellement",
        TransferStatus.Cancelled => "Annulé",
        _ => Status.ToString()
    };
}