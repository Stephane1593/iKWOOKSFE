using CommunityToolkit.Mvvm.ComponentModel;

namespace SFE.WPF.ViewModels;

/// <summary>
/// A single city row in the DRC world-clock strip.
/// Updated every second by DashboardViewModel.
/// </summary>
public partial class CityTimeItem : ObservableObject
{
    public string CityName { get; init; } = "";
    public string Region { get; init; } = "";
    public TimeSpan UtcOffset { get; init; }

    public string UtcOffsetLabel =>
        $"UTC{(UtcOffset.Ticks >= 0 ? "+" : "")}{UtcOffset.Hours}";

    [ObservableProperty] private string _currentTime = "--:--:--";
    [ObservableProperty] private string _currentDateShort = "";
    [ObservableProperty] private bool _isBusinessOpen;
    [ObservableProperty] private string _businessStatus = "";
    [ObservableProperty] private string _periodLabel = ""; // Matin / Après-midi / Soir / Nuit

    /// <summary>Pulses once per second from the dashboard timer.</summary>
    public void Update(DateTimeOffset utcNow)
    {
        var local = utcNow.ToOffset(UtcOffset);

        CurrentTime = local.ToString("HH:mm:ss");
        CurrentDateShort = local.ToString("ddd dd MMM");

        // Business hours: Mon–Sat, 08:00–18:00 (DRC typical retail hours)
        var h = local.Hour;
        var isWeekend = local.DayOfWeek == DayOfWeek.Sunday;
        IsBusinessOpen = !isWeekend && h >= 8 && h < 18;
        BusinessStatus = IsBusinessOpen ? "Ouvert" : "Fermé";

        PeriodLabel = h switch
        {
            >= 5 and < 12 => "Matin",
            >= 12 and < 17 => "Après-midi",
            >= 17 and < 21 => "Soir",
            _ => "Nuit"
        };
    }
}