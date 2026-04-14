using System.Windows;
using System.Windows.Controls;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class StockTransferPage : UserControl
{
    public StockTransferPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is StockTransferViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }
}