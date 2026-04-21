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

    // ═══════════════════════════════════════════════════════
    //  🆕 PRINTER CONFIGURATION (per POS)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Windows printer name for the thermal receipt printer.
    /// Empty = auto-detect on startup.
    /// Example: "EPSON TM-T88V", "POS-80C", "OPTIMA Printer"
    /// </summary>
    public string ThermalPrinterName { get; set; } = "";

    /// <summary>
    /// Paper width in mm. Common values: 58, 80.
    /// Determines chars-per-line: 80mm=48 chars, 58mm=32 chars.
    /// </summary>
    public int PaperWidthMm { get; set; } = 80;

    /// <summary>
    /// Automatically print receipt after normalization.
    /// </summary>
    public bool AutoPrintReceipt { get; set; } = true;

    /// <summary>
    /// Number of copies to auto-print (1 = customer copy only, 2 = customer + merchant).
    /// </summary>
    public int PrintCopies { get; set; } = 1;

    /// <summary>
    /// Enable the customer-facing display on a secondary monitor.
    /// </summary>
    public bool EnableCustomerDisplay { get; set; } = true;

    /// <summary>
    /// Enable cash drawer opening via ESC/POS pulse after cash payment.
    /// </summary>
    public bool EnableCashDrawer { get; set; } = false;

    /// <summary>
    /// ESC/POS cash drawer pin (0 = pin 2, 1 = pin 5).
    /// Most drawers use pin 2.
    /// </summary>
    public int CashDrawerPin { get; set; } = 0;

    /// <summary>
    /// Code page for ESC/POS text encoding.
    /// 858 = Multilingual Latin I + Euro (default for French).
    /// 437 = USA. 850 = Multilingual Latin I.
    /// </summary>
    public int PrinterCodePage { get; set; } = 858;

    /// <summary>
    /// Print company logo at top of receipt (bitmap stored in Company.Logo).
    /// Requires printer that supports ESC/POS GS v 0 raster bitmap.
    /// </summary>
    public bool PrintLogo { get; set; } = false;

    /// <summary>
    /// Custom footer text printed at bottom of each receipt.
    /// Example: "Merci de votre fidélité !"
    /// </summary>
    public string ReceiptFooterText { get; set; } = "Merci pour votre achat !";


    /// <summary>Autoriser la vente même si stock = 0 ?</summary>
    public bool AllowNegativeStock { get; set; } = false;

    // === Navigation ===
    public Company? Company { get; set; }

    // 🆕
    public List<PosStock> PosStocks { get; set; } = new();
    public List<StockMovement> StockMovements { get; set; } = new();
}