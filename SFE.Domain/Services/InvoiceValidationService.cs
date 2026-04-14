using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

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

            if (line.Quantity <= 0)
                errors.Add($"Ligne {line.LineNumber}: la quantité doit être > 0");

            if (line.UnitPrice < 0)
                errors.Add($"Ligne {line.LineNumber}: le prix unitaire ne peut pas être négatif");

            if (line.TaxRate < 0)
                errors.Add($"Ligne {line.LineNumber}: le taux de taxe ne peut pas être négatif");

            // Vérification type d'article vs groupe de taxation
            // BIE/SER valide pour A-K,M ; TAX valide pour L,N
            if (line.ItemType == ItemType.TAX &&
                line.TaxGroup is not (TaxGroup.L or TaxGroup.N))
            {
                errors.Add($"Ligne {line.LineNumber}: le type TAX n'est valide que pour les groupes L et N");
            }
        }

        // ── Montants ──
        if (invoice.TotalTTC <= 0 && invoice.Type.IsSale())
            errors.Add("Le montant total TTC doit être supérieur à 0 pour une facture de vente");

        // ── Paiements ──
        if (invoice.Payments.Count > 0)
        {
            var totalPaiements = invoice.Payments.Sum(p => p.Amount);
            if (totalPaiements < invoice.TotalTTC)
                errors.Add($"Le total des paiements ({totalPaiements:N2}) est inférieur au TTC ({invoice.TotalTTC:N2})");
        }

        // ── Facture d'avoir ──
        if (invoice.Type.IsCreditNote())
        {
            if (!invoice.CreditNoteNature.HasValue)
                errors.Add("La nature de la facture d'avoir est obligatoire (COR, RAN, RAM, RRR)");

            if (string.IsNullOrEmpty(invoice.OriginalInvoiceReference))
                errors.Add("La référence de la facture originale est obligatoire pour une facture d'avoir");

            if (invoice.OriginalInvoiceReference?.Length != 24)
                errors.Add("La référence de la facture originale doit comporter exactement 24 caractères (Code DEF/DGI)");
        }

        return errors;
    }
}