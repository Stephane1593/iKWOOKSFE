using System;
using System.Windows;
using SFE.Application.Interfaces;
using SFE.Domain.Enums;
using SFE.WPF.ViewModels;
using SFE.WPF.Views.Pages;
using System.Threading.Tasks;

namespace SFE.WPF.Services;

public sealed class ManagerAuthorizationPrompter : IManagerAuthorizationPrompter
{
    private readonly IManagerAuthorizationService _svc;
    private readonly IBarcodeScannerService _scanner;

    public ManagerAuthorizationPrompter(IManagerAuthorizationService svc, IBarcodeScannerService scanner)
    {
        _svc = svc;
        _scanner = scanner;
    }

    public Task<Guid?> RequireAsync(ManagerAction action, AuthorizationContext ctx)
    {
        var vm = new ManagerAuthorizationViewModel(_svc, action, ctx);
        var win = new ManagerAuthorizationDialog(vm)
        {
            Owner =  System.Windows.Application.Current?.MainWindow
        };

        void OnScanned(string code)
        {
            // Populate barcode field and submit on UI thread
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    vm.BarcodePayload = code;
                    await vm.SubmitBarcodeCommand.ExecuteAsync(null);
                }
                catch { /* swallow - vm handles errors */ }
            });
        }

        Guid? result = null;

        try
        {
            // Start scanner and subscribe
            _scanner.CodeScanned += OnScanned;
            _scanner.Start();

            var ok = win.ShowDialog() == true;
            result = ok ? vm.TicketId : (Guid?)null;
        }
        finally
        {
            // Unsubscribe and stop scanner to avoid leaking handlers
            _scanner.CodeScanned -= OnScanned;
            _scanner.Stop();
        }

        return Task.FromResult(result);
    }
}