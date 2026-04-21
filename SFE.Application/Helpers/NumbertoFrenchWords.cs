namespace SFE.Application.Helpers;

/// <summary>
/// Converts a decimal amount to French words for DGI-compliant invoices.
/// Handles values up to 999 999 999 999 (trillions of CDF).
/// Follows Belgian/Congolese French rules (septante/nonante NOT used — standard French).
/// </summary>
public static class NumberToFrenchWords
{
    private static readonly string[] Units =
    {
        "", "un", "deux", "trois", "quatre", "cinq", "six", "sept",
        "huit", "neuf", "dix", "onze", "douze", "treize", "quatorze",
        "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf"
    };

    private static readonly string[] Tens =
    {
        "", "dix", "vingt", "trente", "quarante", "cinquante",
        "soixante", "soixante", "quatre-vingt", "quatre-vingt"
    };

    /// <summary>
    /// Converts a decimal amount to French words.
    /// Example: 15 420.50 → "quinze mille quatre cent vingt francs congolais et cinquante centimes"
    /// </summary>
    public static string Convert(decimal amount)
    {
        if (amount == 0)
            return "zéro franc congolais";

        bool negative = amount < 0;
        amount = Math.Abs(amount);

        long wholePart = (long)Math.Floor(amount);
        int centimes = (int)Math.Round((amount - wholePart) * 100);

        var parts = new List<string>();

        if (negative)
            parts.Add("moins");

        if (wholePart == 0)
        {
            parts.Add("zéro");
        }
        else
        {
            string wholeWords = ConvertWholeNumber(wholePart);
            parts.Add(wholeWords);
        }

        // Currency name
        if (wholePart <= 1)
            parts.Add("franc congolais");
        else
            parts.Add("francs congolais");

        // Centimes
        if (centimes > 0)
        {
            parts.Add("et");
            parts.Add(ConvertWholeNumber(centimes));
            if (centimes <= 1)
                parts.Add("centime");
            else
                parts.Add("centimes");
        }

        string result = string.Join(" ", parts);

        // Capitalize first letter
        return char.ToUpper(result[0]) + result[1..];
    }

    /// <summary>
    /// Converts a whole number (0–999 999 999 999) to French words.
    /// </summary>
    private static string ConvertWholeNumber(long n)
    {
        if (n == 0) return "zéro";
        if (n < 0) return "moins " + ConvertWholeNumber(-n);

        var parts = new List<string>();

        // Milliards (billions)
        if (n >= 1_000_000_000)
        {
            long milliards = n / 1_000_000_000;
            if (milliards == 1)
                parts.Add("un milliard");
            else
                parts.Add(ConvertBelow1000(milliards) + " milliards");
            n %= 1_000_000_000;
        }

        // Millions
        if (n >= 1_000_000)
        {
            long millions = n / 1_000_000;
            if (millions == 1)
                parts.Add("un million");
            else
                parts.Add(ConvertBelow1000(millions) + " millions");
            n %= 1_000_000;
        }

        // Milliers (thousands)
        if (n >= 1_000)
        {
            long milliers = n / 1_000;
            if (milliers == 1)
                parts.Add("mille");  // Never "un mille"
            else
                parts.Add(ConvertBelow1000(milliers) + " mille");
            n %= 1_000;
        }

        // Remainder < 1000
        if (n > 0)
        {
            parts.Add(ConvertBelow1000(n));
        }

        return string.Join(" ", parts).Trim();
    }

    /// <summary>
    /// Converts a number 1–999 to French words.
    /// </summary>
    private static string ConvertBelow1000(long n)
    {
        if (n <= 0) return "";
        if (n >= 1000) throw new ArgumentOutOfRangeException(nameof(n));

        var parts = new List<string>();

        // Hundreds
        if (n >= 100)
        {
            long hundreds = n / 100;
            long remainder = n % 100;

            if (hundreds == 1)
            {
                parts.Add("cent");
            }
            else
            {
                parts.Add(Units[hundreds]);
                // "cents" with 's' only when exactly multiple of 100
                if (remainder == 0)
                    parts.Add("cents");
                else
                    parts.Add("cent");
            }

            n = remainder;
        }

        // Below 100
        if (n > 0)
        {
            parts.Add(ConvertBelow100(n));
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Converts a number 1–99 to French words.
    /// Handles French special cases: 70–79, 80–99.
    /// </summary>
    private static string ConvertBelow100(long n)
    {
        if (n <= 0) return "";
        if (n < 20) return Units[n];

        int tensDigit = (int)(n / 10);
        int unitDigit = (int)(n % 10);

        // Special French rules
        switch (tensDigit)
        {
            case 7: // 70–79 = soixante-dix, soixante-et-onze, ... soixante-dix-neuf
                {
                    int subNumber = 60 + (int)(n - 60); // remap to 60 + (10..19)
                    if (n == 70)
                        return "soixante-dix";
                    if (n == 71)
                        return "soixante-et-onze";
                    // 72–79: soixante-douze ... soixante-dix-neuf
                    return "soixante-" + Units[(int)(n - 60)];
                }

            case 8: // 80–89 = quatre-vingts, quatre-vingt-un, ...
                {
                    if (n == 80)
                        return "quatre-vingts"; // With 's' only when alone
                    return "quatre-vingt-" + Units[unitDigit];
                }

            case 9: // 90–99 = quatre-vingt-dix, quatre-vingt-onze, ...
                {
                    if (n == 90)
                        return "quatre-vingt-dix";
                    if (n == 91)
                        return "quatre-vingt-onze";
                    return "quatre-vingt-" + Units[(int)(n - 80)];
                }

            default: // 20–69 (standard)
                {
                    string tensWord = Tens[tensDigit];
                    if (unitDigit == 0)
                        return tensWord;
                    if (unitDigit == 1)
                        return tensWord + "-et-un"; // vingt-et-un, trente-et-un, etc.
                    return tensWord + "-" + Units[unitDigit];
                }
        }
    }
}