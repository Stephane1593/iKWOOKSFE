using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Licensing.Local;
using SFE.Licensing.Domain;
using System.Collections.ObjectModel;
using System.IO;

namespace SFE.WPF.ViewModels;

public partial class BulkInvoicingViewModel : BaseViewModel, IActivatable
{
    private readonly IBulkInvoiceService _bulk;
    private readonly IExcelInvoiceParser _parser;
    private readonly IUnitOfWork _uow;
    private readonly IAuthService _session;
    private readonly ITimeProvider _time;
    private readonly ILicenseGuard _guard;
    private CancellationTokenSource? _cts;

    public ObservableCollection<PointOfSale> AvailablePointsOfSale { get; } = new();
    public ObservableCollection<BulkInvoicePreview> ParsedInvoices { get; } = new();
    public ObservableCollection<BulkParseError> ParseErrors { get; } = new();
    public ObservableCollection<BulkRowResult> ExecutionResults { get; } = new();

    [ObservableProperty] private PointOfSale? selectedPointOfSale;
    [ObservableProperty] private string? selectedFilePath;
    [ObservableProperty] private BulkParseResult? currentParseResult;
    [ObservableProperty] private bool isParsing;
    [ObservableProperty] private bool isExecuting;
    [ObservableProperty] private int progressCurrent;
    [ObservableProperty] private int progressTotal;
    [ObservableProperty] private string progressCaption = "";
    [ObservableProperty] private string progressPhase = "";
    [ObservableProperty] private int successCount;
    [ObservableProperty] private int failureCount;
    [ObservableProperty] private string elapsedText = "";
    [ObservableProperty] private string remainingText = "";
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private bool canExecute;
    [ObservableProperty] private bool executionFinished;

    [ObservableProperty] private bool _canUseBulkInvoicing = true;
    [ObservableProperty] private string _bulkLockReason = "";
    [ObservableProperty] private bool _isBulkLocked;

    public BulkInvoicingViewModel(
        IBulkInvoiceService bulk,
        IExcelInvoiceParser parser,
        IUnitOfWork uow,
        IAuthService session,
        ITimeProvider time,
        ILicenseGuard guard)
    {
        _bulk = bulk;
        _parser = parser;
        _uow = uow;
        _session = session;
        _time = time;
        _guard = guard;

        _guard.StatusChanged += OnLicenseChanged;
        RefreshLicenseGate();
    }

