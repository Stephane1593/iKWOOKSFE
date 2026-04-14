using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Moteur de calcul fiscal conforme DGI RDC — V3.
///
/// Chaîne de calcul (DiscountBeforeTax = true, défaut) :
///   HT brut → Remise → HT net → T.S.(PerArticle) → TVA → TTC
///
/// Chaîne alternative (DiscountBeforeTax = false) :
///   HT brut → T.S. → TVA → TTC brut → Remise → TTC net
///   (HT et TVA fiscaux restent non remisés)
///
/// Groupe N : montant entier considéré comme TVA.
/// Arrondi : 2 décimales, TVA arrondie vers le haut si nécessaire (DGI).
///
/// Taxe spécifique :
///   - Percentage   : montant = baseHT × value / 100
///   - FixedPerUnit : montant = value × quantity
/// </summary>
public static class TaxCalculator
{
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
    //  CALCUL PRINCIPAL — Ligne complète
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Calcul complet d'une ligne de facture :
    /// remise, taxe spécifique, TVA, TTC.
    ///
    /// Quand TaxApplicationMode == OnTotal la T.S. n'est PAS incluse
    /// dans le résultat de la ligne (TaxSpecificAmount = 0).
    /// Elle sera calculée et distribuée au niveau facture
    /// par InvoicingViewModel.RecalculateTotals().
    /// </summary>
    public static LineCalculationResult CalculateLineFull(LineCalculationInput input)
    {
        var result = new LineCalculationResult();

        // ── Groupe N : montant entier = TVA ──
        if (input.TaxGroup == TaxGroup.N)
        {
            var amount = Math.Round(input.UnitPriceHT * input.Quantity, 2);
            result.AmountHTBeforeDiscount = 0;
            result.DiscountAmount = 0;
            result.AmountHT = 0;
            result.AmountTVA = amount;
            result.TaxSpecificAmount = 0;
            result.AmountTTC = amount;
            return result;
        }

        decimal rate = input.TaxRate / 100m;
        decimal grossHT = Math.Round(input.UnitPriceHT * input.Quantity, 2);
        result.AmountHTBeforeDiscount = grossHT;

        // ── T.S. par ligne sauf si le mode est explicitement OnTotal
        //    (auquel cas elle sera calculée au niveau facture). ──
        bool hasTS = input.SpecificTaxType != SpecificTaxType.None
                     && input.SpecificTaxValue > 0
                     && input.TaxApplicationMode != TaxApplicationMode.OnTotal;

        if (input.DiscountType == DiscountType.None || input.DiscountBeforeTax)
        {
            // ═══ REMISE AVANT TAXE (ou pas de remise) ═══

            // 1. Remise sur montant HT brut
            result.DiscountAmount = CalculateDiscountAmount(
                grossHT, input.DiscountType, input.DiscountValue);

            // 2. HT net (après remise)
            result.AmountHT = grossHT - result.DiscountAmount;

            // 3. Taxe spécifique PerArticle → base = HT net
            result.TaxSpecificAmount = 0m;
            if (hasTS)
            {
                result.TaxSpecificAmount = ComputeSpecificTax(
                    input.SpecificTaxType, input.SpecificTaxValue,
                    result.AmountHT, input.Quantity);
            }

            // 4. TVA = (HT net + T.S.) × taux
            decimal baseTVA = result.AmountHT + result.TaxSpecificAmount;
            result.AmountTVA = Math.Round(baseTVA * rate, 2);

            // 5. TTC = HT net + T.S. + TVA
            result.AmountTTC = result.AmountHT + result.TaxSpecificAmount + result.AmountTVA;
        }
        else
        {
            // ═══ REMISE APRÈS TAXE ═══

            // 1. HT fiscal = brut (non remisé)
            result.AmountHT = grossHT;

            // 2. Taxe spécifique sur HT brut
            result.TaxSpecificAmount = 0m;
            if (hasTS)
            {
                result.TaxSpecificAmount = ComputeSpecificTax(
                    input.SpecificTaxType, input.SpecificTaxValue,
                    grossHT, input.Quantity);
            }

            // 3. TVA sur (HT brut + T.S.)
            decimal baseTVA = grossHT + result.TaxSpecificAmount;
            result.AmountTVA = Math.Round(baseTVA * rate, 2);

            // 4. TTC brut (avant remise commerciale)
            decimal grossTTC = grossHT + result.TaxSpecificAmount + result.AmountTVA;

            // 5. Remise calculée sur TTC brut
            result.DiscountAmount = CalculateDiscountAmount(
                grossTTC, input.DiscountType, input.DiscountValue);

            // 6. TTC final
            result.AmountTTC = grossTTC - result.DiscountAmount;
        }

        // ── Vérification arrondi DGI (mode TTC, sans remise, sans T.S.) ──
        bool noTS = input.SpecificTaxType == SpecificTaxType.None
                    || input.SpecificTaxValue <= 0;

        if (input.PriceMode == PriceMode.TTC
            && input.DiscountType == DiscountType.None
            && noTS
            && rate > 0)
        {
            decimal expectedTTC = Math.Round(input.UnitPriceTTC * input.Quantity, 2);
            if (result.AmountHT + result.AmountTVA != expectedTTC)
            {
                result.AmountTVA = expectedTTC - result.AmountHT;
                result.AmountTTC = expectedTTC;
            }
        }

        return result;
    }

