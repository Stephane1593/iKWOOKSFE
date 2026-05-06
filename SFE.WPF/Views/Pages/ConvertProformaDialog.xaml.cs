using System.Windows;
using System.Windows.Controls;
using SFE.Application.Helpers;
using SFE.Domain.Enums;

namespace SFE.WPF.Views.Pages;

public partial class ConvertProformaDialog : Window
{
    public InvoiceType SelectedType { get; private set; } = InvoiceType.FV;
    public decimal AdvanceAmount { get; private set; }

    public ConvertProformaDialog(string proformaNumber, decimal totalTtc)
    {
        InitializeComponent();
        InfoText.Text = $"Proforma {proformaNumber} — Total : {totalTtc:N2} CDF.\n"
                      + "Choisissez le type de facture fiscale à émettre. La proforma sera marquée comme convertie après normalisation DGI.";
        TypeCombo.SelectionChanged += TypeCombo_SelectionChanged;
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = ((ComboBoxItem?)TypeCombo.SelectedItem)?.Tag?.ToString();
        AdvancePanel.Visibility = (tag == "FT" || tag == "ET")
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var tag = ((ComboBoxItem?)TypeCombo.SelectedItem)?.Tag?.ToString() ?? "FV";
        SelectedType = tag switch
        {
            "EV" => InvoiceType.EV,
            "FT" => InvoiceType.FT,
            "ET" => InvoiceType.ET,
            _ => InvoiceType.FV
        };

        if (SelectedType is InvoiceType.FT or InvoiceType.ET)
        {
            if (!DecimalParsingHelper.TryParseFlexible(AdvanceBox.Text, out var amt) || amt <= 0)
            {
                MessageBox.Show("Le montant de l'acompte doit être un nombre positif.",
                                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            AdvanceAmount = amt;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}