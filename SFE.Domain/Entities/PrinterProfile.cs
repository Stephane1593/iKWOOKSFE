using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public class PrinterProfile : SyncableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "sunmi"; // e.g. sunmi, escpos-tcp, windows-printer
    public string ConnectionString { get; set; } = string.Empty; // e.g. "tcp://10.0.0.5:9100" or terminal id
    public bool IsDefaultKitchen { get; set; } = false;
    public bool IsDefaultReceipt { get; set; } = false;
}