using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Validation pré-envoi (utilisée par le ViewModel pour feedback immédiat).
/// La validation complète async (avec lookup DB) est dans InvoiceService.ValidateInvoiceAsync().
/// </summary>
public class InvoiceValidationService
{
    public List<string> Validate(Invoice invoice)
    {
        var errors = new List<string>();

        // ── Règles générales ──
        if (string.IsNullOrEmpty(invoice.ISF))
            errors.Add("L'identifiant SFE (ISF) est obligatoire");

        if (string.IsNullOrEmpty(invoice.OperatorName))
            errors.Add("Le nom de l'opérateur est obligatoire");

        if (string.IsNullOrEmpty(invoice.InvoiceNumber))
            errors.Add("Le numéro de facture est obligatoire");

        // ── Articles ──
        if (invoice.Lines.Count == 0)
            errors.Add("La facture doit contenir au moins un article");

        foreach (var line in invoice.Lines)
        {
            if (string.IsNullOrEmpty(line.Name))
                errors.Add($"Ligne {line.LineNumber}: le nom de l'article est obligatoire");

            if (string.IsNullOrEmpty(line.Code))
                errors.Add($"Ligne {line.LineNumber}: le code de l'article est obligatoire");

            if (line.Quantity <= 0)
                errors.Add($"Ligne {line.LineNumber}: la quantité doit être > 0");

            if (line.UnitPriceHT < 0)
                errors.Add($"Ligne {line.LineNumber}: le prix unitaire HT ne peut pas être négatif");

            if (line.UnitPriceTTC < 0)
                errors.Add($"Ligne {line.LineNumber}: le prix unitaire TTC ne peut pas être négatif");

            if (line.TaxRate < 0)
                errors.Add($"Ligne {line.LineNumber}: le taux de taxe ne peut pas être négatif");

            // Type article vs groupe
            // Only group N is reserved for taxes/redevances on this MCF.
            // Group L is a regular VAT group (often configured at 0% exonéré),
            // so BIE/SER are perfectly valid in L.
            if (line.TaxGroup == TaxGroup.N && line.ItemType != ItemType.TAX)
                errors.Add($"Ligne {line.LineNumber}: l'article « {line.Name} » (groupe N) doit être de type TAX.");

            if (line.TaxGroup != TaxGroup.N && line.ItemType == ItemType.TAX)
                errors.Add($"Ligne {line.LineNumber}: l'article « {line.Name} » : le type TAX est réservé au groupe N.");

            // Remise
            if (line.DiscountType == DiscountType.Percentage && line.DiscountValue > 100)
                errors.Add($"Ligne {line.LineNumber}: la remise ne peut pas dépasser 100%");

            if (line.DiscountType == DiscountType.FixedAmount && line.DiscountValue < 0)
                errors.Add($"Ligne {line.LineNumber}: le montant de remise ne peut pas être négatif");

            // Taxe spécifique
            if (line.SpecificTaxType != SpecificTaxType.None)
            {
                if (line.SpecificTaxValue <= 0)
                    errors.Add($"Ligne {line.LineNumber}: la valeur de taxe spécifique doit être > 0");

                if (line.TaxApplicationMode == TaxApplicationMode.OnTotal
                    && string.IsNullOrWhiteSpace(line.SpecificTaxName))
                    errors.Add($"Ligne {line.LineNumber}: le nom de la T.S. est requis en mode OnTotal");
            }

            // Cohérence HT/TTC
            if (line.UnitPriceHT > 0 && line.UnitPriceTTC > 0
                && line.UnitPriceTTC < line.UnitPriceHT && line.TaxRate > 0)
                errors.Add($"Ligne {line.LineNumber}: le prix TTC ne peut pas être inférieur au prix HT");
        }

        // ── Montants ──
        if (invoice.TotalTTC <= 0 && invoice.Type.IsSale())
            errors.Add("Le montant total TTC doit être > 0 pour une facture de vente");

        // ── Paiements ──
        if (invoice.Payments.Count > 0)
        {
            var totalPaiements = invoice.Payments.Sum(p => p.Amount);
            if (totalPaiements < invoice.TotalTTC)
                errors.Add($"Le total des paiements ({totalPaiements:N2}) est inférieur au TTC ({invoice.TotalTTC:N2})");
        }

        // ── 🆕 Export coherence ──
        // Allowed on an export invoice:
        //   E → goods/services being exported
        //   N → redevances/taxes carried on the same invoice (type TAX)
        // L is NOT allowed: an L-rate sale is a domestic VAT transaction.
        if (invoice.Type.IsExport())
        {
            foreach (var line in invoice.Lines)
            {
                if (line.TaxGroup != TaxGroup.E && line.TaxGroup != TaxGroup.N)
                    errors.Add($"Ligne {line.LineNumber}: facture d'exportation → groupe E requis (actuellement {line.TaxGroup})");
            }
        }

        // ── Facture d'avoir ──
        if (invoice.Type.IsCreditNote())
        {
            if (!invoice.CreditNoteNature.HasValue)
                errors.Add("La nature de la facture d'avoir est obligatoire (COR, RAN, RAM, RRR)");

            if (string.IsNullOrEmpty(invoice.OriginalInvoiceReference))
                errors.Add("La référence de la facture originale est obligatoire");

            // 🆕 RRR → reference must be "RRR"
            if (invoice.CreditNoteNature == CreditNoteNature.RRR
                && !string.IsNullOrWhiteSpace(invoice.OriginalInvoiceReference)
                && invoice.OriginalInvoiceReference.Trim().ToUpper() != "RRR")
            {
                errors.Add("Pour le type RRR, la référence doit être « RRR »");
            }

            // Non-RRR → reference must be 24 chars (Code DEF/DGI format)
            if (invoice.CreditNoteNature != CreditNoteNature.RRR
                && !string.IsNullOrWhiteSpace(invoice.OriginalInvoiceReference)
                && invoice.OriginalInvoiceReference.Trim().Length != 24)
            {
                errors.Add("La référence doit être le Code DEF/DGI (24 caractères) de la facture originale");
            }
        }

        // ── Client rules ──
        if (invoice.ClientType == ClientType.PM || invoice.ClientType == ClientType.PC || invoice.ClientType == ClientType.PL)
        {
            if (string.IsNullOrWhiteSpace(invoice.ClientNIF))
                errors.Add($"Le NIF est obligatoire pour le type client {invoice.ClientType}");
            if (string.IsNullOrWhiteSpace(invoice.ClientName))
                errors.Add($"Le nom/dénomination est obligatoire pour le type client {invoice.ClientType}");
        }

        if (invoice.ClientType == ClientType.AO)
        {
            if (string.IsNullOrWhiteSpace(invoice.ClientName))
                errors.Add("Le nom est obligatoire pour les clients AO");
            if (string.IsNullOrWhiteSpace(invoice.CommentA))
                errors.Add("La référence du certificat d'exonération (Ligne A) est obligatoire pour AO");
        }

        if (invoice.Lines.Any(l => l.TaxGroup == TaxGroup.D) && string.IsNullOrWhiteSpace(invoice.CommentA))
            errors.Add("La référence du document de dérogation DGI (Ligne A) est obligatoire pour le groupe D");

        return errors;
    }
}