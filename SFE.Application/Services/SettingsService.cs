using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Service pour charger et sauvegarder les paramètres.
/// </summary>
public class SettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITimeProvider _time;

    public SettingsService(IUnitOfWork unitOfWork, ITimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <summary>
    /// Charge les paramètres actuels (entreprise + POS actif + AppSettings).
    /// </summary>
    public async Task<SettingsData> LoadSettingsAsync()
    {
        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company == null)
            throw new InvalidOperationException("Aucune entreprise configurée. Lancez le seeder.");

        // ═══ FIX: Load POS with fallback ═══
        List<PointOfSale> posList;
        try
        {
            var companyWithPos = await _unitOfWork.Companies
                .GetWithPointsOfSaleAsync(company.Id);
            posList = companyWithPos?.PointsOfSale?.ToList() ?? new();
        }
        catch
        {
            posList = new();
        }

        // ✅ FALLBACK: if Include didn't work, query POS directly
        if (posList.Count == 0)
        {
            posList = await _unitOfWork.PointsOfSale
                .GetByCompanyIdAsync(company.Id);
        }

        var activePos = posList.FirstOrDefault(p => p.IsActive);

        // 🆕 Charger AppSettings pour les paramètres de calcul
        var appSettings = await _unitOfWork.AppSettings.GetCurrentAsync();

        return new SettingsData
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            CompanyNIF = company.NIF,
            CompanyRCCM = company.RCCM,
            CompanyAddress = company.Address,
            CompanyCity = company.City,
            CompanyPhone = company.Phone,
            CompanyEmail = company.Email,
            DefaultPriceMode = company.DefaultPriceMode,
            LoyaltyEnabled = company.LoyaltyEnabled,
            LoyaltyEarnRate = company.LoyaltyEarnRate,
            LoyaltyRedeemRate = company.LoyaltyRedeemRate,
            DeploymentMode = company.DeploymentMode,
            CompanyISF = company.ISF,
            CompanyLogo = company.Logo,

            // 🆕 Paramètres de calcul (depuis AppSettings)
            DiscountBeforeTax = appSettings?.DiscountBeforeTax ?? true,
            DefaultCurrency = appSettings?.DefaultCurrency ?? Currency.CDF,
            CurrentExchangeRate = appSettings?.CurrentExchangeRate ?? 2800m,
            CurrentExchangeRateEUR = appSettings?.CurrentExchangeRateEUR ?? 3100m,
            CurrentExchangeRateCNY = appSettings?.CurrentExchangeRateCNY ?? 385m,
            ExchangeRateMode = appSettings?.ExchangeRateMode ?? ExchangeRateMode.Manual,

            // POS actif
            ActivePosId = activePos?.Id ?? 0,
            ActivePosCode = activePos?.Code ?? "",
            ActivePosName = activePos?.Name ?? "",
            DeviceType = activePos?.DeviceType ?? DeviceType.EMcf,
            EmcfApiUrl = activePos?.EmcfApiUrl ?? "",
            EmcfToken = activePos?.EmcfToken ?? "",
            EmcfNIM = activePos?.EmcfNIM ?? "",
            McfPortName = activePos?.McfPortName ?? "",
            McfBaudRate = activePos?.McfBaudRate ?? 115200,
            DisableFallback = activePos?.DisableFallback ?? false,   // 🆕

            TotalPosCount = posList.Count,
            ActivePosCount = posList.Count(p => p.IsActive)
        };
    }

    /// <summary>
    /// Sauvegarde les paramètres modifiés.
    /// </summary>
    public async Task SaveSettingsAsync(SettingsData data)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // --- Mettre à jour l'entreprise ---
            var company = await _unitOfWork.Companies.GetByIdAsync(data.CompanyId);
            if (company == null)
                throw new InvalidOperationException("Entreprise introuvable.");

            company.Name = data.CompanyName;
            company.NIF = data.CompanyNIF;
            company.RCCM = data.CompanyRCCM;
            company.Address = data.CompanyAddress;
            company.City = data.CompanyCity;
            company.Phone = data.CompanyPhone;
            company.Email = data.CompanyEmail;
            company.DefaultPriceMode = data.DefaultPriceMode;
            company.LoyaltyEnabled = data.LoyaltyEnabled;
            company.LoyaltyEarnRate = data.LoyaltyEarnRate;
            company.LoyaltyRedeemRate = data.LoyaltyRedeemRate;
            company.DeploymentMode = data.DeploymentMode;
            company.ISF = data.CompanyISF;
            company.Logo = data.CompanyLogo;
            // (duplicate assignment removed — was assigning DefaultPriceMode twice)

            await _unitOfWork.Companies.UpdateAsync(company);

            // 🆕 --- Mettre à jour AppSettings ---
            var appSettings = await _unitOfWork.AppSettings.GetCurrentAsync();
            if (appSettings != null)
            {
                appSettings.DiscountBeforeTax = data.DiscountBeforeTax;
                appSettings.DefaultCurrency = data.DefaultCurrency;
                appSettings.CurrentExchangeRate = data.CurrentExchangeRate;
                appSettings.CurrentExchangeRateEUR = data.CurrentExchangeRateEUR;
                appSettings.CurrentExchangeRateCNY = data.CurrentExchangeRateCNY;
                appSettings.ExchangeRateMode = data.ExchangeRateMode;
                appSettings.UpdatedAt = _time.UtcNow.UtcDateTime;   // ← ITimeProvider
                appSettings.CompanyIdNat = data.CompanyISF;
                appSettings.DefaultPriceMode = data.DefaultPriceMode;

                await _unitOfWork.AppSettings.UpdateAsync(appSettings);
            }

            // --- Mettre à jour le POS actif ---
            if (data.ActivePosId > 0)
            {
                var pos = await _unitOfWork.PointsOfSale.GetByIdAsync(data.ActivePosId);
                if (pos != null)
                {
                    pos.DeviceType = data.DeviceType;
                    pos.EmcfApiUrl = data.EmcfApiUrl;
                    pos.EmcfToken = data.EmcfToken;
                    pos.EmcfNIM = data.EmcfNIM;
                    pos.McfPortName = data.McfPortName;
                    pos.McfBaudRate = data.McfBaudRate;
                    pos.DisableFallback = data.DisableFallback;   // 🆕

                    await _unitOfWork.PointsOfSale.UpdateAsync(pos);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}

/// <summary>
/// DTO plat pour transporter les paramètres entre service et ViewModel.
/// </summary>
public class SettingsData
{
    // Entreprise
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyNIF { get; set; } = string.Empty;
    public string CompanyRCCM { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyCity { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public PriceMode DefaultPriceMode { get; set; }
    public bool LoyaltyEnabled { get; set; }
    public decimal LoyaltyEarnRate { get; set; }
    public decimal LoyaltyRedeemRate { get; set; }
    public decimal LoyaltyMinRedeemPoints { get; set; }
    public DeploymentMode DeploymentMode { get; set; }
    public string CompanyISF { get; set; } = string.Empty;
    public byte[]? CompanyLogo { get; set; }

    // 🆕 Paramètres de calcul (depuis AppSettings)
    public bool DiscountBeforeTax { get; set; } = true;
    public Currency DefaultCurrency { get; set; } = Currency.CDF;
    public decimal CurrentExchangeRate { get; set; } = 2800m;        // USD
    public decimal CurrentExchangeRateEUR { get; set; } = 3100m;
    public decimal CurrentExchangeRateCNY { get; set; } = 385m;
    public ExchangeRateMode ExchangeRateMode { get; set; } = ExchangeRateMode.Manual;
    public DateTimeOffset? DgiExchangeRateDate { get; set; }

    // POS actif
    public int ActivePosId { get; set; }
    public string ActivePosCode { get; set; } = string.Empty;
    public string ActivePosName { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string EmcfApiUrl { get; set; } = string.Empty;
    public string EmcfToken { get; set; } = string.Empty;
    public string EmcfNIM { get; set; } = string.Empty;
    public string McfPortName { get; set; } = string.Empty;
    public int McfBaudRate { get; set; }
    public bool McfPortValidated { get; set; } = false;
    public bool DisableFallback { get; set; } = false;   // 🆕

    // Stats
    public int TotalPosCount { get; set; }
    public int ActivePosCount { get; set; }
}