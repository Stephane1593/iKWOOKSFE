using System.Windows;
using SFE.WPF.ViewModels;

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
}