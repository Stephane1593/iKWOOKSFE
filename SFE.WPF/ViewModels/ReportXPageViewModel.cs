using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
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

    // 🔧 FIX : plus de _timeProvider shadow, plus de _isBusy redondant
    // → on utilise IsLoading hérité de la base

    public ReportXPageViewModel(
        IUnitOfWork uow,
        ReportService reportService,
        CashSessionState sessionState,
        IAuthService authService,
        ITimeProvider timeProvider)
        : base(uow, reportService, sessionState, authService, timeProvider)
    {
    }

    [RelayCommand]
    private async Task GenerateXDaily()
    {
        IsLoading = true;                    // 🔧 unifié
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
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateXPeriodic()
    {
        if (PeriodicFrom.Date > PeriodicTo.Date)
        {
            ShowStatus("La date de début doit précéder la date de fin.", true);
            return;
        }

        IsLoading = true;
        ClearStatus();

        try
        {
            // 🔧 borne exclusive — évite de perdre les ms du dernier jour
            var report = await _reportService.GenerateReportXPeriodicAsync(
                GetOperatorName(),
                PeriodicFrom.Date,
                PeriodicTo.Date.AddDays(1));   // [from, to) exclusif

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
            IsLoading = false;
        }
    }
}