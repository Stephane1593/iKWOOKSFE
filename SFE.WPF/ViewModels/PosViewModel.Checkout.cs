using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Application.Payments;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class PosViewModel
{
    // ══════ CANAL D'ENCAISSEMENT ══════
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocalPos))]
    [NotifyPropertyChangedFor(nameof(IsSunmiChannel))]
    private PaymentChannel _paymentChannel = PaymentChannel.LocalPos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSunmiQr))]
    [NotifyPropertyChangedFor(nameof(IsSunmiLan))]
    private SunmiHandoff _sunmiHandoff = SunmiHandoff.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMobileMoney))]
    private string? _selectedMobileOperator;

    public bool IsLocalPos => PaymentChannel == PaymentChannel.LocalPos;
    public bool IsSunmiChannel => PaymentChannel == PaymentChannel.SunmiTerminal;
    public bool IsSunmiQr => IsSunmiChannel && SunmiHandoff == SunmiHandoff.ShowQr;
    public bool IsSunmiLan => IsSunmiChannel && SunmiHandoff == SunmiHandoff.LanDevice;
    public bool IsMobileMoney => SelectedPaymentType == PaymentType.MobileMoney;

    /// <summary>
    /// True when the proforma button should be enabled — cart has items and
    /// the invoice hasn't already been fiscalised.
    /// </summary>
    public bool CanPrintProforma =>
        !IsNormalized && CartItems.Count > 0 && HasThermalPrinter;

    public ObservableCollection<string> MobileMoneyOperators { get; } = new()
    {
        "M-Pesa", "Airtel Money", "Orange Money"
    };

    // ══════ SÉLECTION ══════

    [RelayCommand]
    private void SelectPaymentChannel(PaymentChannel channel)
    {
        PaymentChannel = channel;
        if (channel == PaymentChannel.LocalPos)
            SunmiHandoff = SunmiHandoff.None;
        else
            SelectedMobileOperator = null;
        ClearStatus();
    }

    [RelayCommand]
    private void SelectLocalMethod(PaymentType type)
    {
        PaymentChannel = PaymentChannel.LocalPos;
        SunmiHandoff = SunmiHandoff.None;
        SelectedPaymentType = type;
        if (type != PaymentType.MobileMoney) SelectedMobileOperator = null;
        ClearStatus();
    }

    [RelayCommand]
    private void SelectMobileOperator(string? op)
    {
        PaymentChannel = PaymentChannel.LocalPos;
        SunmiHandoff = SunmiHandoff.None;
        SelectedPaymentType = PaymentType.MobileMoney;
        SelectedMobileOperator = op;
        ClearStatus();
    }

    [RelayCommand]
    private void SelectSunmiHandoff(SunmiHandoff handoff)
    {
        PaymentChannel = PaymentChannel.SunmiTerminal;
        SunmiHandoff = handoff;
        SelectedPaymentType = PaymentType.CarteBancaire;
        ClearStatus();
    }

    partial void OnSelectedPaymentTypeChanged(PaymentType value)
    {
        OnPropertyChanged(nameof(IsMobileMoney));
        if (value != PaymentType.MobileMoney) SelectedMobileOperator = null;
    }

    // ══════════════════════════════════════════════════════════
    //  PROFORMA (preview — never touches the DB)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Prints the current cart as a proforma on the local thermal printer.
    /// If the Sunmi channel is selected, ALSO publishes the draft to the
    /// pending-order store so the Sunmi's own "Print proforma" button can
    /// fetch <c>GET /orders/{id}/receipt/proforma</c> and print on its
    /// built-in printer.
    ///
    /// Does NOT create an Invoice row, does NOT normalise, does NOT charge.
    /// Idempotent — can be pressed as many times as the cashier likes.
    /// </summary>
    [RelayCommand]
    private async Task PrintProforma()
    {
        if (IsNormalized)
        {
            StatusMessage = "La facture est déjà normalisée. Utilisez « Réimprimer » pour un duplicata.";
            ShowError = true;
            return;
        }
        if (CartItems.Count == 0)
        {
            StatusMessage = "Panier vide — rien à imprimer.";
            ShowError = true;
            return;
        }
        if (!HasThermalPrinter)
        {
            StatusMessage = "Aucune imprimante thermique détectée.";
            ShowError = true;
            return;
        }
        if (SelectedPointOfSale == null)
        {
            StatusMessage = "Veuillez sélectionner un point de vente.";
            ShowError = true;
            return;
        }

        ClearStatus();

        // Build a preview invoice from the current cart state. No payments,
        // no advance amount — the builder will skip the payment section.
        var draft = BuildInvoice(paidAmount: 0m, advanceAmount: 0m);
        draft.Payments.Clear();      // this is a *preview* — no fiscal payment info

        // If the Sunmi is targeted, publish so its poll picks up this order
        // and its /orders/{id}/receipt/proforma call works.
        if (IsSunmiChannel)
        {
            _pendingOrderStore.Set(
                new OrderDto(
                    OrderId: draft.InvoiceNumber,
                    Label: $"PROFORMA {draft.InvoiceNumber}",
                    Amount: draft.TotalTTC,
                    Currency: draft.CurrencyCode ?? "CDF"),
                draft);
        }

        // Print locally on the WPF thermal.
        await PrintThermalReceiptAsync(draft, isDuplicate: false, asProforma: true);

        StatusMessage = IsSunmiChannel
            ? "✓ Proforma imprimée — disponible aussi sur le terminal Sunmi."
            : "✓ Proforma imprimée.";
        ShowSuccess = true;
    }

    // ══════════════════════════════════════════════════════════
    //  PAIEMENT
    // ══════════════════════════════════════════════════════════

    private sealed class PaymentExecution
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public bool ChargeApproved { get; init; }

        public static PaymentExecution Ok(bool chargeApproved = false) =>
            new() { Success = true, ChargeApproved = chargeApproved };
        public static PaymentExecution Fail(string m) =>
            new() { Success = false, ErrorMessage = m };
    }

    private async Task<PaymentExecution> ExecutePaymentAsync(Invoice invoice)
    {
        if (PaymentChannel == PaymentChannel.LocalPos)
            return PaymentExecution.Ok();

        return SunmiHandoff switch
        {
            SunmiHandoff.ShowQr => await CollectViaSunmiAsync(invoice, showQrWindow: true),
            SunmiHandoff.LanDevice => await CollectViaSunmiAsync(invoice, showQrWindow: false),
            _ => PaymentExecution.Fail("Terminal Sunmi : choisissez « Afficher QR » ou « Terminal LAN ».")
        };
    }

    private async Task<PaymentExecution> CollectViaSunmiAsync(Invoice invoice, bool showQrWindow)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetService<IPaymentProvider>();
        if (provider is null)
            return PaymentExecution.Fail("Paiement carte indisponible : provider non enregistré.");

        string idempotencyKey = invoice.InvoiceNumber;

        // Make the invoice visible to OfflineQrResolver/Sunmi.
        _pendingOrderStore.Set(
            new OrderDto(
                OrderId: idempotencyKey,
                Label: $"FACTURE {invoice.InvoiceNumber}",
                Amount: invoice.TotalTTC,
                Currency: invoice.CurrencyCode ?? "CDF"),
            invoice);

        OfflineQrViewModel? qrVm = null;
        Views.Pages.OfflineQrWindow? qrWindow = null;
        bool closedByUser = false;

        if (showQrWindow)
        {
            qrVm = new OfflineQrViewModel(_scopeFactory, idempotencyKey);
            qrWindow = new Views.Pages.OfflineQrWindow(qrVm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            qrWindow.Closed += (_, _) => closedByUser = true;
            qrWindow.Show();
        }

        try
        {
            StatusMessage = showQrWindow
                ? "Scannez le QR sur le terminal Sunmi…"
                : "Ordre envoyé au terminal Sunmi — suivez les instructions sur l'écran du terminal…";

            var deadline = _time.UtcNow.AddSeconds(120);

            while (_time.UtcNow < deadline)
            {
                if (closedByUser)
                    return PaymentExecution.Fail("Fenêtre QR fermée avant l'encaissement.");

                var res = await provider.QueryAsync(idempotencyKey, CancellationToken.None);

                switch (res.Status)
                {
                    case PaymentTransactionStatus.Approved:
                        return PaymentExecution.Ok(chargeApproved: true);

                    case PaymentTransactionStatus.Declined:
                        return PaymentExecution.Fail($"Paiement refusé : {res.Reason}");

                    case PaymentTransactionStatus.TimedOut:
                        return PaymentExecution.Fail("Délai d'encaissement dépassé sur le terminal.");

                }

                await Task.Delay(1500);
            }

            return PaymentExecution.Fail("Aucune réponse du terminal Sunmi (délai dépassé).");
        }
        finally
        {
            if (qrWindow is not null && !closedByUser)
                qrWindow.Close();

            qrVm?.Dispose();

            _pendingOrderStore.Clear();
        }
    }
}