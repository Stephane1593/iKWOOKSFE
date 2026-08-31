namespace SFE.Application.Interfaces;

/// <summary>
/// Lightweight view of the active license for the application layer.
/// Implemented in the licensing layer (adapter over ILicenseGuard) to avoid
/// a circular project reference.
/// </summary>
public interface ILicenseService
{
    /// Max user accounts allowed. 0 or less = unlimited.
    int MaxUsers { get; }

    /// Max points of sale allowed. 0 or less = unlimited.
    int MaxPointsOfSale { get; }

    /// True when the license is currently usable.
    bool IsUsable { get; }
}