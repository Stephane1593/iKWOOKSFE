using System.Windows;
using System.Windows.Controls;

namespace SFE.WPF.Views.Pages;

public partial class ProductsPage : UserControl
{
    public ProductsPage()
    {
        InitializeComponent();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }
}