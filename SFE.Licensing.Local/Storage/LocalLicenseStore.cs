using System.Text.Json;
using SFE.Licensing.Domain;

namespace SFE.Licensing.Local.Storage;

public interface ILocalLicenseStore
{
    string LicenseFilePath { get; }
    string StateFilePath { get; }

    Task<string?> ReadLicenseBlobAsync(CancellationToken ct = default);
    Task WriteLicenseBlobAsync(string blob, CancellationToken ct = default);

    Task<LocalLicenseState> ReadStateAsync(CancellationToken ct = default);
    Task WriteStateAsync(LocalLicenseState state, CancellationToken ct = default);
}

public sealed class LocalLicenseStore : ILocalLicenseStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string LicenseFilePath { get; }
    public string StateFilePath { get; }

    public LocalLicenseStore(string? directoryOverride = null)
    {
        var dir = directoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SFE");
        Directory.CreateDirectory(dir);

        LicenseFilePath = Path.Combine(dir, "license.dat");
        StateFilePath = Path.Combine(dir, "license.state.json");
    }

    public async Task<string?> ReadLicenseBlobAsync(CancellationToken ct = default)
    {
        if (!File.Exists(LicenseFilePath)) return null;
        return await File.ReadAllTextAsync(LicenseFilePath, ct);
    }

    public async Task WriteLicenseBlobAsync(string blob, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var tmp = LicenseFilePath + ".tmp";
            await File.WriteAllTextAsync(tmp, blob, ct);
            File.Move(tmp, LicenseFilePath, overwrite: true);
        }
        finally { _gate.Release(); }
    }

    public async Task<LocalLicenseState> ReadStateAsync(CancellationToken ct = default)
    {
        if (!File.Exists(StateFilePath))
            return new LocalLicenseState();

        try
        {
            var json = await File.ReadAllTextAsync(StateFilePath, ct);
            return JsonSerializer.Deserialize<LocalLicenseState>(json) ?? new LocalLicenseState();
        }
        catch
        {
            // Corrupt state file is not fatal — we can rebuild everything from
            // the license blob + fingerprint. Just start fresh.
            return new LocalLicenseState();
        }
    }

    public async Task WriteStateAsync(LocalLicenseState state, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var tmp = StateFilePath + ".tmp";
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tmp, json, ct);
            File.Move(tmp, StateFilePath, overwrite: true);
        }
        finally { _gate.Release(); }
    }
}