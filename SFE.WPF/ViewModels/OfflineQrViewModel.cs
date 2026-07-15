using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Payments;
using SFE.Application.Services;
using SFE.WPF.Helpers;

namespace SFE.WPF.ViewModels;

public enum OfflineQrState { Loading, Ready, NothingDue, NotFound, Error }

public partial class OfflineQrViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _orderId;
    private readonly DispatcherTimer _refresh;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public OfflineQrViewModel(IServiceScopeFactory scopeFactory, string orderId)
    {
        _scopeFactory = scopeFactory;
        _orderId = orderId;

        // The amount due can change while the QR is shown (a colleague takes a
        // partial cash payment). Re-resolve periodically so the Sunmi never
        // scans a stale amount.
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _refresh.Tick += async (_, _) => await LoadCoreAsync(silent: true);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(ShowCloseOnly))]
    private OfflineQrState _state = OfflineQrState.Loading;

    [ObservableProperty] private BitmapImage? _qrImage;
    [ObservableProperty] private string _caption = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isFiscal;

    public bool IsLoading => State == OfflineQrState.Loading;
    public bool IsReady => State == OfflineQrState.Ready;
    public bool CanRetry => State == OfflineQrState.Error;   // retry only helps transient faults
    public bool ShowCloseOnly => State is OfflineQrState.NothingDue or OfflineQrState.NotFound;
    public Task LoadAsync() => LoadCoreAsync(silent: false);

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private Task Refresh() => LoadCoreAsync(silent: false);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private async Task LoadCoreAsync(bool silent)
    {
        if (_disposed) return;

        // Supersede any in-flight load. Swap the CTS out atomically-ish; a late
        // timer tick after Dispose() can't run because of the _disposed guard above.
        var previous = _cts;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try { previous.Cancel(); }
        catch (ObjectDisposedException) { /* already gone */ }
        finally { previous.Dispose(); }

        if (!silent) State = OfflineQrState.Loading;

        OfflineQrResult result;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<OfflineQrResolver>();
            result = await resolver.ResolveAsync(_orderId, ct);
        }
        catch (OperationCanceledException) { return; }   // superseded by a newer load
        catch
        {
            _refresh.Stop();
            if (!silent) SetError("Impossible de générer le code. Réessayez.");
            return;
        }

        if (_disposed || ct.IsCancellationRequested) return;

        switch (result.Outcome)
        {
            case OfflineQrOutcome.NotFound:
                _refresh.Stop();
                QrImage = null;
                StatusText = "Commande introuvable ou expirée.";
                State = OfflineQrState.NotFound;
                return;

            case OfflineQrOutcome.NothingDue:
                _refresh.Stop();
                QrImage = null;
                StatusText = "Cette commande est déjà réglée. Aucun montant à encaisser.";
                State = OfflineQrState.NothingDue;
                return;
        }

        var bmp = QrCodeHelper.Generate(result.Token);
        if (bmp is null) { _refresh.Stop(); SetError("Échec de génération du QR."); return; }

        QrImage = bmp;
        IsFiscal = result.Kind == OfflineDocKind.Fiscal;
        AmountText = $"{result.Order!.Amount:N0} {result.Order.Currency}";
        Caption = IsFiscal
            ? $"Reçu fiscal disponible hors-ligne — {result.Order.Label}"
            : $"REÇU PROVISOIRE (proforma) — {result.Order.Label}";
        StatusText = "Présentez ce code à la borne Sunmi.";
        State = OfflineQrState.Ready;

        if (!_disposed && !_refresh.IsEnabled) _refresh.Start();
    }

    private void SetError(string msg)
    {
        QrImage = null;
        StatusText = msg;
        State = OfflineQrState.Error;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _refresh.Stop();

        try { _cts.Cancel(); }
        catch (ObjectDisposedException) { /* already disposed */ }

        _cts.Dispose();
    }
}