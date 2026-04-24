using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Moteur de calcul fiscal conforme DGI RDC — V10.
///
/// V10 vs V6–V9 :
///   TS Percentage uses Ceil2 (arrondi par excès) at the SOURCE
///   inside CalculateLineFull, matching the VSDC exactly.
///   All fragile post-fix passes are eliminated.
///
///   Formula (TS Percentage):
///     TS  = Ceil2(goodsHT × tsRate)
///     TTC = Ceil2((goodsHT + TS) × (1 + vatRate))   ← two-step
///     HT  = R2(TTC / (1 + vatRate))                  ← reverse
///     TVA = TTC − HT
///
///   Formula (TS FixedPerUnit) — unchanged:
///     TS  = R2(value × qty)
///     HT  = goodsHT + TS
///     TTC = R2(HT × (1 + vatRate))
///     TVA = TTC − HT
///
/// ⚠ AmountHT includes TS (HT fiscal = goodsHT + TS).
///   AmountHTCommercial = AmountHT − TaxSpecificAmount.
/// </summary>
public static class TaxCalculator
{
    // ═══════════════════════════════════════════════════
    //  ARRONDI
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Standard round to 2 decimals, AwayFromZero — matches WinDev Arrondi().
    /// </summary>
    public static decimal R2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Ceiling to 2 decimals — matches VSDC "arrondi par excès"
    /// used for TS percentage and TTC when TS% is present.
    /// </summary>
    public static decimal Ceil2(decimal v) =>
        Math.Ceiling(v * 100m) / 100m;

    // ═══════════════════════════════════════════════════
    //  TAUX ET LABELS
    // ═══════════════════════════════════════════════════

    public static decimal GetDefaultRate(TaxGroup group) => group switch
    {
        TaxGroup.A => 0m,
        TaxGroup.B => 16m,
        TaxGroup.C => 5m,
        TaxGroup.D => 0m,
        TaxGroup.E => 0m,
        TaxGroup.F => 16m,
        TaxGroup.G => 5m,
        TaxGroup.H => 0m,
        TaxGroup.I => 0m,
        TaxGroup.J => 0m,
        TaxGroup.K => 0m,
        TaxGroup.L => 0m,
        TaxGroup.M => 0m,
        TaxGroup.N => 0m,
        TaxGroup.O => 1m,
        TaxGroup.P => 1m,
        _ => 0m
    };

    public static string GetGroupLabel(TaxGroup group) => group switch
    {
        TaxGroup.A => "Exonéré/Hors champ",
        TaxGroup.B => "Taxable 16%",
        TaxGroup.C => "Taxable 5%",
        TaxGroup.D => "Régimes dérogatoires TVA",
        TaxGroup.E => "Exportation",
        TaxGroup.F => "TVA marché pub. ext. 16%",
        TaxGroup.G => "TVA marché pub. ext. 5%",
        TaxGroup.H => "Consignation",
        TaxGroup.I => "Garantie et caution",
        TaxGroup.J => "Débours",
        TaxGroup.K => "Non assujettis",
        TaxGroup.L => "Prélèvements sur ventes",
        TaxGroup.M => "Ventes réglementées",
        TaxGroup.N => "TVA spécifique",
        TaxGroup.O => "Taxable 1%",
        TaxGroup.P => "TVA marché pub. ext. 1%",
        _ => "Inconnu"
    };

    public static string GetPriceModeLabel(PriceMode mode) =>
        mode == PriceMode.TTC ? "TTC" : "HT";

    // ═══════════════════════════════════════════════════
    //  VALIDATION — type d'article vs groupe de taxation
    // ═══════════════════════════════════════════════════

    public static bool IsItemTypeValidForGroup(ItemType itemType, TaxGroup group)
    {
        bool isLOrN = group == TaxGroup.L || group == TaxGroup.N;
        return itemType switch
        {
            ItemType.TAX => isLOrN,
            _ => !isLOrN
        };
    }

    // ═══════════════════════════════════════════════════
    //  CALCUL PRINCIPAL — Ligne complète (V10)
    // ═══════════════════════════════════════════════════

