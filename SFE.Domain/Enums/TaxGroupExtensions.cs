namespace SFE.Domain.Enums;

public static class TaxGroupExtensions
{
    /// <summary>Code court pour affichage (en-tête colonne, ligne facture…).</summary>
    public static string DisplayCode(this TaxGroup group, TaxGroupAType? variant = null)
        => group == TaxGroup.A && variant == TaxGroupAType.HorsChamp
            ? "A-HC"
            : group.ToString();

    /// <summary>Libellé long (inchangé pour compat) + overload avec variante.</summary>
    public static string GetGroupLabel(this TaxGroup group) => group switch
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

    public static string GetGroupLabel(this TaxGroup group, TaxGroupAType? variant)
    {
        if (group == TaxGroup.A)
            return variant == TaxGroupAType.HorsChamp ? "Hors champ TVA" : "Exonéré de TVA";
        return group.GetGroupLabel();
    }
}