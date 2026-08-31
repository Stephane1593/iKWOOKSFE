using System.Windows.Controls;
using System.Windows.Input;
using SFE.WPF.ViewModels;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows;

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
        Unloaded += OnUnloaded;
        if (DataContext is PosViewModel vm)
        {
            vm.CartPulseRequested += AnimateCartBadge;
            vm.TotalPulseRequested += AnimateTotal;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }

    private void AnimateCartBadge()
    {
        var scale = BadgeScale;
        if (scale == null) return;

        var anim = new DoubleAnimation
        {
            From = 1,
            To = 1.4,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void AnimateTotal()
    {
        //var scale = TotalScale;
        //if (scale == null) return;

        //var anim = new DoubleAnimation
        //{
        //    From = 1,
        //    To = 1.05,
        //    Duration = TimeSpan.FromMilliseconds(100),
        //    AutoReverse = true
        //};

        //scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        //scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
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