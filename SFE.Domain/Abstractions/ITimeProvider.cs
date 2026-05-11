namespace SFE.Domain.Abstractions;

/// <summary>
/// DGI §1.1 — single source of truth for time in SFE.
/// Storage is always UTC. Display is always converted through this provider,
/// which is aware that DRC spans TWO timezones:
///   • West (UTC+1) — Kinshasa, Matadi, Bandundu, Mbandaka, Kikwit
///   • East (UTC+2) — Lubumbashi, Goma, Bukavu, Kisangani, Kananga, Mbuji-Mayi
/// Neither half observes DST.
/// </summary>
public interface ITimeProvider
{
    // ── UTC (storage) ─────────────────────────────────────────
    DateTimeOffset UtcNow { get; }
    DateOnly UtcToday { get; }

    // ── Default app zone (head-office / tenant default) ───────
    TimeZoneInfo AppTimeZone { get; }
    DateTimeOffset LocalNow { get; }

    // ── Zone resolution ───────────────────────────────────────
    /// <summary>
    /// Resolves a timezone by ID with robust cross-platform fallbacks.
    /// Returns <see cref="AppTimeZone"/> if <paramref name="id"/> is null/empty.
    /// Never throws — unknown IDs degrade to <see cref="AppTimeZone"/>.
    /// </summary>
    TimeZoneInfo GetZone(string? id);

    // ── UTC → local conversions ───────────────────────────────
    DateTimeOffset ToLocal(DateTimeOffset utc);
    DateTime ToLocal(DateTime utc);

    DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone);
    DateTime ToLocal(DateTime utc, TimeZoneInfo zone);

    DateTimeOffset ToLocal(DateTimeOffset utc, string? zoneId);
    DateTime ToLocal(DateTime utc, string? zoneId);

    // ── Aliases ───────────────────────────────────────────────
    DateTimeOffset ToAppLocal(DateTimeOffset utc);
    DateTime ToAppLocal(DateTime utc);
}