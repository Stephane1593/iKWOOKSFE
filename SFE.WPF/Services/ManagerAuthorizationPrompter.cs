using System;
using System.Threading.Tasks;
using System.Windows;
using SFE.Application.Interfaces;
using SFE.Domain.Enums;
using SFE.WPF.ViewModels;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.Services;

public sealed class ManagerAuthorizationPrompter : IManagerAuthorizationPrompter
{
    private readonly IManagerAuthorizationService _svc;
    private readonly IBarcodeScannerService _scanner;

    public ManagerAuthorizationPrompter(
        IManagerAuthorizationService svc,
        IBarcodeScannerService scanner)
    {
        _svc = svc;
        _scanner = scanner;
    }

    public Task<Guid?> RequireAsync(ManagerAction action, AuthorizationContext ctx)
    {
        var vm = new ManagerAuthorizationViewModel(_svc, action, ctx);

        // Anchor on the cashier's main window so this dialog opens on the
        // primary (cashier-facing) screen — never on the customer display.
        var mainWindow = System.Windows.Application.Current?.MainWindow;

        var win = new ManagerAuthorizationDialog(vm)
        {
            Owner = mainWindow,
            WindowStartupLocation = mainWindow != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Topmost = false,
        };

        if (mainWindow == null)
        {
            win.WindowStartupLocation = WindowStartupLocation.Manual;
            win.Left = (SystemParameters.PrimaryScreenWidth - win.Width) / 2;
            win.Top = (SystemParameters.PrimaryScreenHeight - win.Height) / 2;
        }

        void OnScanned(string code)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    vm.BarcodePayload = code;
                    await vm.SubmitBarcodeCommand.ExecuteAsync(null);
                }
                catch { /* vm surfaces its own errors */ }
            });
        }

        Guid? result = null;

        try
        {
            // IMPORTANT: Start() is idempotent (guarded by _running).
            // Calling it here guarantees the scanner is live for the badge
            // scan even if nothing else has started it yet.
            _scanner.Start();
            _scanner.CodeScanned += OnScanned;

            var ok = win.ShowDialog() == true;
            result = ok ? vm.TicketId : (Guid?)null;
        }
        finally
        {
            _scanner.CodeScanned -= OnScanned;
            // NOTE: we deliberately do NOT call _scanner.Stop() here.
            // Stopping it would kill the app-wide product-scan feature.
            // Start() is safe to call again on the next dialog.

            try { mainWindow?.Activate(); } catch { }
        }

        return Task.FromResult(result);
    }
}