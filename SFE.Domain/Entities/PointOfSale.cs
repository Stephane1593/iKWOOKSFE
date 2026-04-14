using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class PointOfSale
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // --- Configuration dispositif fiscal ---
    public DeviceType DeviceType { get; set; } = DeviceType.EMcf;

    // Config e-MCF
    public string? EmcfApiUrl { get; set; }
    public string? EmcfToken { get; set; }
    public string? EmcfNIM { get; set; }

    // Config MCF
    public string? McfPortName { get; set; }
    public int McfBaudRate { get; set; } = 115200;

    // 🆕 --- Stock ---
    /// <summary>Ce POS gère-t-il du stock ? (false pour un POS de services purs)</summary>
    public bool ManagesStock { get; set; } = true;

    /// <summary>Autoriser la vente même si stock = 0 ?</summary>
    public bool AllowNegativeStock { get; set; } = false;

    // === Navigation ===
    public Company? Company { get; set; }

    // 🆕
    public List<PosStock> PosStocks { get; set; } = new();
    public List<StockMovement> StockMovements { get; set; } = new();
}