namespace SFE.Licensing.Domain;

/// <summary>
/// Immutable projection of the current license state, safe to pass around and
/// bind to the UI. Recomputed by the guard whenever anything changes.
/// </summary>
public sealed record LicenseSnapshot(
    LicenseStatus Status,
    LicenseClaims? Claims,
    DateTimeOffset EvaluatedAtUtc,
    DateTimeOffset? LastSuccessfulContactUtc,
    DateTimeOffset? GraceStartedAtUtc,
    int? DaysUntilExpiry,
    int? DaysOfGraceRemaining,
    string? Reason)
{
    public bool AllowsInvoicing => !Status.IsFatal();
    public bool HasFeature(Feature f) => Claims?.HasFeature(f) == true;
}