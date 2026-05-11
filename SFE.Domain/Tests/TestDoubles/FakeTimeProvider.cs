using SFE.Domain.Abstractions;

namespace SFE.Domain.Tests.TestDoubles;

/// <summary>
/// Deterministic <see cref="ITimeProvider"/> for unit tests.
/// Defaults to DRC West (UTC+1, Kinshasa, no DST) but supports per-zone lookups
/// so tests can simulate POS in Goma/Lubumbashi (UTC+2) without touching the OS clock.
/// </summary>
public sealed class FakeTimeProvider : ITimeProvider
{
    // ──────────────────────────────────────────────────────────
    //  Canonical test zones (no DST, no OS dependency)
    // ──────────────────────────────────────────────────────────
    private static readonly TimeZoneInfo WestDrc = TimeZoneInfo.CreateCustomTimeZone(
        "SFE-Test-West", TimeSpan.FromHours(1),
        "SFE Test DRC West (UTC+1)", "SFE Test DRC West (UTC+1)");

    private static readonly TimeZoneInfo EastDrc = TimeZoneInfo.CreateCustomTimeZone(
        "SFE-Test-East", TimeSpan.FromHours(2),
        "SFE Test DRC East (UTC+2)", "SFE Test DRC East (UTC+2)");

    private static readonly Dictionary<string, TimeZoneInfo> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // IANA
            ["Africa/Kinshasa"] = WestDrc,
            ["Africa/Brazzaville"] = WestDrc,
            ["Africa/Lagos"] = WestDrc,
            ["Africa/Luanda"] = WestDrc,
            ["Africa/Lubumbashi"] = EastDrc,
            ["Africa/Maputo"] = EastDrc,
            ["Africa/Harare"] = EastDrc,
            ["Africa/Johannesburg"] = EastDrc,

            // Windows
            ["W. Central Africa Standard Time"] = WestDrc,
            ["South Africa Standard Time"] = EastDrc,

            // Cities
            ["Kinshasa"] = WestDrc,
            ["Matadi"] = WestDrc,
            ["Bandundu"] = WestDrc,
            ["Mbandaka"] = WestDrc,
            ["Kikwit"] = WestDrc,
            ["Boma"] = WestDrc,
            ["Lubumbashi"] = EastDrc,
            ["Goma"] = EastDrc,
            ["Bukavu"] = EastDrc,
            ["Kisangani"] = EastDrc,
            ["Kananga"] = EastDrc,
            ["Mbuji-Mayi"] = EastDrc,
            ["Mbuji Mayi"] = EastDrc,
            ["Kolwezi"] = EastDrc,
            ["Likasi"] = EastDrc,
            ["Uvira"] = EastDrc,
            ["Beni"] = EastDrc,
            ["Butembo"] = EastDrc,

            // Short codes
            ["WEST"] = WestDrc,
            ["W"] = WestDrc,
            ["UTC+1"] = WestDrc,
            ["EAST"] = EastDrc,
            ["E"] = EastDrc,
            ["UTC+2"] = EastDrc,
        };

    private DateTimeOffset _now;

    public TimeZoneInfo AppTimeZone { get; }

    // ──────────────────────────────────────────────────────────
    //  Ctors
    // ──────────────────────────────────────────────────────────
    public FakeTimeProvider(DateTimeOffset initial, TimeZoneInfo? displayZone = null)
    {
        _now = initial;
        AppTimeZone = displayZone ?? WestDrc;
    }

    public FakeTimeProvider(DateTimeOffset initial, string? displayZoneId)
    {
        _now = initial;
        AppTimeZone = ResolveOrDefault(displayZoneId, WestDrc);
    }

    // ──────────────────────────────────────────────────────────
    //  UTC (storage)
    // ──────────────────────────────────────────────────────────
    public DateTimeOffset UtcNow => _now.ToUniversalTime();
    public DateOnly UtcToday => DateOnly.FromDateTime(_now.UtcDateTime);

    // ──────────────────────────────────────────────────────────
    //  Default (app) zone
    // ──────────────────────────────────────────────────────────
    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(_now, AppTimeZone);

    public DateTimeOffset ToLocal(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, AppTimeZone);

    public DateTime ToLocal(DateTime utc) => ToLocal(utc, AppTimeZone);

    public DateTimeOffset ToAppLocal(DateTimeOffset utc) => ToLocal(utc);
    public DateTime ToAppLocal(DateTime utc) => ToLocal(utc);

    // ──────────────────────────────────────────────────────────
    //  Explicit-zone overloads (per-POS)
    // ──────────────────────────────────────────────────────────
    public DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone ?? AppTimeZone);

    public DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
    {
        var z = zone ?? AppTimeZone;

        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            DateTimeKind.Local => throw new ArgumentException(
                "DateTimeKind.Local leaked into ToLocal — caller used DateTime.Now somewhere.",
                nameof(utc)),
            _ => utc
        };

        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, z);
    }

    public DateTimeOffset ToLocal(DateTimeOffset utc, string? zoneId) =>
        ToLocal(utc, GetZone(zoneId));

    public DateTime ToLocal(DateTime utc, string? zoneId) =>
        ToLocal(utc, GetZone(zoneId));

    // ──────────────────────────────────────────────────────────
    //  Zone resolution — deterministic, never touches the OS
    // ──────────────────────────────────────────────────────────
    public TimeZoneInfo GetZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return AppTimeZone;

        return Aliases.TryGetValue(id.Trim(), out var tz) ? tz : AppTimeZone;
    }

    private static TimeZoneInfo ResolveOrDefault(string? id, TimeZoneInfo fallback)
    {
        if (string.IsNullOrWhiteSpace(id)) return fallback;
        return Aliases.TryGetValue(id.Trim(), out var tz) ? tz : fallback;
    }

    // ──────────────────────────────────────────────────────────
    //  Test controls
    // ──────────────────────────────────────────────────────────
    public void Advance(TimeSpan by) => _now = _now.Add(by);
    public void SetUtc(DateTimeOffset utc) => _now = utc;
}