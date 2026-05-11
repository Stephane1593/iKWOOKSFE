using SFE.WPF.ViewModels;
using System.Windows.Controls;

namespace SFE.WPF.Views.Pages;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as DashboardViewModel)?.StopClock();
    }
}