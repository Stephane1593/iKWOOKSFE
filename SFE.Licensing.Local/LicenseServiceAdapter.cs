using SFE.Application.Interfaces;

namespace SFE.Licensing.Local;

/// <summary>
/// Bridges the app-layer ILicenseService to the real ILicenseGuard.
/// </summary>
public sealed class LicenseServiceAdapter : ILicenseService
{
    private readonly ILicenseGuard _guard;
    public LicenseServiceAdapter(ILicenseGuard guard) => _guard = guard;

    public int MaxUsers => _guard.Current.Claims?.MaxUsers ?? 0;
    public int MaxPointsOfSale => _guard.Current.Claims?.MaxPointsOfSale ?? 0;
    public bool IsUsable => _guard.Current.AllowsInvoicing;
}