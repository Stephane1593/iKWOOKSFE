using System.Text.Json.Serialization;

namespace SFE.Licensing.Domain;

/// <summary>
/// The canonical payload signed by the portal. Field names are single-char / short
/// on purpose — this blob is embedded in a base64 file and we want it compact.
/// DO NOT rename JsonPropertyName values without bumping <see cref="V"/>.
/// </summary>
public sealed class LicenseClaims
{
    [JsonPropertyName("v")] public int Version { get; init; } = 1;
    [JsonPropertyName("lid")] public string LicenseId { get; init; } = "";     // Ulid string
    [JsonPropertyName("csid")] public string CompanySyncId { get; init; } = ""; // Company.SyncId
    [JsonPropertyName("cn")] public string CompanyName { get; init; } = "";
    [JsonPropertyName("ed")] public string Edition { get; init; } = "Standalone"; // "Standalone" | "MultiPos"
    [JsonPropertyName("maxPos")] public int MaxPointsOfSale { get; init; } = 1;
    [JsonPropertyName("maxUsers")] public int MaxUsers { get; init; } = 5;
    [JsonPropertyName("slots")] public int ActivationSlots { get; init; } = 1;
    [JsonPropertyName("feat")] public List<string> Features { get; init; } = new();
    [JsonPropertyName("iat")] public long IssuedAtUnix { get; init; }
    [JsonPropertyName("nbf")] public long NotBeforeUnix { get; init; }
    [JsonPropertyName("exp")] public long ExpiresAtUnix { get; init; }
    [JsonPropertyName("grace")] public int GraceDays { get; init; } = 14;
    [JsonPropertyName("hb")] public int HeartbeatIntervalHours { get; init; } = 6;
    [JsonPropertyName("rev")] public bool Revocable { get; init; } = true;
    [JsonPropertyName("iss")] public string Issuer { get; init; } = "sfe.portal.v1";
    [JsonPropertyName("kind")] public string Kind { get; init; } = "full"; // "full" | "trial"
    [JsonPropertyName("fp")] public string BoundFingerprint { get; init; } = "";

    // -- Convenience --
    [JsonIgnore] public DateTimeOffset IssuedAt => DateTimeOffset.FromUnixTimeSeconds(IssuedAtUnix);
    [JsonIgnore] public DateTimeOffset NotBefore => DateTimeOffset.FromUnixTimeSeconds(NotBeforeUnix);
    [JsonIgnore] public DateTimeOffset ExpiresAt => DateTimeOffset.FromUnixTimeSeconds(ExpiresAtUnix);
    [JsonIgnore] public bool IsTrial => Kind.Equals("trial", StringComparison.OrdinalIgnoreCase);

    public bool HasFeature(Feature f) =>
        Features.Contains(f.ToToken(), StringComparer.OrdinalIgnoreCase);
}