    private void OnLicenseChanged(LicenseSnapshot _)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) RefreshLicenseGate();
        else d.Invoke(RefreshLicenseGate);
    }

    private void RefreshLicenseGate()
    {
        CanUseBulkInvoicing = _guard.TryUse(Feature.BulkInvoicing, out var reason);
        BulkLockReason = reason ?? "";
        IsBulkLocked = !CanUseBulkInvoicing;

        // If the licence just went away mid-session, kill any pending "ready to execute"
        // state so the user can't hit Traiter after the fact.
        if (!CanUseBulkInvoicing)
        {
            CanExecute = false;
            _cts?.Cancel();
        }
    }

    public async Task ActivateAsync()
    {
        RefreshLicenseGate();

        AvailablePointsOfSale.Clear();
        foreach (var pos in await _uow.PointsOfSale.GetAllAsync())
            AvailablePointsOfSale.Add(pos);
        SelectedPointOfSale ??= AvailablePointsOfSale.FirstOrDefault();
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Fichiers Excel (*.xlsx)|*.xlsx",
            Title = "Choisir le fichier de factures"
        };
        if (dlg.ShowDialog() != true) return;
        SelectedFilePath = dlg.FileName;
    }

    [RelayCommand]
    private async Task DownloadTemplateAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Fichiers Excel (*.xlsx)|*.xlsx",
            FileName = "Factures_Modele.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        await using var fs = File.Create(dlg.FileName);
        await _parser.WriteTemplateAsync(fs);
    }

    [RelayCommand]
    private async Task ParseAsync()
    {
        if (!_guard.TryUse(Feature.BulkInvoicing, out var why))
        {
            LastError = why;
            System.Windows.MessageBox.Show(why!,
                "Fonctionnalité non disponible",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(SelectedFilePath) || SelectedPointOfSale == null)
        {
            LastError = "Sélectionnez un fichier et un point de vente.";
            return;
        }

        IsParsing = true;
        CanExecute = false;
        ExecutionFinished = false;
        ParsedInvoices.Clear();
        ParseErrors.Clear();
        ExecutionResults.Clear();
        LastError = null;

        try
        {
            var user = _session.CurrentUser;
            await using var fs = File.OpenRead(SelectedFilePath);
            var result = await _bulk.ParseAndValidateAsync(
                fs, SelectedPointOfSale.Id,
                user?.Id.ToString() ?? "0",
                user?.FullName ?? "Opérateur",
                CancellationToken.None);

            CurrentParseResult = result;

            foreach (var e in result.Errors) ParseErrors.Add(e);
            foreach (var inv in result.Invoices)
                ParsedInvoices.Add(new BulkInvoicePreview(inv));

            CanExecute = result.IsValid
                         && result.Invoices.Count > 0
                         && CanUseBulkInvoicing;
            ProgressCaption = $"{result.Invoices.Count} facture(s) prête(s), {result.Errors.Count} erreur(s).";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally { IsParsing = false; }
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (!_guard.TryUse(Feature.BulkInvoicing, out var why))
        {
            // Re-run the full refresh so the banner appears if the state just changed.
            RefreshLicenseGate();
            LastError = why;
            return;
        }
        if (CurrentParseResult == null || !CanExecute) return;

        IsExecuting = true;
        CanExecute = false;
        ExecutionFinished = false;
        ExecutionResults.Clear();
        SuccessCount = 0;
        FailureCount = 0;
        _cts = new CancellationTokenSource();

        var progress = new Progress<BulkProgress>(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ProgressPhase = p.Phase;
            ProgressCaption = $"{p.Current} / {p.Total} — {p.CurrentReference}";
            SuccessCount = p.Successes;
            FailureCount = p.Failures;
            ElapsedText = p.Elapsed.ToString(@"mm\:ss");
            RemainingText = p.EstimatedRemaining.ToString(@"mm\:ss");
            if (!string.IsNullOrEmpty(p.LastError)) LastError = p.LastError;
        });

        try
        {
            var result = await _bulk.ExecuteAsync(CurrentParseResult, progress, _cts.Token);
            foreach (var r in result.Results) ExecutionResults.Add(r);
            ProgressCaption = result.WasCancelled
                ? $"Interrompu — {result.SuccessCount} succès, {result.FailureCount} échecs."
                : $"Terminé — {result.SuccessCount} succès, {result.FailureCount} échecs.";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsExecuting = false;
            ExecutionFinished = true;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void Reset()
    {
        SelectedFilePath = null;
        CurrentParseResult = null;
        ParsedInvoices.Clear();
        ParseErrors.Clear();
        ExecutionResults.Clear();
        LastError = null;
        CanExecute = false;
        ExecutionFinished = false;
        ProgressCaption = "";
        SuccessCount = FailureCount = ProgressCurrent = ProgressTotal = 0;
    }
}

public class BulkInvoicePreview
{
    public string Reference { get; }
    public string Type { get; }
    public string ClientName { get; }
    public int LineCount { get; }
    public decimal TotalTTC { get; }
    public string Currency { get; }

    public BulkInvoicePreview(Invoice inv)
    {
        Reference = inv.CommentA;
        Type = inv.Type.ToString();
        ClientName = inv.ClientName;
        LineCount = inv.Lines.Count;
        TotalTTC = inv.TotalTTC;
        Currency = inv.CurrencyCode;
    }
}