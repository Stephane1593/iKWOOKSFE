using System.Windows;
using System.Windows.Input;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class ManagerAuthorizationDialog : Window
{
    private readonly ManagerAuthorizationViewModel _vm;

    public ManagerAuthorizationDialog(ManagerAuthorizationViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.Succeeded += () => { DialogResult = true; Close(); };
        vm.Cancelled += () => { DialogResult = false; Close(); };

        Loaded += (_, _) => BarcodeBox.Focus();
    }

    private void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private async void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        // HID barcode scanners emit CR (or CRLF) after the payload.
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            await _vm.SubmitBarcodeCommand.ExecuteAsync(null);
        }
    }

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Pin = PinBox.Password;

    private void CredBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.CredPassword = CredBox.Password;
}