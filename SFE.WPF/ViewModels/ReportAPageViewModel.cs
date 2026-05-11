using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;
using SFE.WPF.Services;

namespace SFE.WPF.ViewModels;

public partial class ReportAPageViewModel : BaseReportListViewModel
{
    protected override ReportType ReportType => ReportType.A;
    protected override string TypePrefix => "A";

    // 🔧 FIX : suppression de _timeProvider (shadow) et _isBusy (redondant)

    public ReportAPageViewModel(
        IUnitOfWork uow,
        ReportService reportService,
        CashSessionState sessionState,
        IAuthService authService,
        ITimeProvider timeProvider)
        : base(uow, reportService, sessionState, authService, timeProvider)
    {
    }

    [RelayCommand]
    private async Task GenerateA()
    {
        IsLoading = true;
        ClearStatus();

        try
        {
            var report = await _reportService.GenerateReportAAsync(GetOperatorName());
            await LoadAsync();
            ShowStatus($"✓ A-Rapport N°{report.ReportNumber} généré.", false);

            var newest = Reports.FirstOrDefault();
            if (newest != null) SelectedReport = newest;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur : {ex.Message}", true);
        }
        finally
        {
            IsLoading = false;
        }
    }
}