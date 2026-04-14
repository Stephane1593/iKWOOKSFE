using System.Windows.Controls;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class ClientsPage : UserControl
{
    public ClientsPage(ClientsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}