    // ═══════════════════════════════════════════════════
    //  TAXE SPÉCIFIQUE — Calcul typé (PerArticle)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Calcule le montant de la taxe spécifique pour une ligne (PerArticle).
    /// </summary>
    /// <param name="type">Percentage ou FixedPerUnit</param>
    /// <param name="value">Taux (ex: 10 pour 10 %) ou montant fixe par unité</param>
    /// <param name="amountHT">Base HT de la ligne</param>
    /// <param name="quantity">Quantité</param>
    public static decimal ComputeSpecificTax(
        SpecificTaxType type, decimal value, decimal amountHT, decimal quantity)
    {
        if (value <= 0m)
            return 0m;

        return type switch
        {
            SpecificTaxType.Percentage =>
                Math.Round(amountHT * value / 100m, 2),

            SpecificTaxType.FixedPerUnit =>
                Math.Round(value * quantity, 2),

            _ => 0m
        };
    }

    // ═══════════════════════════════════════════════════
    //  TAXE SPÉCIFIQUE — Calcul OnTotal (niveau facture)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Calcule la taxe spécifique en mode OnTotal (sur le sous-total groupé).
    ///   - Percentage   : groupHT × taux / 100
    ///   - FixedPerUnit : value × groupQuantity  (somme des quantités du groupe)
    /// </summary>
    /// <param name="type">Type de T.S.</param>
    /// <param name="value">Taux ou montant fixe par unité</param>
    /// <param name="groupHT">Sous-total HT du groupe</param>
    /// <param name="groupQuantity">Somme des quantités du groupe</param>
    public static decimal ComputeOnTotalSpecificTax(
        SpecificTaxType type, decimal value, decimal groupHT, decimal groupQuantity)
    {
        if (value <= 0m)
            return 0m;

        return type switch
        {
            SpecificTaxType.Percentage =>
                Math.Round(groupHT * value / 100m, 2),

            SpecificTaxType.FixedPerUnit =>
                Math.Round(value * groupQuantity, 2),

            _ => 0m
        };
    }

    // ═══════════════════════════════════════════════════
    //  HELPERS PUBLICS
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Calcule le montant de remise selon le type et la valeur.
    /// La remise est plafonnée au montant de base (jamais négatif).
    /// </summary>
    public static decimal CalculateDiscountAmount(
        decimal baseAmount, DiscountType type, decimal value)
    {
        if (baseAmount <= 0 || value <= 0)
            return 0m;

        return type switch
        {
            DiscountType.Percentage => Math.Round(
                baseAmount * Math.Min(value, 100m) / 100m, 2),
            DiscountType.FixedAmount => Math.Round(
                Math.Min(value, baseAmount), 2),
            _ => 0m
        };
    }

    /// <summary>
    /// Garantit la paire HT/TTC — sans taxe spécifique.
    /// </summary>
    public static (decimal ht, decimal ttc) EnsureDualPrices(
        decimal inputPrice, PriceMode mode, decimal taxRate)
        => EnsureDualPrices(inputPrice, mode, taxRate,
            SpecificTaxType.None, 0m);

