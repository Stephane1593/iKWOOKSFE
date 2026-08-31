namespace SFE.Licensing.Domain;

/// <summary>
/// Thrown when code calls <c>ILicenseGuard.Require(feature)</c> and the current
/// license doesn't include it, OR when the license is in a fatal state. Caught by
/// the WPF shell to show the license modal — never let this bubble to the user
/// as an unhandled exception.
/// </summary>
public sealed class FeatureBlockedException : Exception
{
    public Feature? Feature { get; }
    public LicenseStatus Status { get; }

    public FeatureBlockedException(LicenseStatus status, Feature? feature, string message)
        : base(message)
    {
        Status = status;
        Feature = feature;
    }
}