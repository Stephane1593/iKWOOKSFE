using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly AppDbContext _context;

    public AppSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings?> GetCurrentAsync()
    {
        return await _context.AppSettings
            .FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(AppSettings settings)
    {
        var existing = await _context.AppSettings
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            settings.UpdatedAt = DateTime.Now;
            await _context.AppSettings.AddAsync(settings);
        }
        else
        {
            existing.ExchangeRateMode = settings.ExchangeRateMode;
            existing.CurrentExchangeRate = settings.CurrentExchangeRate;
            existing.CurrentExchangeRateEUR = settings.CurrentExchangeRateEUR;   
            existing.CurrentExchangeRateCNY = settings.CurrentExchangeRateCNY;
            existing.ExchangeRateUpdatedAt = settings.ExchangeRateUpdatedAt;
            existing.DefaultCurrency = settings.DefaultCurrency;
            existing.DefaultPriceMode = settings.DefaultPriceMode;
            existing.DiscountBeforeTax = settings.DiscountBeforeTax;
            existing.CompanyName = settings.CompanyName;
            existing.CompanyNIF = settings.CompanyNIF;
            existing.CompanyRCCM = settings.CompanyRCCM;
            existing.CompanyIdNat = settings.CompanyIdNat;
            existing.CompanyAddress = settings.CompanyAddress;
            existing.CompanyPhone = settings.CompanyPhone;
            existing.CompanyEmail = settings.CompanyEmail;
            existing.UpdatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }
}