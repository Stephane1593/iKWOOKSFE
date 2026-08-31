using System.Collections.Concurrent;
using SFE.Domain.Abstractions;

namespace SFE.Application.Services;

/// <summary>
/// DRC-aware time provider. Never reads the OS timezone.
/// Handles the two DRC zones (UTC+1 Kinshasa / UTC+2 Lubumbashi) plus
/// arbitrary IANA / Windows IDs, with safe fallbacks.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    // ──────────────────────────────────────────────────────────
    //  KNOWN DRC ZONES — authoritative fallbacks (no DST)
    // ──────────────────────────────────────────────────────────
    private static readonly TimeZoneInfo WestDrc = BuildZone(
        iana: "Africa/Kinshasa",
        windows: "W. Central Africa Standard Time",
        fallbackId: "SFE-DRC-West-UTC+1",
        fallbackOffsetHours: 1,
        fallbackDisplay: "DRC West (UTC+1, Kinshasa)");

    private static readonly TimeZoneInfo EastDrc = BuildZone(
        iana: "Africa/Lubumbashi",
        windows: "South Africa Standard Time",     // same UTC+2 no-DST profile
        fallbackId: "SFE-DRC-East-UTC+2",
        fallbackOffsetHours: 2,
        fallbackDisplay: "DRC East (UTC+2, Lubumbashi)");

    /// <summary>Case-insensitive alias map → canonical TimeZoneInfo.</summary>
    private static readonly Dictionary<string, TimeZoneInfo> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // IANA
            ["Africa/Kinshasa"] = WestDrc,
            ["Africa/Brazzaville"] = WestDrc,
            ["Africa/Lagos"] = WestDrc,   // same offset, widely installed
            ["Africa/Luanda"] = WestDrc,
            ["Africa/Lubumbashi"] = EastDrc,
            ["Africa/Maputo"] = EastDrc,
            ["Africa/Harare"] = EastDrc,
            ["Africa/Johannesburg"] = EastDrc,

            // Windows
            ["W. Central Africa Standard Time"] = WestDrc,
            ["South Africa Standard Time"] = EastDrc,

            // Cities (convenience — use in config / POS.TimeZoneId)
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

    /// <summary>Runtime cache for any other system ID the caller requests.</summary>
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> _resolvedCache =
        new(StringComparer.OrdinalIgnoreCase);

    // ──────────────────────────────────────────────────────────
    //  APP DEFAULT ZONE
    // ──────────────────────────────────────────────────────────
    public TimeZoneInfo AppTimeZone { get; }

    /// <summary>
    /// Default ctor → Kinshasa (UTC+1). Most tenants install the app in
    /// the west; POSes in the east override per-site via PointOfSale.TimeZoneId.
    /// </summary>
    public SystemTimeProvider() : this(WestDrc) { }

    /// <summary>Use this overload if the tenant's head office is in the east.</summary>
    public SystemTimeProvider(string? defaultZoneId)
        : this(ResolveOrDefault(defaultZoneId, WestDrc)) { }

    public SystemTimeProvider(TimeZoneInfo defaultZone)
    {
        AppTimeZone = defaultZone ?? WestDrc;
    }

    // ──────────────────────────────────────────────────────────
    //  UTC (storage)
    // ──────────────────────────────────────────────────────────
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    // ──────────────────────────────────────────────────────────
    //  LOCAL (app default zone)
    // ──────────────────────────────────────────────────────────
    public DateTimeOffset LocalNow =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, AppTimeZone);

    public DateTimeOffset ToLocal(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, AppTimeZone);

    public DateTime ToLocal(DateTime utc) => ToLocal(utc, AppTimeZone);

    public DateTimeOffset ToAppLocal(DateTimeOffset utc) => ToLocal(utc);
    public DateTime ToAppLocal(DateTime utc) => ToLocal(utc);

    // ──────────────────────────────────────────────────────────
    //  LOCAL (explicit zone — per POS)
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
                "ToLocal received a DateTime with Kind=Local. SFE stores UTC only; "
                + "a caller is leaking DateTime.Now. Fix the source, not this call.",
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
    //  ZONE RESOLUTION
    // ──────────────────────────────────────────────────────────
    public TimeZoneInfo GetZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return AppTimeZone;

        var key = id.Trim();

        // 1) Curated aliases (cities, short codes, IANA, Windows)
        if (Aliases.TryGetValue(key, out var aliased))
            return aliased;

        // 2) Resolved cache
        if (_resolvedCache.TryGetValue(key, out var cached))
            return cached;

        // 3) Try system lookup — never throws out
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(key);
            _resolvedCache[key] = tz;
            return tz;
        }
        catch
        {
            // 4) Last resort → app default
            return AppTimeZone;
        }
    }

    // ──────────────────────────────────────────────────────────
    //  INFRASTRUCTURE
    // ──────────────────────────────────────────────────────────
    private static TimeZoneInfo ResolveOrDefault(string? id, TimeZoneInfo fallback)
    {
        if (string.IsNullOrWhiteSpace(id)) return fallback;

        if (Aliases.TryGetValue(id.Trim(), out var aliased))
            return aliased;

        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return fallback; }
    }

    private static TimeZoneInfo BuildZone(
        string iana, string windows,
        string fallbackId, int fallbackOffsetHours, string fallbackDisplay)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(iana); } catch { }
        try { return TimeZoneInfo.FindSystemTimeZoneById(windows); } catch { }

        return TimeZoneInfo.CreateCustomTimeZone(
            id: fallbackId,
            baseUtcOffset: TimeSpan.FromHours(fallbackOffsetHours),
            displayName: fallbackDisplay,
            standardDisplayName: fallbackDisplay);
    }
}