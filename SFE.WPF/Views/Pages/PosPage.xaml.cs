using System.Windows.Controls;
using System.Windows.Input;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class PosPage : UserControl
{
    public PosPage()
    {
        InitializeComponent();

        // Raccourcis clavier
        this.KeyDown += PosPage_KeyDown;
        this.Focusable = true;
        this.Loaded += (_, _) => this.Focus();
    }

    private void PosPage_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PosViewModel vm) return;

        switch (e.Key)
        {
            case Key.F9:
                // F9 = Mettre en attente
                if (vm.RequestHoldCommand.CanExecute(null))
                    vm.RequestHoldCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F10:
                // F10 = Ouvrir/Fermer paniers en attente
                if (vm.ToggleHeldPanelCommand.CanExecute(null))
                    vm.ToggleHeldPanelCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F12:
                // F12 = Encaisser
                if (vm.ProcessSaleCommand.CanExecute(null))
                    vm.ProcessSaleCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                // Escape = Fermer panneau / dialogue
                if (vm.ShowHoldDialog)
                {
                    vm.CancelHoldCommand.Execute(null);
                    e.Handled = true;
                }
                else if (vm.ShowHeldPanel)
                {
                    vm.ShowHeldPanel = false;
                    e.Handled = true;
                }
                break;
        }
    }
}