using SFE.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SFE.WPF.Views.Pages
{
    /// <summary>
    /// Interaction logic for StockPage.xaml
    /// </summary>
    public partial class StockPage : UserControl
    {
        public StockPage()
        {
            InitializeComponent();
            // ✅ Reload every time the page becomes visible
            Loaded += StockPage_Loaded;
        }

        private async void StockPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is StockViewModel vm)
            {
                await vm.LoadCommand.ExecuteAsync(null);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
