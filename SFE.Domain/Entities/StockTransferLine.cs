// File: SFE.Domain/Entities/StockTransferLine.cs
namespace SFE.Domain.Entities;

public class StockTransferLine
{
    public int Id { get; set; }
    public int StockTransferId { get; set; }
    public int ProductId { get; set; }

    /// <summary>Quantité demandée/envoyée</summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>Quantité réellement reçue (null = pas encore réceptionné)</summary>
    public decimal? ReceivedQuantity { get; set; }

    /// <summary>Notes spécifiques à cette ligne (ex: "2 unités abîmées")</summary>
    public string Notes { get; set; } = string.Empty;

    // === Navigation ===
    public StockTransfer StockTransfer { get; set; } = null!;
    public Product Product { get; set; } = null!;
}