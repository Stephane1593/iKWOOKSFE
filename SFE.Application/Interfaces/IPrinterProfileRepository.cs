using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IPrinterProfileRepository : IRepository<PrinterProfile>
{
    Task<List<PrinterProfile>> GetAllAsync();

    Task<PrinterProfile?> GetDefaultKitchenAsync();

    Task<PrinterProfile?> GetDefaultReceiptAsync();

    Task<bool> SetDefaultKitchenAsync(int printerProfileId);

    Task<bool> SetDefaultReceiptAsync(int printerProfileId);
}