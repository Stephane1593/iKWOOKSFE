using System;
using System.Windows;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class OfflineQrWindow : Window
{
    private readonly OfflineQrViewModel _vm;

    public OfflineQrWindow(OfflineQrViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        _vm.CloseRequested += OnCloseRequested;
        Loaded += async (_, _) => await _vm.LoadAsync();  // see note below
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _vm.CloseRequested -= OnCloseRequested;
        _vm.Dispose();          // stops the timer + cancels the token
        base.OnClosed(e);
    }
}