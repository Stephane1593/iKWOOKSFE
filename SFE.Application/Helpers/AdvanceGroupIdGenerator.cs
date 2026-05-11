using System.Security.Cryptography;
using SFE.Domain.Abstractions;

namespace SFE.Application.Helpers;

/// <summary>
/// Generates collision-free, human-readable IDs for advance invoice chains.
/// Format: ADV-{YEAR}/{8-CHAR-HEX}
/// Example: ADV-2026/A1B2C3D4
/// </summary>
public static class AdvanceGroupIdGenerator
{
    public static string Generate(ITimeProvider time,DateTime? referenceDate = null)
    {
        var date = referenceDate ?? time.UtcNow;
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        var hex = Convert.ToHexString(buf);  // 8 uppercase chars
        return $"ADV-{date:yyyy}/{hex}";
    }

    /// <summary>
    /// Validates the format ADV-YYYY/XXXXXXXX (case-insensitive hex).
    /// </summary>
    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (!id.StartsWith("ADV-", StringComparison.Ordinal)) return false;

        var parts = id[4..].Split('/');
        if (parts.Length != 2) return false;
        if (parts[0].Length != 4 || !int.TryParse(parts[0], out _)) return false;
        if (parts[1].Length != 8) return false;

        foreach (var c in parts[1])
            if (!Uri.IsHexDigit(c)) return false;

        return true;
    }
}