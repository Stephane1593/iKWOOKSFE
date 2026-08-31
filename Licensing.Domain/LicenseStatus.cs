namespace SFE.Licensing.Domain;

public enum LicenseStatus
{
    /// <summary>No license file found yet; app is still bootstrapping.</summary>
    Unknown = 0,

    /// <summary>Auto-issued trial, still valid.</summary>
    Trial,

    /// <summary>Valid license, recent successful contact (or offline within tolerance).</summary>
    Active,

    /// <summary>Valid license but no successful heartbeat in a while; counter running.</summary>
    ActiveOffline,

    /// <summary>Past ExpiresAt but within GraceDays; banner shown, invoicing still works.</summary>
    GracePeriod,

    /// <summary>Portal signaled revocation. Read-only fiscal mode.</summary>
    Suspended,

    /// <summary>Past exp + grace with no valid heartbeat. Read-only fiscal mode.</summary>
    Expired,

    /// <summary>Clock tamper or fingerprint mismatch detected.</summary>
    Tampered
}

public static class LicenseStatusExtensions
{
    /// <summary>
    /// Fatal = the app must enter read-only fiscal mode for anything that creates
    /// non-fiscal invoices. Fiscal duties (duplicates, Z-reports, DGI exports) MUST
    /// still work regardless. See A4 in the design.
    /// </summary>
    public static bool IsFatal(this LicenseStatus s) =>
        s is LicenseStatus.Suspended or LicenseStatus.Expired or LicenseStatus.Tampered;

    /// <summary>Yellow banner status — user should be warned but can keep working.</summary>
    public static bool IsWarning(this LicenseStatus s) =>
        s is LicenseStatus.ActiveOffline or LicenseStatus.GracePeriod;
}