    /// <summary>
    /// Garantit la paire HT/TTC en tenant compte de la TVA ET de la taxe spécifique.
    ///
    /// Formules (PerArticle, par unité, qty=1) :
    ///   TS Percentage  : TTC = HT × (1 + TS%/100) × (1 + TVA%/100)
    ///   TS FixedPerUnit: TTC = (HT + TS_fixe) × (1 + TVA%/100)
    ///
    /// Pour le mode OnTotal, l'appelant passe SpecificTaxType.None
    /// (la TS est calculée au niveau facture).
    /// </summary>
    public static (decimal ht, decimal ttc) EnsureDualPrices(
        decimal inputPrice,
        PriceMode mode,
        decimal taxRate,
        SpecificTaxType specificTaxType,
        decimal specificTaxValue)
    {
        decimal rate = taxRate / 100m;

        switch (specificTaxType)
        {
            case SpecificTaxType.Percentage when specificTaxValue > 0:
                {
                    decimal tsRate = specificTaxValue / 100m;
                    if (mode == PriceMode.TTC)
                    {
                        decimal ttc = inputPrice;
                        decimal divisor = (1m + tsRate) * (1m + rate);
                        decimal ht = divisor > 0m
                            ? Math.Round(ttc / divisor, 2)
                            : ttc;
                        return (Math.Max(0m, ht), ttc);
                    }
                    else
                    {
                        decimal ht = inputPrice;
                        decimal ttc = Math.Round(ht * (1m + tsRate) * (1m + rate), 2);
                        return (ht, ttc);
                    }
                }

            case SpecificTaxType.FixedPerUnit when specificTaxValue > 0:
                {
                    if (mode == PriceMode.TTC)
                    {
                        decimal ttc = inputPrice;
                        decimal divisor = 1m + rate;
                        decimal baseBeforeTva = divisor > 0m ? ttc / divisor : ttc;
                        decimal ht = Math.Round(baseBeforeTva - specificTaxValue, 2);
                        return (Math.Max(0m, ht), ttc);
                    }
                    else
                    {
                        decimal ht = inputPrice;
                        decimal ttc = Math.Round((ht + specificTaxValue) * (1m + rate), 2);
                        return (ht, ttc);
                    }
                }

            default:
                {
                    if (mode == PriceMode.TTC)
                    {
                        decimal ttc = inputPrice;
                        decimal ht = rate > 0m
                            ? Math.Round(ttc / (1m + rate), 2)
                            : ttc;
                        return (ht, ttc);
                    }
                    else
                    {
                        decimal ht = inputPrice;
                        decimal ttc = Math.Round(ht * (1m + rate), 2);
                        return (ht, ttc);
                    }
                }
        }
    }

    // ═══════════════════════════════════════════════════
    //  RÉTROCOMPATIBILITÉ — Parse ancien format string
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Convertit l'ancien format string ("10%" ou "230") vers le couple typé.
    /// Utile pour la migration de données existantes.
    /// </summary>
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

/// <summary>
/// Données d'entrée pour le calcul d'une ligne.
/// </summary>
public class LineCalculationInput
{
    public decimal UnitPriceHT { get; set; }
    public decimal UnitPriceTTC { get; set; }
    public decimal Quantity { get; set; } = 1;
    public TaxGroup TaxGroup { get; set; } = TaxGroup.B;
    public decimal TaxRate { get; set; }
    public PriceMode PriceMode { get; set; } = PriceMode.TTC;

    // Remise
    public DiscountType DiscountType { get; set; } = DiscountType.None;
    public decimal DiscountValue { get; set; }
    public bool DiscountBeforeTax { get; set; } = true;

    // ── Taxe spécifique — typée ──
    public SpecificTaxType SpecificTaxType { get; set; } = SpecificTaxType.None;
    public decimal SpecificTaxValue { get; set; }
    public TaxApplicationMode TaxApplicationMode { get; set; } = TaxApplicationMode.PerArticle;

    // ── Compat (lues par CartItemViewModel.Recalculate + TaxCalculator) ──
    public bool HasSpecificTax { get; set; }
    public decimal SpecificTaxRate { get; set; }
    public string TaxSpecificValue { get; set; } = "";
}

/// <summary>
/// Résultat du calcul complet d'une ligne.
/// </summary>
public class LineCalculationResult
{
    /// <summary>Qty × UnitPriceHT (avant remise)</summary>
    public decimal AmountHTBeforeDiscount { get; set; }

    /// <summary>Montant de la remise (sur HT ou TTC selon DiscountBeforeTax)</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Montant HT net (base fiscale)</summary>
    public decimal AmountHT { get; set; }

    /// <summary>Montant TVA</summary>
    public decimal AmountTVA { get; set; }

    /// <summary>Taxe spécifique PerArticle (0 si OnTotal — distribué au niveau facture)</summary>
    public decimal TaxSpecificAmount { get; set; }

    /// <summary>Total TTC final (après toutes taxes et remises)</summary>
    public decimal AmountTTC { get; set; }
}