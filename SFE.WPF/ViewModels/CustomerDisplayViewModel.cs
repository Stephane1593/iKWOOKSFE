using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SFE.WPF.Helpers;

namespace SFE.WPF.ViewModels;

public partial class CustomerDisplayViewModel : ObservableObject
{
    private readonly DispatcherTimer _clock;

    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentDate = "";

    // ── States ──
    [ObservableProperty] private bool _showIdle = true;
    [ObservableProperty] private bool _showCart;
    [ObservableProperty] private bool _showReceipt;

    // ── Cart ──
    public ObservableCollection<CustomerDisplayItem> DisplayItems { get; } = new();
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private string _grandTotalLabel = "TOTAL TTC";
    [ObservableProperty] private int _itemCount;

    // ── Receipt ──
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private ImageSource? _qrCodeImage;

    public CustomerDisplayViewModel()
    {
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            CurrentDate = DateTime.Now.ToString("dddd dd MMMM yyyy",
                new System.Globalization.CultureInfo("fr-FR"));
        };
        _clock.Start();
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        CurrentDate = DateTime.Now.ToString("dddd dd MMMM yyyy",
            new System.Globalization.CultureInfo("fr-FR"));
    }

    /// <summary>Show idle/welcome screen.</summary>
    public void SetIdle()
    {
        ShowIdle = true;
        ShowCart = false;
        ShowReceipt = false;
        DisplayItems.Clear();
        GrandTotal = 0;
        ItemCount = 0;
    }

    /// <summary>Update the cart display from POS items.</summary>
    public void UpdateCart(IEnumerable<CartItemViewModel> items,
        decimal grandTotal, string grandTotalLabel, int itemCount)
    {
        ShowIdle = false;
        ShowCart = true;
        ShowReceipt = false;

        DisplayItems.Clear();
        foreach (var item in items)
        {
            DisplayItems.Add(new CustomerDisplayItem
            {
                Name = item.Name,
                Quantity = item.Quantity,
                Total = item.AmountTTC
            });
        }

        GrandTotal = grandTotal;
        GrandTotalLabel = grandTotalLabel;
        ItemCount = itemCount;
    }

    /// <summary>Show the normalized receipt on customer display.</summary>
    public void ShowNormalized(decimal total, string codeDEFDGI, string? qrContent)
    {
        ShowIdle = false;
        ShowCart = false;
        ShowReceipt = true;

        GrandTotal = total;
        CodeDEFDGI = codeDEFDGI;
        QrCodeImage = QrCodeHelper.Generate(qrContent, pixelsPerModule: 10);
    }
}

public class CustomerDisplayItem
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Total { get; set; }
}