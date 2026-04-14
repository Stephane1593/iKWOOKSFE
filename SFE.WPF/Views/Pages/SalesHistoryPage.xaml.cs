using System.Windows.Controls;
using System.Windows.Input;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class SalesHistoryPage : UserControl
{
    public SalesHistoryPage()
    {
        InitializeComponent();

        this.KeyDown += OnKeyDown;
        this.Focusable = true;
        this.Loaded += (_, _) => this.Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SalesHistoryViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape when vm.ShowDetail:
                vm.CloseDetailCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F5:
                if (vm.SearchCommand.CanExecute(null))
                    vm.SearchCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}