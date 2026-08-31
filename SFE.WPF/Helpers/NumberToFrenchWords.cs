namespace SFE.WPF.Helpers;

public static class NumberToFrenchWords
{
    private static readonly string[] Units =
    {
        "", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf",
        "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize",
        "dix-sept", "dix-huit", "dix-neuf"
    };

    private static readonly string[] TensNames =
    {
    "", "", "vingt", "trente", "quarante", "cinquante", "soixante",
    "septante", "", "nonante"
    };

    public static string Convert(decimal amount, string currency = "francs congolais toutes taxes comprises")
    {
        long whole = (long)Math.Floor(Math.Abs(amount));
        int cents = (int)Math.Round((Math.Abs(amount) - whole) * 100);

        string result = whole == 0 ? "zéro" : ConvertWholeNumber(whole);

        if (cents > 0)
            result += $" virgule {ConvertWholeNumber(cents)}";

        result += $" {currency}";

        // Capitalize first letter
        return char.ToUpper(result[0]) + result[1..];
    }

    private static string ConvertWholeNumber(long n)
    {
        if (n == 0) return "zéro";
        if (n < 0) return "moins " + ConvertWholeNumber(-n);

        var parts = new List<string>();

        if (n >= 1_000_000_000)
        {
            long milliards = n / 1_000_000_000;
            parts.Add(milliards == 1 ? "un milliard" : $"{ConvertUnder1000(milliards)} milliards");
            n %= 1_000_000_000;
        }

        if (n >= 1_000_000)
        {
            long millions = n / 1_000_000;
            parts.Add(millions == 1 ? "un million" : $"{ConvertUnder1000(millions)} millions");
            n %= 1_000_000;
        }

        if (n >= 1000)
        {
            long thousands = n / 1000;
            parts.Add(thousands == 1 ? "mille" : $"{ConvertUnder1000(thousands)} mille");
            n %= 1000;
        }

        if (n > 0)
            parts.Add(ConvertUnder1000(n));

        return string.Join(" ", parts);
    }

    private static string ConvertUnder1000(long n)
    {
        if (n >= 100)
        {
            long hundreds = n / 100;
            long remainder = n % 100;

            string hPart;
            if (hundreds == 1)
                hPart = "cent";
            else if (remainder == 0)
                hPart = $"{ConvertUnder100(hundreds)} cents";
            else
                hPart = $"{ConvertUnder100(hundreds)} cent";

            return remainder > 0 ? $"{hPart} {ConvertUnder100(remainder)}" : hPart;
        }

        return ConvertUnder100(n);
    }

    private static string ConvertUnder100(long n)
    {
        if (n < 20) return Units[n];

        // 80-99 : quatre-vingt(s) — unchanged in Belgian French
        if (n >= 80 && n < 90)
        {
            if (n == 80) return "quatre-vingts";
            return $"quatre-vingt-{Units[n - 80]}";
        }

        // 20-79 and 90-99 : all regular, incl. septante / nonante
        long ten = n / 10;
        long unit = n % 10;

        if (unit == 0) return TensNames[ten];
        if (unit == 1) return $"{TensNames[ten]} et un";
        return $"{TensNames[ten]}-{Units[unit]}";
    }
}