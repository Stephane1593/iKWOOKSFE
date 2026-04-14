// File: SFE.Application/Services/DashboardService.cs  (MODIFIÉ)
using SFE.Application.Interfaces;

namespace SFE.Application.Services;

public class DashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardData> LoadDashboardAsync(int? activePosId = null)
    {
        var totalProducts = await _unitOfWork.Products.CountAsync();
        var totalClients = await _unitOfWork.Clients.CountAsync();
        var loyaltyMembers = (await _unitOfWork.Clients.GetLoyaltyMembersAsync()).Count;

        // 🆕 Stats factures
        var todayCount = await _unitOfWork.Invoices.GetTodayCountAsync();
        var todayTotal = await _unitOfWork.Invoices.GetTodayTotalAsync();

        // 🆕 Alertes stock
        int lowStockAlerts = 0;
        if (activePosId.HasValue && activePosId.Value > 0)
        {
            lowStockAlerts = await _unitOfWork.PosStocks
                .GetLowStockCountAsync(activePosId.Value);
        }
        else
        {
            lowStockAlerts = await _unitOfWork.PosStocks
                .GetTotalLowStockCountAsync();
        }

        return new DashboardData
        {
            TotalProducts = totalProducts,
            TotalClients = totalClients,
            LoyaltyMembers = loyaltyMembers,
            TodaySalesAmount = todayTotal,
            TodayInvoiceCount = todayCount,
            LowStockAlerts = lowStockAlerts
        };
    }
}

public class DashboardData
{
    public decimal TodaySalesAmount { get; set; }
    public int TodayInvoiceCount { get; set; }
    public int TotalProducts { get; set; }
    public int TotalClients { get; set; }
    public int LoyaltyMembers { get; set; }
    public int LowStockAlerts { get; set; }
}