using System.Windows;
using SFE.WPF.ViewModels;
using System.Windows.Input;

namespace SFE.WPF.Views.Pages;

public partial class SessionCloseDialog : Window
{
    public SessionCloseDialog()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SessionCloseViewModel vm)
        {
            vm.SessionClosed += () =>
            {
                DialogResult = true;
                Close();
            };

            vm.CloseRequested += () =>
            {
                DialogResult = false;
                Close();
            };
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}