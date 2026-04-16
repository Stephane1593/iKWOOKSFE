using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.WPF.Services;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.ViewModels;

public partial class ReportZPageViewModel : BaseReportListViewModel
{
    protected override ReportType ReportType => ReportType.Z;
    protected override string TypePrefix => "Z";

    public bool CanGenerate => _sessionState.IsSessionOpen && !_sessionState.IsSetupMode;
    public string GenerateTooltip => _sessionState.IsSetupMode
        ? "Non disponible en mode configuration"
        : !_sessionState.IsSessionOpen
            ? "Aucune session active"
            : "Clôturer la session et générer le Z-Rapport";

    /// <summary>
    /// Fired after a Z-report is generated so the host (MainViewModel) can log out.
    /// </summary>
    public event Action? SessionClosedByZ;

    public ReportZPageViewModel(
        IUnitOfWork uow,
        ReportService reportService,
        CashSessionState sessionState,
        IAuthService authService)
        : base(uow, reportService, sessionState, authService)
    {
    }

    [RelayCommand]
    private async Task GenerateZ()
    {
        if (_sessionState.IsSetupMode)
        {
            ShowStatus("Mode configuration — clôture non disponible.", true);
            return;
        }

        if (!_sessionState.IsSessionOpen)
        {
            ShowStatus("Aucune session active à clôturer.", true);
            return;
        }

        // Open the session close dialog
        var vm = App.ServiceProvider.GetRequiredService<SessionCloseViewModel>();
        var dialog = new SessionCloseDialog { DataContext = vm };

        var mainWin = System.Windows.Application.Current.MainWindow;
        if (mainWin != null) dialog.Owner = mainWin;

        var result = dialog.ShowDialog();

        if (result == true && vm.GeneratedReport != null)
        {
            await LoadAsync();
            ShowStatus($"✓ Z-Rapport N°{vm.GeneratedReport.ReportNumber} généré avec succès.", false);

            // Select the newly generated report
            var newest = Reports.FirstOrDefault();
            if (newest != null) SelectedReport = newest;

            // Signal session closed
            SessionClosedByZ?.Invoke();
        }
    }
}