using System.Windows.Controls;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Views.Pages;

public partial class BulkInvoicingPage : UserControl
{
    public BulkInvoicingPage(BulkInvoicingViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}