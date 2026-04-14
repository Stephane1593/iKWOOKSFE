using SFE.Domain.Enums;

namespace SFE.Domain.Services;

/// <summary>
/// Conversion entre prix HT et TTC selon le taux de TVA.
/// Utilisé par le SFE selon le PMODE sélectionné (spec MCF C0h).
/// </summary>
public static class PriceModeConverter
{
    /// <summary>
    /// Calcule le montant HT à partir du montant TTC.
    /// Formule: HT = TTC / (1 + taux/100)
    /// </summary>
    public static decimal TtcToHt(decimal amountTTC, decimal taxRate)
    {
        if (taxRate <= 0) return amountTTC;
        return Math.Round(amountTTC / (1 + taxRate / 100m), 2);
    }

    /// <summary>
    /// Calcule le montant TTC à partir du montant HT.
    /// Formule: TTC = HT × (1 + taux/100)
    /// </summary>
    public static decimal HtToTtc(decimal amountHT, decimal taxRate)
    {
        return Math.Round(amountHT * (1 + taxRate / 100m), 2);
    }

    /// <summary>
    /// Calcule la TVA à partir du TTC.
    /// TVA = TTC - HT
    /// </summary>
    public static decimal TvaFromTtc(decimal amountTTC, decimal taxRate)
    {
        return amountTTC - TtcToHt(amountTTC, taxRate);
    }

    /// <summary>
    /// Calcule la TVA à partir du HT.
    /// TVA = HT × taux / 100
    /// </summary>
    public static decimal TvaFromHt(decimal amountHT, decimal taxRate)
    {
        return Math.Round(amountHT * taxRate / 100m, 2);
    }

    /// <summary>
    /// Calcule tous les montants d'une ligne selon le mode.
    /// </summary>
    public static (decimal ht, decimal tva, decimal ttc) Calculate(
        decimal unitPrice, decimal quantity, decimal taxRate, PriceMode mode)
    {
        decimal total = Math.Round(unitPrice * quantity, 2);

        if (mode == PriceMode.TTC)
        {
            decimal ttc = total;
            decimal ht = TtcToHt(ttc, taxRate);
            decimal tva = ttc - ht;
            return (ht, tva, ttc);
        }
        else
        {
            decimal ht = total;
            decimal tva = TvaFromHt(ht, taxRate);
            decimal ttc = ht + tva;
            return (ht, tva, ttc);
        }
    }

    /// <summary>
    /// 🆕 Garantit la paire (HT, TTC) à partir d'un prix et d'un mode.
    /// Wrapper pratique au-dessus de TaxCalculator.EnsureDualPrices.
    /// </summary>
    public static (decimal ht, decimal ttc) EnsureDualPrices(
        decimal inputPrice, PriceMode mode, decimal taxRate)
    {
        if (mode == PriceMode.TTC)
        {
            return (TtcToHt(inputPrice, taxRate), inputPrice);
        }
        else
        {
            return (inputPrice, HtToTtc(inputPrice, taxRate));
        }
    }
}