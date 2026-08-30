using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using SFE.Licensing.Domain;
using SFE.Licensing.Local;
using SFE.Licensing.Local.MachineFingerprintProviders;
using System.Windows.Input;

namespace SFE.WPF.Views.Pages;

public partial class LicenseBlockedWindow : Window
{
    private readonly ILicenseGuard _guard;
    private DispatcherTimer? _copyResetTimer;

    public LicenseBlockedWindow(ILicenseGuard guard)
    {
        InitializeComponent();
        _guard = guard;
        Refresh();
    }

    private void Refresh()
    {
        var snap = _guard.Current;
        StatusText.Text = $"Statut : {snap.Status}. {snap.Reason}".Trim();

        // Show fingerprint so the user can send it to support.
        var fp = App.ServiceProvider.GetService(typeof(IMachineFingerprintProvider))
            as IMachineFingerprintProvider;

        if (fp is null)
        {
            FingerprintText.Text = "(empreinte machine indisponible)";
            CopyButton.IsEnabled = false;
            return;
        }

        var fingerprint = fp.Compute().Value;
        FingerprintText.Text =
            $"Empreinte machine : {fingerprint}{Environment.NewLine}" +
            $"Machine        : {Environment.MachineName}{Environment.NewLine}" +
            $"Utilisateur    : {Environment.UserName}{Environment.NewLine}" +
            $"OS             : {Environment.OSVersion}{Environment.NewLine}" +
            $"Date           : {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
            $"Statut licence : {snap.Status} - {snap.Reason}";

        CopyButton.IsEnabled = true;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = FingerprintText.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return;

            Clipboard.SetText(text);

            // Visual feedback
            CopyIcon.Kind = PackIconKind.Check;
            CopyButtonText.Text = "COPIÉ";

            // Reset previous timer if still running
            _copyResetTimer?.Stop();
            _copyResetTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _copyResetTimer.Tick += (_, _) =>
            {
                CopyIcon.Kind = PackIconKind.ContentCopy;
                CopyButtonText.Text = "COPIER";
                _copyResetTimer?.Stop();
                _copyResetTimer = null;
            };
            _copyResetTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Impossible de copier les informations machine :\n" + ex.Message,
                "Licence SFE", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Sélectionnez le fichier de licence",
            Filter = "Fichier de licence SFE (*.lic;*.dat)|*.lic;*.dat|Tous les fichiers|*.*"
        };

        if (dlg.ShowDialog(this) != true) return;

        // Disable buttons during install to prevent double-click issues
        InstallButton.IsEnabled = false;
        QuitButton.IsEnabled = false;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        try
        {
            var blob = await File.ReadAllTextAsync(dlg.FileName);
            var snap = await _guard.InstallLicenseAsync(blob);

            if (!snap.Status.IsFatal())
            {
                MessageBox.Show(this, "Licence installée avec succès.",
                    "Licence SFE", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
                return;
            }

            Refresh();
            MessageBox.Show(this,
                $"Licence chargée mais toujours refusée : {snap.Reason}",
                "Licence SFE", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Fichier de licence invalide.\n\n" + ex.Message,
                "Licence SFE", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            InstallButton.IsEnabled = true;
            QuitButton.IsEnabled = true;
        }
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _copyResetTimer?.Stop();
        _copyResetTimer = null;
        base.OnClosed(e);
    }
}