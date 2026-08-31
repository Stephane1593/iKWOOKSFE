namespace SFE.Domain.Enums;

/// <summary>
/// État connexion serveur MCF — spec MCF C2h champ STA
/// </summary>
public enum McfConnectionStatus
{
    DIS, // Non connecté au réseau
    CON, // Connecté, pas d'envoi en cours
    TRA, // Envoi de données en cours
    RES  // Redémarrage en cours
}

/// <summary>
/// Synthetic health verdict derived from C1h + C2h responses.
/// Use this for dashboards / monitoring instead of looking at raw STA,
/// because STA=DIS is the normal idle state of an Incotex MCF.
/// </summary>
public enum McfHealth
{
    /// <summary>Could not determine — communication error.</summary>
    Unknown = 0,
    /// <summary>Device is operating normally.</summary>
    Healthy = 1,
    /// <summary>Device is working but showing warning signs (growing backlog, stale sync, transient errors).</summary>
    Degraded = 2,
    /// <summary>Device cannot reach the DGI server (hard transport failure or very large backlog).</summary>
    Unhealthy = 3
}

public class McfHealthReport
{
    public McfHealth Status { get; set; } = McfHealth.Unknown;

    /// <summary>Short human-readable summary, e.g. "OK" or "Backlog growing (87 pending)".</summary>
    public string Summary { get; set; } = "";

    /// <summary>All non-fatal warnings found, even when Status is Healthy/Degraded.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Raw STA value reported by the MCF (CON / TRA / DIS).</summary>
    public string? RawConnectionStatus { get; set; }

    /// <summary>Number of fiscal transactions still queued inside the MCF (DC field).</summary>
    public int PendingCount { get; set; }

    /// <summary>Total transactions successfully sent to DGI since reset (EC field).</summary>
    public int SentCount { get; set; }

    /// <summary>Time elapsed since last successful DGI handshake. Null if MCF has never connected.</summary>
    public TimeSpan? TimeSinceLastSync { get; set; }

    public DateTimeOffset? LastServerConnection { get; set; }

    public string? LastError { get; set; }

    /// <summary>True if the underlying C2h call itself failed (so all other fields are unreliable).</summary>
    public bool CommunicationFailed { get; set; }
}

/// <summary>
/// Tunable thresholds for <see cref="McfSerialClient.GetHealthReportAsync"/>.
/// Defaults are conservative; adjust to match your invoice volume.
/// </summary>
public class McfHealthThresholds
{
    /// <summary>Pending count above this → Degraded.</summary>
    public int DegradedPendingCount { get; set; } = 5;

    /// <summary>Pending count above this → Unhealthy.</summary>
    public int UnhealthyPendingCount { get; set; } = 50;

    /// <summary>Time since last sync above this → Degraded.</summary>
    public TimeSpan DegradedSyncAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Time since last sync above this → Unhealthy.</summary>
    public TimeSpan UnhealthySyncAge { get; set; } = TimeSpan.FromHours(6);

    /// <summary>If true, the device having never connected is considered Unhealthy.</summary>
    public bool TreatNeverConnectedAsUnhealthy { get; set; } = true;

    public static McfHealthThresholds Default => new();
}