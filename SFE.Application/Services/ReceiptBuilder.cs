using System.Text;
using SFE.Domain.Entities;

namespace SFE.Application.Services;

public static class ReceiptBuilder
{
    private const int Width = 44;

    public static string BuildTextReceipt(Invoice invoice)
    {
        var sb = new StringBuilder();
        var line = new string('═', Width);
        var thinLine = new string('─', Width);

        // ── EN-TÊTE ──
        sb.AppendLine(line);
        sb.AppendLine(Center("GECOM2025 - Système de Facturation"));
        sb.AppendLine(Center("Électronique"));
        sb.AppendLine(line);
        sb.AppendLine();

        sb.AppendLine($"  N° : {invoice.InvoiceNumber}");
        sb.AppendLine($"  Type : {GetTypeName(invoice.Type)}");
        sb.AppendLine($"  Date : {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"  ISF  : {invoice.ISF}");
        sb.AppendLine($"  Op.  : {invoice.OperatorName}");

        if (!string.IsNullOrEmpty(invoice.ClientName))
            sb.AppendLine($"  Client : {invoice.ClientName}");
        if (!string.IsNullOrEmpty(invoice.ClientNIF))
            sb.AppendLine($"  NIF    : {invoice.ClientNIF}");

        sb.AppendLine();
        sb.AppendLine(thinLine);

        // ── LIGNES ──
        sb.AppendLine($"  {"Article",-22} {"Qté",5} {"P.U.",8} {"Total",7}");
        sb.AppendLine(thinLine);

        foreach (var ln in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            // Nom sur la première ligne si trop long
            if (ln.Name.Length > 22)
            {
                sb.AppendLine($"  {ln.Name}");
                sb.AppendLine($"  {"",22} {ln.Quantity,5:G} {ln.UnitPrice,8:N0} {ln.AmountTTC,7:N0}");
            }
            else
            {
                sb.AppendLine($"  {ln.Name,-22} {ln.Quantity,5:G} {ln.UnitPrice,8:N0} {ln.AmountTTC,7:N0}");
            }
        }

        sb.AppendLine(thinLine);

        // ── TOTAUX ──
        sb.AppendLine($"  {"Sous-total HT",-30} {invoice.TotalHT,12:N0}");
        sb.AppendLine($"  {"TVA",-30} {invoice.TotalTVA,12:N0}");
        sb.AppendLine(line);
        sb.AppendLine($"  {"TOTAL TTC",-30} {invoice.TotalTTC,12:N0} CDF");
        sb.AppendLine(line);

        // ── PAIEMENTS ──
        foreach (var pay in invoice.Payments)
        {
            sb.AppendLine($"  {pay.PaymentType,-30} {pay.Amount,12:N0}");
        }

        sb.AppendLine();

        // ── NORMALISATION ──
        if (!string.IsNullOrEmpty(invoice.CodeDEFDGI))
        {
            sb.AppendLine(thinLine);
            sb.AppendLine(Center("── FACTURE NORMALISÉE ──"));
            sb.AppendLine();
            sb.AppendLine($"  Code DEF/DGI :");
            sb.AppendLine($"  {invoice.CodeDEFDGI}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(invoice.NIM))
                sb.AppendLine($"  NIM : {invoice.NIM}");
            if (!string.IsNullOrEmpty(invoice.Counters))
                sb.AppendLine($"  Compteurs : {invoice.Counters}");
            if (invoice.NormalizedAt.HasValue)
                sb.AppendLine($"  Normalisée le : {invoice.NormalizedAt:dd/MM/yyyy HH:mm:ss}");

            sb.AppendLine();
            sb.AppendLine(Center("[QR CODE]"));
        }

        sb.AppendLine();
        sb.AppendLine(thinLine);
        sb.AppendLine(Center("Merci pour votre achat !"));
        sb.AppendLine(Center($"Imprimé le {DateTime.Now:dd/MM/yyyy HH:mm}"));
        sb.AppendLine(thinLine);

        return sb.ToString();
    }

    private static string Center(string text)
    {
        if (text.Length >= Width) return text;
        var pad = (Width - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string GetTypeName(Domain.Enums.InvoiceType type) => type switch
    {
        Domain.Enums.InvoiceType.FV => "Facture de Vente",
        Domain.Enums.InvoiceType.FT => "Facture d'acompte",
        Domain.Enums.InvoiceType.EV => "Facture de vente a l'exportation",
        Domain.Enums.InvoiceType.ET => "Facture d'acompte a l'exportation",
        Domain.Enums.InvoiceType.EA => "Facture d'avoir a l'exportation",
        Domain.Enums.InvoiceType.FA => "Facture d'avaoir",
        _ => type.ToString()
    };
}