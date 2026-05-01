using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Moteur de calcul fiscal conforme DGI RDC — V13.
///
/// V13 vs V12 :
///   Fixed TTC mode decomposition to match WinDev GetCumul() exactly:
///     - In TTC mode, the entered price ALREADY contains TVA on goods.
///     - TS is computed on the entered TTC price (not extracted HT).
///     - TS gets its own TVA ADDED ON TOP (not included in the TTC envelope).
///     - TVA is decomposed as two components: TVA_goods (extraction) + TVA_TS (addition).
///
///   Formula (TS Percentage, TTC mode, no discount):
///     TS        = R2(PU_TTC × QTY × tsRate / 100)
///     goodsHT   = R2(PU_TTC × QTY / (1 + vatRate))
///     TVA_goods = PU_TTC × QTY − goodsHT
///     TVA_TS    = R2(TS × vatRate)
///     AmountHT  = goodsHT + TS
///     AmountTVA = TVA_goods + TVA_TS
///     AmountTTC = AmountHT + AmountTVA  (= PU_TTC×QTY + TS + TVA_TS)
///
///   Formula (TS FixedPerUnit, TTC mode, no discount):
///     TS        = R2(fixedValue × QTY)
///     goodsHT   = R2(PU_TTC × QTY / (1 + vatRate))
///     TVA_goods = PU_TTC × QTY − goodsHT
///     TVA_TS    = R2(TS × vatRate)
///     AmountHT  = goodsHT + TS
///     AmountTVA = TVA_goods + TVA_TS
///     AmountTTC = AmountHT + AmountTVA
///
///   Formula (TS Percentage, HT mode, no discount):
///     TS        = R2(PU_HT × QTY × tsRate / 100)
///     AmountHT  = PU_HT × QTY + TS
///     AmountTVA = R2(AmountHT × vatRate)   ← single TVA on total HT
///     AmountTTC = AmountHT + AmountTVA
///
/// ⚠ AmountHT is the fiscal HT (includes TS contribution).
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
    /// Ceiling to 2 decimals — kept for utility/OnTotal edge cases.
    /// No longer used in PerArticle line calculation (V13).
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
    //  CALCUL PRINCIPAL — Ligne complète (V13)
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

        // AmountHTBeforeDiscount : HT of goods only (no TS, no discount)
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
    //  V13: TTC mode — TS is an add-on with separate TVA
    // ─────────────────────────────────────────────────
    private static void CalculateDiscountBeforeTax(
        LineCalculationInput input, LineCalculationResult result,
        decimal grossAmount, decimal rate, bool isTTC, bool hasTS)
    {
        // ── 1. Remise sur montant brut ──
        result.DiscountAmount = CalculateDiscountAmount(
            grossAmount, input.DiscountType, input.DiscountValue);
        decimal netAmount = grossAmount - result.DiscountAmount;

        // ── 2. Calculer TS (base = netAmount = prix après remise) ──
        decimal ts = 0m;
        if (hasTS)
        {
            if (input.SpecificTaxType == SpecificTaxType.Percentage)
            {
                // V13: TS% on entered/net price (TTC or HT) — matches WinDev
                ts = R2(netAmount * input.SpecificTaxValue / 100m);
            }
            else // FixedPerUnit
            {
                ts = R2(input.SpecificTaxValue * input.Quantity);
            }
            result.TaxSpecificAmount = ts;
        }
        else
        {
            result.TaxSpecificAmount = 0m;
        }

        // ── 3. Décomposition HT / TVA / TTC ──
        if (isTTC)
        {
            // ═══════════════════════════════════════════════════
            //  TTC MODE (V13 — WinDev aligned):
            //  netAmount is goods_TTC (already includes TVA on goods).
            //  TS is an ADD-ON that gets its own TVA on top.
            //
            //  goodsHT   = R2(netAmount / (1 + rate))
            //  TVA_goods = netAmount − goodsHT
            //  TVA_TS    = R2(TS × rate)
            //  AmountHT  = goodsHT + TS
            //  AmountTVA = TVA_goods + TVA_TS
            //  AmountTTC = AmountHT + AmountTVA
            // ═══════════════════════════════════════════════════
            decimal goodsHT = rate > 0m ? R2(netAmount / (1m + rate)) : netAmount;
            decimal tvaGoods = netAmount - goodsHT;

            if (hasTS && ts > 0m)
            {
                decimal tvaTS = R2(ts * rate);
                result.AmountHT = goodsHT + ts;
                result.AmountTVA = tvaGoods + tvaTS;
                result.AmountTTC = result.AmountHT + result.AmountTVA;
            }
            else
            {
                // No TS: standard extraction
                result.AmountTTC = netAmount;
                result.AmountHT = goodsHT;
                result.AmountTVA = tvaGoods;
            }
        }
        else
        {
            // ═══════════════════════════════════════════════════
            //  HT MODE:
            //  netAmount is goods_HT. TS added to HT base.
            //  TVA computed on total HT (goods + TS).
            //
            //  AmountHT  = netAmount + TS
            //  AmountTVA = R2(AmountHT × rate)
            //  AmountTTC = AmountHT + AmountTVA
            // ═══════════════════════════════════════════════════
            result.AmountHT = netAmount + ts;
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
    //  V13: TTC mode — TS is an add-on with separate TVA
    // ─────────────────────────────────────────────────
    private static void CalculateDiscountAfterTax(
        LineCalculationInput input, LineCalculationResult result,
        decimal grossAmount, decimal rate, bool isTTC, bool hasTS)
    {
        // ── 1. Calculer TS sur grossAmount (prix complet avant remise) ──
        decimal ts = 0m;
        if (hasTS)
        {
            if (input.SpecificTaxType == SpecificTaxType.Percentage)
            {
                ts = R2(grossAmount * input.SpecificTaxValue / 100m);
            }
            else // FixedPerUnit
            {
                ts = R2(input.SpecificTaxValue * input.Quantity);
            }
            result.TaxSpecificAmount = ts;
        }
        else
        {
            result.TaxSpecificAmount = 0m;
        }

        // ── 2. Décomposer le montant brut (avant remise) ──
        decimal grossTTC;
        if (isTTC)
        {
            // TTC mode: grossAmount is goods_TTC, TS gets own TVA
            decimal goodsHT = rate > 0m ? R2(grossAmount / (1m + rate)) : grossAmount;
            decimal tvaGoods = grossAmount - goodsHT;

            if (hasTS && ts > 0m)
            {
                decimal tvaTS = R2(ts * rate);
                result.AmountHT = goodsHT + ts;
                result.AmountTVA = tvaGoods + tvaTS;
                grossTTC = result.AmountHT + result.AmountTVA;
            }
            else
            {
                result.AmountHT = goodsHT;
                result.AmountTVA = tvaGoods;
                grossTTC = grossAmount;
            }
        }
        else
        {
            // HT mode: TVA on total HT
            result.AmountHT = grossAmount + ts;
            grossTTC = R2(result.AmountHT * (1m + rate));
            result.AmountTVA = grossTTC - result.AmountHT;
        }

        // Garde arrondi DGI sur le brut
        if (result.AmountHT + result.AmountTVA != grossTTC)
        {
            result.AmountTVA = grossTTC - result.AmountHT;
        }

        // ── 3. Remise sur TTC brut ──
        result.DiscountAmount = CalculateDiscountAmount(
            grossTTC, input.DiscountType, input.DiscountValue);

        // ── 4. TTC final (HT et TVA inchangés — remise commerciale post-taxe) ──
        result.AmountTTC = grossTTC - result.DiscountAmount;
    }

    // ═══════════════════════════════════════════════════
    //  TAXE SPÉCIFIQUE — Calcul typé (V13: R2 partout)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Calcule la TS.
    /// V13: Uses R2 (standard rounding) — matches WinDev Arrondi().
    /// </summary>
    public static decimal ComputeSpecificTax(
        SpecificTaxType type, decimal value, decimal baseAmount, decimal quantity)
    {
        if (value <= 0m)
            return 0m;

        return type switch
        {
            SpecificTaxType.Percentage => R2(baseAmount * value / 100m),
            SpecificTaxType.FixedPerUnit => R2(value * quantity),
            _ => 0m
        };
    }

    // ═══════════════════════════════════════════════════
    //  TAXE SPÉCIFIQUE — OnTotal (niveau facture)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// TS au niveau facture (OnTotal).
    /// V13: For FixedPerUnit, base is quantity. For Percentage, base is passed amount.
    /// </summary>
    public static decimal ComputeOnTotalSpecificTax(
        SpecificTaxType type, decimal value, decimal baseAmount, decimal groupQuantity)
    {
        if (value <= 0m)
            return 0m;

        return type switch
        {
            SpecificTaxType.Percentage => R2(baseAmount * value / 100m),
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