    public static LineCalculationResult CalculateLineFull(LineCalculationInput input)
    {
        var result = new LineCalculationResult();

        // ── Groupe N : montant entier = TVA (spec 1.5.8) ──
        if (input.TaxGroup == TaxGroup.N)
        {
            decimal unitPrice = input.PriceMode == PriceMode.TTC
                ? input.UnitPriceTTC
                : input.UnitPriceHT;
            decimal amount = R2(unitPrice * input.Quantity);
            result.AmountHTBeforeDiscount = 0m;
            result.DiscountAmount = 0m;
            result.AmountHT = 0m;
            result.AmountTVA = amount;
            result.TaxSpecificAmount = 0m;
            result.AmountTTC = amount;
            return result;
        }

        bool isTTC = input.PriceMode == PriceMode.TTC;
        decimal rate = input.TaxRate / 100m;

        // ── grossAmount = R2(PU × Qty) ──
        decimal unitPriceUsed = isTTC ? input.UnitPriceTTC : input.UnitPriceHT;
        decimal grossAmount = R2(unitPriceUsed * input.Quantity);

        // AmountHTBeforeDiscount : HT commercial pur (sans TS, sans remise)
        result.AmountHTBeforeDiscount = isTTC && rate > 0m
            ? R2(grossAmount / (1m + rate))
            : grossAmount;

        // ── TS en ligne ? (désactivée si OnTotal) ──
        bool hasTS = input.SpecificTaxType != SpecificTaxType.None
                     && input.SpecificTaxValue > 0m
                     && input.TaxApplicationMode != TaxApplicationMode.OnTotal;

        if (input.DiscountType == DiscountType.None || input.DiscountBeforeTax)
        {
            CalculateDiscountBeforeTax(input, result, grossAmount, rate, isTTC, hasTS);
        }
        else
        {
            CalculateDiscountAfterTax(input, result, grossAmount, rate, isTTC, hasTS);
        }

        return result;
    }

    // ─────────────────────────────────────────────────
    //  Branche 1 : pas de remise ou remise avant taxe
    // ─────────────────────────────────────────────────
    private static void CalculateDiscountBeforeTax(
        LineCalculationInput input, LineCalculationResult result,
        decimal grossAmount, decimal rate, bool isTTC, bool hasTS)
    {
        // ── 1. Remise sur montant brut ──
        result.DiscountAmount = CalculateDiscountAmount(
            grossAmount, input.DiscountType, input.DiscountValue);
        decimal netAmount = grossAmount - result.DiscountAmount;

        // ── 2. Extraire le HT marchandise ──
        decimal goodsHT = isTTC && rate > 0m
            ? R2(netAmount / (1m + rate))
            : netAmount;

        // ── 3. Taxe spécifique — V10: Ceil2 pour pourcentage ──
        result.TaxSpecificAmount = hasTS
            ? ComputeSpecificTax(input.SpecificTaxType, input.SpecificTaxValue,
                                 goodsHT, input.Quantity)
            : 0m;

        // ── 4. Calcul HT / TVA / TTC ──
        if (hasTS && result.TaxSpecificAmount > 0m)
        {
            if (input.SpecificTaxType == SpecificTaxType.Percentage)
            {
                // ── V10: Two-step Ceil2 forward + reverse ──
                // TS  = Ceil2(goodsHT × tsRate)  [already done above]
                // TTC = Ceil2((goodsHT + TS) × (1+vat))
                // HT  = R2(TTC / (1+vat))
                // TVA = TTC − HT
                decimal ts = result.TaxSpecificAmount;
                decimal baseHT = goodsHT + ts;
                result.AmountTTC = Ceil2(baseHT * (1m + rate));
                result.AmountHT = R2(result.AmountTTC / (1m + rate));
                result.AmountTVA = result.AmountTTC - result.AmountHT;
            }
            else
            {
                // ── FixedPerUnit: standard R2 ──
                result.AmountHT = goodsHT + result.TaxSpecificAmount;
                result.AmountTTC = R2(result.AmountHT * (1m + rate));
                result.AmountTVA = result.AmountTTC - result.AmountHT;
            }
        }
        else if (isTTC)
        {
            // ── Sans TS, mode TTC : préserver le TTC utilisateur ──
            result.AmountTTC = netAmount;
            result.AmountHT = goodsHT;
            result.AmountTVA = result.AmountTTC - result.AmountHT;
        }
        else
        {
            // ── Sans TS, mode HT ──
            result.AmountHT = netAmount;
            result.AmountTTC = R2(result.AmountHT * (1m + rate));
            result.AmountTVA = result.AmountTTC - result.AmountHT;
        }

        // ── Garde arrondi DGI (spec 1.5.7) ──
        if (result.AmountHT + result.AmountTVA != result.AmountTTC)
        {
            result.AmountTVA = result.AmountTTC - result.AmountHT;
        }
    }

