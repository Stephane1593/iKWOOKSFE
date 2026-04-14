namespace SFE.Domain.Enums
{
    public static class TaxGroupExtensions
    {
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
    }
}