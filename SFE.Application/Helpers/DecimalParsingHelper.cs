using System.Globalization;

namespace SFE.Application.Helpers;

/// <summary>
/// Gère le parsing décimal robuste : compatible comma-as-decimal (FR)
/// et dot-as-decimal (EN).
/// </summary>
public static class DecimalParsingHelper
{
    /// <summary>
    /// Parse de façon flexible un montant saisi par l'utilisateur.
    /// "3000,00" → 3000.00, "1.500,00" → 1500.00, "1,500.00" → 1500.00
    /// </summary>
    public static bool TryParseFlexible(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();

        bool hasDot = trimmed.Contains('.');
        bool hasComma = trimmed.Contains(',');

        if (hasComma && !hasDot)
        {
            // "3000,00" → French decimal
            trimmed = trimmed.Replace(',', '.');
        }
        else if (hasComma && hasDot)
        {
            int lastDot = trimmed.LastIndexOf('.');
            int lastComma = trimmed.LastIndexOf(',');

            if (lastComma > lastDot)
            {
                // "1.500,00" → European: dots = thousands, comma = decimal
                trimmed = trimmed.Replace(".", "").Replace(',', '.');
            }
            else
            {
                // "1,500.00" → US: commas = thousands, dot = decimal
                trimmed = trimmed.Replace(",", "");
            }
        }
        // If only dot → standard invariant, nothing to do

        return decimal.TryParse(trimmed, NumberStyles.Any,
            CultureInfo.InvariantCulture, out result);
    }

    /// <summary>Parse flexible, retourne 0 si échec.</summary>
    public static decimal ParseOrZero(string? value)
    {
        TryParseFlexible(value, out var result);
        return result;
    }
}