using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Application.Services;

namespace SFE.WPF.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly DashboardService _dashboardService;

    [ObservableProperty]
    private string _todaySalesAmount = "0 CDF";

    [ObservableProperty]
    private string _todayInvoiceCount = "0";

    [ObservableProperty]
    private string _loyaltyMemberCount = "0";

    [ObservableProperty]
    private string _lowStockAlerts = "0";

    [ObservableProperty]
    private string _totalProducts = "0";

    [ObservableProperty]
    private string _totalClients = "0";

    public DashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        PageTitle = "Tableau de bord";

        _ = LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _dashboardService.LoadDashboardAsync();

            TodaySalesAmount = $"{data.TodaySalesAmount:N0} CDF";
            TodayInvoiceCount = data.TodayInvoiceCount.ToString();
            LoyaltyMemberCount = data.LoyaltyMembers.ToString();
            LowStockAlerts = data.LowStockAlerts.ToString();
            TotalProducts = data.TotalProducts.ToString();
            TotalClients = data.TotalClients.ToString();
        }
        catch
        {
            // Silencieux pour le dashboard
        }
        finally
        {
            IsBusy = false;
        }
    }
}