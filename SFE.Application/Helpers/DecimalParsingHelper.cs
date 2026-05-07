using System.Globalization;

namespace SFE.Application.Helpers;

/// <summary>
/// Robust decimal parsing for user-entered monetary values.
/// Handles French, US, European, and space-separated formats, as well as
/// currency suffixes and accounting negatives.
///
/// Accepted inputs (all parse to 1500.00):
///   "1500"         "1500,00"       "1500.00"
///   "1 500,00"     "1 500.00"      "1\u00A0500,00"   (NBSP)
///   "1.500,00"     "1,500.00"
///   "1 500,00 CDF" "1,500.00 USD"  "€ 1.500,00"
///   "(1500)"       → -1500.00  (accounting negative)
/// </summary>
public static class DecimalParsingHelper
{
    // Characters that may appear as thousand separators we should ignore
    // once we've identified the decimal separator.
    private static readonly char[] WhitespaceSeparators =
    {
        ' ',        // regular space
        '\u00A0',   // NO-BREAK SPACE (fr-FR thousand separator)
        '\u202F',   // NARROW NO-BREAK SPACE
        '\u2009',   // THIN SPACE
        '_'         // underscore (occasionally used by users)
    };

    // Currency / unit tokens we silently strip.
    private static readonly string[] CurrencyTokens =
    {
        "CDF", "USD", "EUR", "XAF", "XOF", "FC",
        "$", "€", "£", "¥", "FCFA"
    };

    /// <summary>
    /// Flexibly parses a user-entered amount.
    /// Returns true on success; <paramref name="result"/> is 0 on failure.
    /// </summary>
    public static bool TryParseFlexible(string? value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value)) return false;

        string s = value.Trim();

        // ── 1. Accounting negative: "(1500)" → "-1500" ──
        bool isNegative = false;
        if (s.Length >= 2 && s[0] == '(' && s[^1] == ')')
        {
            isNegative = true;
            s = s.Substring(1, s.Length - 2).Trim();
        }

        // ── 2. Strip currency tokens (case-insensitive) ──
        foreach (var token in CurrencyTokens)
        {
            // Remove token anywhere (prefix, suffix, or standalone)
            int idx;
            while ((idx = s.IndexOf(token, StringComparison.OrdinalIgnoreCase)) >= 0)
                s = s.Remove(idx, token.Length);
        }

        s = s.Trim();
        if (s.Length == 0) return false;

        // ── 3. Strip whitespace-like thousand separators ──
        foreach (var ws in WhitespaceSeparators)
            s = s.Replace(ws.ToString(), string.Empty);

        // ── 4. Detect leading sign ──
        if (s.StartsWith("-"))
        {
            isNegative = !isNegative;
            s = s.Substring(1);
        }
        else if (s.StartsWith("+"))
        {
            s = s.Substring(1);
        }

        if (s.Length == 0) return false;

        // ── 5. Normalize to invariant form ('.' = decimal, no thousands) ──
        bool hasDot = s.Contains('.');
        bool hasComma = s.Contains(',');

        if (hasComma && hasDot)
        {
            // Both present: the LAST one encountered is the decimal separator.
            int lastDot = s.LastIndexOf('.');
            int lastComma = s.LastIndexOf(',');

            if (lastComma > lastDot)
            {
                // European: "1.500,00" → dots are thousands, comma is decimal
                s = s.Replace(".", string.Empty).Replace(',', '.');
            }
            else
            {
                // US / Invariant: "1,500.00" → commas are thousands, dot is decimal
                s = s.Replace(",", string.Empty);
            }
        }
        else if (hasComma)
        {
            // Only comma: could be decimal ("3000,00") or thousands ("3,000").
            // Heuristic:
            //   • Multiple commas  → thousands separators  ("1,000,000")
            //   • Exactly one comma and the tail is exactly 3 digits with no
            //     other separators → treat as thousands separator ("3,000")
            //   • Otherwise → decimal separator ("3,5" or "3000,00")
            int commaCount = s.Length - s.Replace(",", string.Empty).Length;

            if (commaCount > 1)
            {
                s = s.Replace(",", string.Empty);
            }
            else
            {
                int commaIdx = s.IndexOf(',');
                string tail = s.Substring(commaIdx + 1);
                string head = s.Substring(0, commaIdx);

                bool tailIsThreeDigits = tail.Length == 3 && tail.All(char.IsDigit);
                bool headIsDigits = head.Length > 0 && head.All(char.IsDigit);

                if (tailIsThreeDigits && headIsDigits && head.Length <= 3)
                {
                    // "3,000" or "123,456" → thousands separator
                    s = s.Replace(",", string.Empty);
                }
                else
                {
                    // "3000,00" or "3,5" → decimal separator
                    s = s.Replace(',', '.');
                }
            }
        }
        else if (hasDot)
        {
            // Only dot(s): could be decimal ("3000.00") or European thousands
            // ("1.500"). Apply the same heuristic inverted.
            int dotCount = s.Length - s.Replace(".", string.Empty).Length;

            if (dotCount > 1)
            {
                // "1.000.000" → thousands
                s = s.Replace(".", string.Empty);
            }
            // single dot → already invariant decimal, leave as-is
        }

        // ── 6. Final parse under invariant culture ──
        if (!decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                              CultureInfo.InvariantCulture, out result))
        {
            result = 0m;
            return false;
        }

        if (isNegative) result = -result;
        return true;
    }

    /// <summary>Flexible parse; returns 0 on failure.</summary>
    public static decimal ParseOrZero(string? value)
        => TryParseFlexible(value, out var result) ? result : 0m;

    /// <summary>Flexible parse; returns <paramref name="fallback"/> on failure.</summary>
    public static decimal ParseOr(string? value, decimal fallback)
        => TryParseFlexible(value, out var result) ? result : fallback;

    /// <summary>Flexible parse; returns null on failure (useful for optional fields).</summary>
    public static decimal? ParseOrNull(string? value)
        => TryParseFlexible(value, out var result) ? result : null;
}