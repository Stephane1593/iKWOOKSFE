using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class SessionOpenDialog : Window
{
    public SessionOpenDialog()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Restrict input to digits, dots, and commas (for decimal amounts and rates).
    /// </summary>
    private void NumericInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.,]+$");
    }
}