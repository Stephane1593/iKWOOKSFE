using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.WPF.Services;

namespace SFE.WPF.ViewModels;

public partial class ReportXPageViewModel : BaseReportListViewModel
{
    protected override ReportType ReportType => ReportType.X;
    protected override string TypePrefix => "X";

    // ── Periodic date range ──
    [ObservableProperty] private DateTime _periodicFrom = DateTime.Today;
    [ObservableProperty] private DateTime _periodicTo = DateTime.Today;

    [ObservableProperty] private bool _isBusy;

    public ReportXPageViewModel(
        IUnitOfWork uow,
        ReportService reportService,
        CashSessionState sessionState,
        IAuthService authService)
        : base(uow, reportService, sessionState, authService)
    {
    }

    [RelayCommand]
    private async Task GenerateXDaily()
    {
        IsBusy = true;
        ClearStatus();

        try
        {
            var report = await _reportService.GenerateReportXAsync(GetOperatorName());
            await LoadAsync();
            ShowStatus($"✓ X-Rapport quotidien N°{report.ReportNumber} généré.", false);

            var newest = Reports.FirstOrDefault();
            if (newest != null) SelectedReport = newest;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur : {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateXPeriodic()
    {
        if (PeriodicFrom > PeriodicTo)
        {
            ShowStatus("La date de début doit précéder la date de fin.", true);
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            var report = await _reportService.GenerateReportXPeriodicAsync(
                GetOperatorName(), PeriodicFrom, PeriodicTo.Date.AddDays(1).AddSeconds(-1));

            await LoadAsync();
            ShowStatus($"✓ X-Rapport périodique N°{report.ReportNumber} généré.", false);

            var newest = Reports.FirstOrDefault();
            if (newest != null) SelectedReport = newest;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur : {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}