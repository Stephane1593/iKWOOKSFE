using System.Windows;
using SFE.WPF.ViewModels;

namespace SFE.WPF;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}