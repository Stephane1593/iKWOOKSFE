using System.Text.Json.Serialization;

namespace SFE.Licensing.Domain;

/// <summary>Sent by the installation to the portal every HeartbeatIntervalHours.</summary>
public sealed class HeartbeatReport
{
    [JsonPropertyName("fp")] public string Fingerprint { get; init; } = "";
    [JsonPropertyName("licId")] public string? LicenseId { get; init; }
    [JsonPropertyName("csid")] public string CompanySyncId { get; init; } = "";
    [JsonPropertyName("mn")] public string MachineName { get; init; } = "";
    [JsonPropertyName("os")] public string OsVersion { get; init; } = "";
    [JsonPropertyName("app")] public string AppVersion { get; init; } = "";
    [JsonPropertyName("posCount")] public int PointOfSaleCount { get; init; }
    [JsonPropertyName("posActive")] public int ActivePointOfSaleCount { get; init; }
    [JsonPropertyName("userCount")] public int UserCount { get; init; }
    [JsonPropertyName("invToday")] public int InvoicesToday { get; init; }
    [JsonPropertyName("invMonth")] public int InvoicesThisMonth { get; init; }
    [JsonPropertyName("mcfOk")] public bool AnyMcfHealthy { get; init; }
    [JsonPropertyName("lastZ")] public DateTimeOffset? LastZReportAt { get; init; }
    [JsonPropertyName("clockUtc")] public DateTimeOffset ClientClockUtc { get; init; }
    [JsonPropertyName("boot")] public DateTimeOffset ClientBootUtc { get; init; }
}

/// <summary>Portal's reply. May carry a refreshed license blob or remote commands.</summary>
public sealed class HeartbeatResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; init; } = true;
    [JsonPropertyName("licRefresh")] public string? RefreshedLicenseBlob { get; init; }
    [JsonPropertyName("revoked")] public bool Revoked { get; init; }
    [JsonPropertyName("msg")] public string? Message { get; init; }
    [JsonPropertyName("nextHb")] public int? NextHeartbeatMinutes { get; init; }
}