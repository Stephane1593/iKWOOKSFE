using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public class PrinterProfile : SyncableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CompanyId { get; set; }

    // Differentiates how the system talks to this printer
    public string Kind { get; set; } = "windows-printer"; // "sunmi", "escpos-tcp", "windows-printer"

    // IP Address (e.g. "192.168.1.100") or Windows Printer Name (e.g. "POS-80C")
    public string ConnectionString { get; set; } = string.Empty;

    // Used for network printers
    public int Port { get; set; } = 9100;

    public bool IsDefaultKitchen { get; set; } = false;
    public bool IsDefaultReceipt { get; set; } = false;
}