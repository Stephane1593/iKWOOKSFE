using System.Globalization;
using System.Windows.Data;
using SFE.Domain.Enums;

namespace SFE.WPF.Converters; // ← match your other converters' namespace

public sealed class PaymentTypeLabelConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) => v switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèque",
        PaymentType.Credit => "Crédit",
        PaymentType.Autre => "Autre",
        _ => v?.ToString() ?? ""
    };
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}