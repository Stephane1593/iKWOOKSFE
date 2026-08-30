using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Licensing.Local.MachineFingerprintProviders;
using SFE.Licensing.Local.Signing;
using SFE.Licensing.Local.Storage;
using SFE.Licensing.Local.Time;
using System;

namespace SFE.Licensing.Local;

public static class LicensingLocalRegistration
{
    /// <summary>
    /// Registers the local licensing stack. Pass the Ed25519 public key bytes
    /// (32 bytes) and its pinned SHA-256 hex (from your build pipeline).
    /// </summary>
    public static IServiceCollection AddSfeLicensingLocal(
        this IServiceCollection services,
        byte[] licensePublicKey,
        string pinnedPublicKeySha256Hex)
    {
        services.AddSingleton<ILicenseVerifier>(
            _ => new Ed25519LicenseVerifier(licensePublicKey, pinnedPublicKeySha256Hex));

        services.AddSingleton<ILocalLicenseStore, LocalLicenseStore>();
        services.AddSingleton<IAntiClockTamper, AntiClockTamper>();

        if (OperatingSystem.IsWindows())
            services.AddSingleton<IMachineFingerprintProvider,
                SFE.Licensing.Local.MachineFingerprintProviders.WindowsMachineFingerprintProvider>();
        else
            services.AddSingleton<IMachineFingerprintProvider,
                SFE.Licensing.Local.MachineFingerprintProviders.NullMachineFingerprintProvider>();

        services.AddSingleton<ITrialIssuer, NoTrialIssuer>();
        services.AddSingleton<ILicenseGuard, LicenseGuard>();
        services.AddSingleton<ILicenseService, LicenseServiceAdapter>();
        return services;
    }
}