    // ─────────────────────────────────────────────────
    //  Branche 2 : remise après taxe
    // ─────────────────────────────────────────────────
    private static void CalculateDiscountAfterTax(
        LineCalculationInput input, LineCalculationResult result,
        decimal grossAmount, decimal rate, bool isTTC, bool hasTS)
    {
        // ── 1. Extraire HT marchandise ──
        decimal goodsHT = isTTC && rate > 0m
            ? R2(grossAmount / (1m + rate))
            : grossAmount;

        // ── 2. TS — V10: Ceil2 pour pourcentage ──
        result.TaxSpecificAmount = hasTS
            ? ComputeSpecificTax(input.SpecificTaxType, input.SpecificTaxValue,
                                 goodsHT, input.Quantity)
            : 0m;

        // ── 3. Calcul complet sur brut (non remisé) ──
        decimal grossTTC;

        if (hasTS && result.TaxSpecificAmount > 0m)
        {
            if (input.SpecificTaxType == SpecificTaxType.Percentage)
            {
                // ── V10: Two-step Ceil2 forward + reverse ──
                decimal ts = result.TaxSpecificAmount;
                decimal baseHT = goodsHT + ts;
                grossTTC = Ceil2(baseHT * (1m + rate));
                result.AmountHT = R2(grossTTC / (1m + rate));
                result.AmountTVA = grossTTC - result.AmountHT;
            }
            else
            {
                result.AmountHT = goodsHT + result.TaxSpecificAmount;
                grossTTC = R2(result.AmountHT * (1m + rate));
                result.AmountTVA = grossTTC - result.AmountHT;
            }
        }
        else if (isTTC)
        {
            grossTTC = grossAmount;
            result.AmountHT = goodsHT;
            result.AmountTVA = grossTTC - result.AmountHT;
        }
        else
        {
            result.AmountHT = grossAmount;
            grossTTC = R2(result.AmountHT * (1m + rate));
            result.AmountTVA = grossTTC - result.AmountHT;
        }

        // Garde arrondi DGI sur le brut
        if (result.AmountHT + result.AmountTVA != grossTTC)
        {
            result.AmountTVA = grossTTC - result.AmountHT;
            grossTTC = result.AmountHT + result.AmountTVA;
        }

        // ── 4. Remise sur TTC brut ──
        result.DiscountAmount = CalculateDiscountAmount(
            grossTTC, input.DiscountType, input.DiscountValue);

        // ── 5. TTC final ──
        result.AmountTTC = grossTTC - result.DiscountAmount;
    }

    // ═══════════════════════════════════════════════════
    //  TAXE SPÉCIFIQUE — Calcul typé (V10: Ceil2 pour %)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Calcule la TS.
    /// V10: Percentage uses Ceil2 (arrondi par excès) to match VSDC.
    /// </summary>
    public static decimal ComputeSpecificTax(
        SpecificTaxType type, decimal value, decimal baseAmount, decimal quantity)
    {
        if (value <= 0m)
            return 0m;

        return type switch
        {
            SpecificTaxType.Percentage => Ceil2(baseAmount * value / 100m),
            SpecificTaxType.FixedPerUnit => R2(value * quantity),
            _ => 0m
        };
    }

    // ═══════════════════════════════════════════════════
    //  TAXE SPÉCIFIQUE — OnTotal (niveau facture)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// TS au niveau facture (OnTotal).
    /// V10: Percentage uses Ceil2 for consistency.
    /// Note: OnTotal Percentage path in RecalculateTotals computes per-line,
    ///       so this is mainly used for FixedPerUnit.
    /// </summary>
    public static decimal ComputeOnTotalSpecificTax(
        SpecificTaxType type, decimal value, decimal groupHT, decimal groupQuantity)
    {
        if (value <= 0m)
            return 0m;

        return type switch
        {
            SpecificTaxType.Percentage => Ceil2(groupHT * value / 100m),
            SpecificTaxType.FixedPerUnit => R2(value * groupQuantity),
            _ => 0m
        };
    }

