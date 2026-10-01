using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Abstractions;
using SFE.WPF.Helpers;

namespace SFE.WPF.ViewModels;

public partial class CustomerDisplayViewModel : ObservableObject
{
    private static readonly CultureInfo FrCulture = new("fr-FR");

    private readonly ITimeProvider _timeProvider;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _idleTimer; // 10-second idle timeout

    // ── Publicity ──
    private readonly DispatcherTimer _publicityTimer;
    private List<string> _publicityImages = new();
    private int _currentImageIndex = 0;

    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentDate = "";

    // ── States ──
    [ObservableProperty] private bool _showIdle = true;
    [ObservableProperty] private bool _showCart;
    [ObservableProperty] private bool _showReceipt;
    [ObservableProperty] private bool _isPublicityEnabled;
    [ObservableProperty] private ImageSource? _currentPublicityImage;

    // ── Cart & Receipt ──
    public ObservableCollection<CustomerDisplayItem> DisplayItems { get; } = new();
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private string _grandTotalLabel = "TOTAL TTC";
    [ObservableProperty] private int _itemCount;
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private ImageSource? _qrCodeImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompanyLogo))]
    private ImageSource? _companyLogo;

    public bool HasCompanyLogo => CompanyLogo != null;

    public CustomerDisplayViewModel(ITimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        // Clock Timer
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();

        // Publicity Slideshow Timer
        _publicityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _publicityTimer.Tick += (_, _) => CyclePublicityImage();

        // 10-Second Idle Timer
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _idleTimer.Tick += (_, _) =>
        {
            _idleTimer.Stop();
            SetIdle();
        };

        UpdateClock();
    }

    public void LoadCompanyLogo(byte[]? logoBytes)
    {
        if (logoBytes == null || logoBytes.Length == 0) return;
        try
        {
            using var stream = new MemoryStream(logoBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            CompanyLogo = bitmap;
        }
        catch { }
    }

    public void InitializePublicity(bool enable, string folderPath)
    {
        IsPublicityEnabled = enable;
        _publicityTimer.Stop();
        _publicityImages.Clear();
        CurrentPublicityImage = null;

        if (enable && !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            var validExtensions = new[] { ".png", ".jpg", ".jpeg" };
            _publicityImages = Directory.GetFiles(folderPath)
                .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            if (_publicityImages.Any())
            {
                CyclePublicityImage();
                _publicityTimer.Start();
            }
        }
    }

    private void CyclePublicityImage()
    {
        if (_publicityImages.Count == 0) return;
        try
        {
            if (_currentImageIndex >= _publicityImages.Count) _currentImageIndex = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(_publicityImages[_currentImageIndex], UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            CurrentPublicityImage = bitmap;
            _currentImageIndex++;
        }
        catch { }
    }

    private void UpdateClock()
    {
        var now = _timeProvider.LocalNow;
        CurrentTime = now.ToString("HH:mm:ss", FrCulture);
        CurrentDate = now.ToString("dddd dd MMMM yyyy", FrCulture);
    }

    public void SetIdle()
    {
        ShowIdle = true;
        ShowCart = false;
        ShowReceipt = false;
        DisplayItems.Clear();
        GrandTotal = 0;
        ItemCount = 0;
    }

    public void UpdateCart(IEnumerable<CartItemViewModel> items, decimal grandTotal, string grandTotalLabel, int itemCount)
    {
        ShowIdle = false;
        ShowCart = true;
        ShowReceipt = false;

        DisplayItems.Clear();
        foreach (var item in items)
        {
            DisplayItems.Add(new CustomerDisplayItem { Name = item.Name, Quantity = item.Quantity, Total = item.AmountTTC });
        }

        GrandTotal = grandTotal;
        GrandTotalLabel = grandTotalLabel;
        ItemCount = itemCount;

        // Return to idle if cart is emptied; otherwise, stop the idle timer so the cart stays visible
        if (itemCount == 0) _idleTimer.Start();
        else _idleTimer.Stop();
    }

    public void ShowNormalized(decimal total, string codeDEFDGI, string? qrContent)
    {
        ShowIdle = false;
        ShowCart = false;
        ShowReceipt = true;
        GrandTotal = total;
        CodeDEFDGI = codeDEFDGI;
        QrCodeImage = QrCodeHelper.Generate(qrContent, pixelsPerModule: 10);

        // Start 10-second countdown to return to publicity screen after payment
        _idleTimer.Start();
    }

    public void StopTimers()
    {
        _clock.Stop();
        _publicityTimer.Stop();
        _idleTimer.Stop();
    }
}

public class CustomerDisplayItem
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Total { get; set; }
}