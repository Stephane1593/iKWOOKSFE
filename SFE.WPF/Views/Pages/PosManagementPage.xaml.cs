using SFE.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SFE.WPF.Views.Pages;

public partial class PosManagementPage : UserControl
{
    public PosManagementPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PointOfSaleManagementViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }
}