    // ═══════════════════════════════════════════════════
    //  HELPERS PUBLICS
    // ═══════════════════════════════════════════════════

    public static decimal CalculateDiscountAmount(
        decimal baseAmount, DiscountType type, decimal value)
    {
        if (baseAmount <= 0m || value <= 0m)
            return 0m;

        return type switch
        {
            DiscountType.Percentage => R2(baseAmount * Math.Min(value, 100m) / 100m),
            DiscountType.FixedAmount => R2(Math.Min(value, baseAmount)),
            _ => 0m
        };
    }

    public static (decimal ht, decimal ttc) EnsureDualPrices(
        decimal inputPrice, PriceMode mode, decimal taxRate)
    {
        decimal rate = taxRate / 100m;

        if (mode == PriceMode.TTC)
        {
            decimal ttc = inputPrice;
            decimal ht = rate > 0m ? R2(ttc / (1m + rate)) : ttc;
            return (ht, ttc);
        }
        else
        {
            decimal ht = inputPrice;
            decimal ttc = R2(ht * (1m + rate));
            return (ht, ttc);
        }
    }

    public static (decimal ht, decimal ttc) EnsureDualPrices(
        decimal inputPrice, PriceMode mode, decimal taxRate,
        SpecificTaxType specificTaxType, decimal specificTaxValue)
    {
        return EnsureDualPrices(inputPrice, mode, taxRate);
    }

    // ═══════════════════════════════════════════════════
    //  RÉTROCOMPATIBILITÉ
    // ═══════════════════════════════════════════════════

    public static (SpecificTaxType type, decimal value) ParseLegacySpecificTax(
        string? taxSpecificValue)
    {
        if (string.IsNullOrWhiteSpace(taxSpecificValue))
            return (SpecificTaxType.None, 0m);

        var trimmed = taxSpecificValue.Trim();

        if (trimmed.EndsWith('%'))
        {
            if (decimal.TryParse(
                    trimmed.TrimEnd('%'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var pct) && pct > 0)
            {
                return (SpecificTaxType.Percentage, pct);
            }
        }
        else if (decimal.TryParse(
                     trimmed,
                     System.Globalization.NumberStyles.Any,
                     System.Globalization.CultureInfo.InvariantCulture,
                     out var perUnit) && perUnit > 0)
        {
            return (SpecificTaxType.FixedPerUnit, perUnit);
        }

        return (SpecificTaxType.None, 0m);
    }
}

// ═══════════════════════════════════════════════════
//  MODÈLES I/O
// ═══════════════════════════════════════════════════

public class LineCalculationInput
{
    public decimal UnitPriceHT { get; set; }
    public decimal UnitPriceTTC { get; set; }
    public decimal Quantity { get; set; } = 1;
    public TaxGroup TaxGroup { get; set; } = TaxGroup.B;
    public decimal TaxRate { get; set; }
    public PriceMode PriceMode { get; set; } = PriceMode.TTC;

    public DiscountType DiscountType { get; set; } = DiscountType.None;
    public decimal DiscountValue { get; set; }
    public bool DiscountBeforeTax { get; set; } = true;

    public SpecificTaxType SpecificTaxType { get; set; } = SpecificTaxType.None;
    public decimal SpecificTaxValue { get; set; }
    public TaxApplicationMode TaxApplicationMode { get; set; } = TaxApplicationMode.PerArticle;

    // Compat legacy
    public bool HasSpecificTax { get; set; }
    public decimal SpecificTaxRate { get; set; }
    public string TaxSpecificValue { get; set; } = "";
}

public class LineCalculationResult
{
    public decimal AmountHTBeforeDiscount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountHT { get; set; }
    public decimal AmountTVA { get; set; }
    public decimal TaxSpecificAmount { get; set; }
    public decimal AmountTTC { get; set; }
    public decimal AmountHTCommercial => AmountHT - TaxSpecificAmount;
}