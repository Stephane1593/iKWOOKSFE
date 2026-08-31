using System;
using System.Globalization;
using System.Windows.Data;
using SFE.Domain.Enums;

namespace SFE.WPF.Converters
{
    public class EnumToFrenchLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return "";

            // SpecificTaxMode
            if (value is TaxSpecificMode mode)
            {
                return mode switch
                {
                    TaxSpecificMode.PerArticle => "Par article",
                    TaxSpecificMode.OnTotal => "Sur le total",
                    _ => mode.ToString()
                };
            }

            if (value is TaxApplicationMode taxMode)
            {
                return taxMode switch
                {
                    TaxApplicationMode.PerArticle => "Par article",
                    TaxApplicationMode.OnTotal => "Sur le total",
                    _ => taxMode.ToString()
                };
            }

            // if (value is SpecificTaxType s) ...
            if (value is SpecificTaxType type)
            {
                return type switch
                {
                    SpecificTaxType.None => "Pas de taxe spécifique",
                    SpecificTaxType.FixedPerUnit => "Montant fixe par unité",
                    SpecificTaxType.Percentage => "Pourcentage",
                    _ => type.ToString()
                };
            }

            // if (value is DiscountType d) ...
            if (value is DiscountType discount)
            {
                return discount switch
                {
                    DiscountType.None => "Aucune",
                    DiscountType.Percentage => "Pourcentage",
                    DiscountType.FixedAmount => "MontantFixe",
                    _ => discount.ToString()
                };
            }

            // if (value is TaxGroup t) ...
            if (value is TaxGroup tax)
            {
                return tax switch
                {
                    TaxGroup.A => "A Exonéré / Hors champ — TVA 0%",
                    TaxGroup.B => "B Taxable standard — TVA 16%",
                    TaxGroup.C => "C Taux réduit — TVA 5%",
                    TaxGroup.D => "D Régime dérogatoire — TVA 0%",
                    TaxGroup.E => "E Exportation — TVA 0%",
                    TaxGroup.F => "F Marché public ext. — TVA 16%",
                    TaxGroup.G => "G Marché public ext. — TVA 5%",
                    TaxGroup.H => "H Consignation — TVA 0%",
                    TaxGroup.I => "I Garantie/Caution — TVA 0%",
                    TaxGroup.J => "J Débours — TVA 0%",
                    TaxGroup.K => "K Non assujettis — TVA 0%",
                    TaxGroup.L => "L Prélèvements — (article type TAX uniquement)",
                    TaxGroup.M => "M Ventes réglementées — HT seul",
                    TaxGroup.N => "N TVA spécifique — (article type TAX uniquement)",
                    TaxGroup.O => "O Taux réduit — TVA 1%",
                    TaxGroup.P => "P Marché public ext. — TVA 1%",
                    _ => tax.ToString()
                };
            }

            // if (value is ItemType i) ...
            if (value is ItemType item)
            {
                return item switch
                {
                    ItemType.BIE => "Bien",
                    ItemType.SER => "Service",
                    ItemType.TAX => "Taxes",
                    _ => item.ToString()
                };
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}