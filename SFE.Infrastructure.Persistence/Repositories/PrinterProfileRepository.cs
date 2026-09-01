using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Infrastructure.Persistence;
using SFE.Infrastructure.Persistence.Repositories;

namespace SFE.Infrastructure.Repositories;

public class PrinterProfileRepository
: Repository<PrinterProfile>, IPrinterProfileRepository
{
    private readonly AppDbContext _context;

    public PrinterProfileRepository(AppDbContext context)
: base(context)
{
    _context = context;
}

public async Task<List<PrinterProfile>> GetAllAsync()
{
    return await _context.Set<PrinterProfile>()
    .OrderBy(p => p.Name)
    .ToListAsync();
}

public async Task<PrinterProfile?> GetDefaultKitchenAsync()
{
    return await _context.Set<PrinterProfile>()
    .FirstOrDefaultAsync(p => p.IsDefaultKitchen);
}

public async Task<PrinterProfile?> GetDefaultReceiptAsync()
{
    return await _context.Set<PrinterProfile>()
    .FirstOrDefaultAsync(p => p.IsDefaultReceipt);
}

public async Task<bool> SetDefaultKitchenAsync(int printerProfileId)
{
    var printers = await _context.Set<PrinterProfile>()
    .Where(p => p.IsDefaultKitchen || p.Id == printerProfileId)
    .ToListAsync();

    var selectedPrinter = printers.FirstOrDefault(
    p => p.Id == printerProfileId);

    if (selectedPrinter is null)
        return false;

    foreach (var printer in printers)
        printer.IsDefaultKitchen = printer.Id == printerProfileId;

    await _context.SaveChangesAsync();

    return true;
}

public async Task<bool> SetDefaultReceiptAsync(int printerProfileId)
{
    var printers = await _context.Set<PrinterProfile>()
    .Where(p => p.IsDefaultReceipt || p.Id == printerProfileId)
    .ToListAsync();

    var selectedPrinter = printers.FirstOrDefault(
    p => p.Id == printerProfileId);

    if (selectedPrinter is null)
        return false;

    foreach (var printer in printers)
        printer.IsDefaultReceipt = printer.Id == printerProfileId;

    await _context.SaveChangesAsync();

    return true;
}
}