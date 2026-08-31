using System.Windows;
using System.Windows.Controls.Primitives;
using SFE.WPF.ViewModels;

namespace SFE.WPF;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () => Close();
    }

    /// <summary>
    /// When any dropdown opens, close all others (only one open at a time).
    /// </summary>
    private void OnDropdownOpened(object sender, RoutedEventArgs e)
    {
        var opened = sender as ToggleButton;

        foreach (var btn in new ToggleButton?[]
        {
            FichierDropdownBtn,
            EditerDropdownBtn,
            AffichageDropdownBtn,
            OutilsDropdownBtn,
            AideDropdownBtn,
            UserDropdownBtn
        })
        {
            if (btn != null && btn != opened)
                btn.IsChecked = false;
        }
    }

    /// <summary>
    /// Close all dropdowns after a sub-item is clicked.
    /// </summary>
    private void CloseDropdowns(object sender, RoutedEventArgs e)
    {
        FichierDropdownBtn.IsChecked = false;
        EditerDropdownBtn.IsChecked = false;
        AffichageDropdownBtn.IsChecked = false;
        OutilsDropdownBtn.IsChecked = false;
        AideDropdownBtn.IsChecked = false;
        UserDropdownBtn.IsChecked = false;
    }
}