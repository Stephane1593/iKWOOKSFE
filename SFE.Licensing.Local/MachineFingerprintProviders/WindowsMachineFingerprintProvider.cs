using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using SFE.Licensing.Domain;

namespace SFE.Licensing.Local.MachineFingerprintProviders;

/// <summary>
/// Fingerprint = SHA-256 over:
///   - HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
///   - MAC of the first non-virtual physical adapter (sorted for stability)
///   - Windows install date
///   - Machine SID (from WMI)
/// Windows-only at runtime; DI registers <see cref="NullMachineFingerprintProvider"/>
/// on non-Windows hosts (server-side verifiers).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsMachineFingerprintProvider : IMachineFingerprintProvider
{
    public MachineFingerprint Compute()
    {
        var parts = new List<string?>
        {
            ReadMachineGuid(),
            ReadPrimaryMac(),
            ReadInstallDate(),
            ReadMachineSid()
        };

        var joined = string.Join("|", parts.Select(p => p ?? "-"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return new MachineFingerprint(Convert.ToHexString(hash));
    }

    private static string? ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", writable: false);
            return key?.GetValue("MachineGuid") as string;
        }
        catch { return null; }
    }

    private static string? ReadPrimaryMac()
    {
        try
        {
            var macs = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                         || n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Down)
                .Where(n => n.NetworkInterfaceType is
                    System.Net.NetworkInformation.NetworkInterfaceType.Ethernet or
                    System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                .Where(n => !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                .Where(n => !n.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.GetPhysicalAddress().ToString())
                .Where(m => !string.IsNullOrEmpty(m) && m != "000000000000")
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToList();

            return macs.FirstOrDefault();
        }
        catch { return null; }
    }

    private static string? ReadInstallDate()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable: false);
            return key?.GetValue("InstallDate")?.ToString();
        }
        catch { return null; }
    }

    private static string? ReadMachineSid()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SID FROM Win32_UserAccount WHERE LocalAccount=True AND SIDType=1");
            foreach (var obj in searcher.Get())
            {
                var sid = obj["SID"]?.ToString();
                if (!string.IsNullOrEmpty(sid))
                {
                    var dash = sid.LastIndexOf('-');
                    if (dash > 0) return sid[..dash];
                }
            }
        }
        catch { /* WMI may be locked down; fall through */ }
        return null;
    }
}

/// <summary>
/// Non-Windows fallback and server-side default. Returns an empty fingerprint;
/// server-side code paths never rely on the local fingerprint anyway.
/// </summary>
public sealed class NullMachineFingerprintProvider : IMachineFingerprintProvider
{
    public MachineFingerprint Compute() => new MachineFingerprint(string.Empty);
}