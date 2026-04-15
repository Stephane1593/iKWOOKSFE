using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            await vm.LoginAsync(PasswordBox.Password);
    }

    private void InputField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            PasswordBox.Focus();
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
            await vm.LoginAsync(PasswordBox.Password);
    }
}