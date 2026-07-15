using System.Globalization;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public static class ReceiptDocumentBuilder
{
    private static string Fmt(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);
    private static string Rate(decimal r) => (r * 100m).ToString("0.##", CultureInfo.InvariantCulture);


    public static ReceiptDocument Build(
        Invoice invoice, Company company, PointOfSale? pos,
        DateTimeOffset time, decimal exchangeRate,
        bool isDuplicate = false, bool asProforma = false,
        int overridePaperWidthMm = 80)
    {
        int width = overridePaperWidthMm >= 80 ? 48 : 32;
        var doc = new ReceiptDocument
        {
            Width = width,
            IsProforma = asProforma,
            IsDuplicate = isDuplicate,
            PrintedAt = time.ToString("dd/MM/yyyy HH:mm")
        };
        var e = doc.Elements;

        void Text(string t, ReceiptAlign a = ReceiptAlign.Left, bool bold = false, bool dbl = false)
            => e.Add(new ReceiptElement { Type = ReceiptElementType.Text, Text = t, Align = a, Bold = bold, DoubleSize = dbl });
        void Row(string l, string r) => e.Add(new ReceiptElement { Type = ReceiptElementType.Row, Left = l, Right = r });
        void Dash() => e.Add(new ReceiptElement { Type = ReceiptElementType.DashLine });
        void Dbl() => e.Add(new ReceiptElement { Type = ReceiptElementType.DoubleLine });
        void Feed(int n = 1) => e.Add(new ReceiptElement { Type = ReceiptElementType.Feed, FeedLines = n });

        // ── LOGO ──
        if (company.Logo is { Length: > 0 })
        {
            e.Add(new ReceiptElement
            {
                Type = ReceiptElementType.Logo,
                Text = Convert.ToBase64String(company.Logo), // base64 PNG/JPG
                Align = ReceiptAlign.Center
            });
        }

        // ── HEADER (company + POS) ──
        Text(company.Name, ReceiptAlign.Center, bold: true, dbl: true);
        if (!string.IsNullOrWhiteSpace(company.Address)) Text(company.Address, ReceiptAlign.Center);
        if (!string.IsNullOrWhiteSpace(company.NIF)) Text($"NIF: {company.NIF}", ReceiptAlign.Center);
        if (pos != null && !string.IsNullOrWhiteSpace(pos.Name)) Text($"Point de vente: {pos.Name}", ReceiptAlign.Center);

        if (asProforma)
        {
            Dbl();
            Text("*** PROFORMA - SANS VALEUR FISCALE ***", ReceiptAlign.Center, bold: true);
        }
        if (isDuplicate)
            Text("*** DUPLICATA ***", ReceiptAlign.Center, bold: true);

        // ── INVOICE META ──
        Dbl();
        Text($" N: {invoice.InvoiceNumber}");
        Text($" Type: {GetTypeLabel(invoice.Type)}");
        Text($" Date: {invoice.CreatedAt:dd/MM/yyyy HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(invoice.ISF)) Text($" ISF: {invoice.ISF}");
        if (!string.IsNullOrWhiteSpace(invoice.OperatorName)) Text($" Op: {invoice.OperatorName}");
        if (!string.IsNullOrWhiteSpace(invoice.ClientName)) Text($" Client: {invoice.ClientName}");
        if (!string.IsNullOrWhiteSpace(invoice.ClientNIF)) Text($" NIF client: {invoice.ClientNIF}");

        // ── LINES ──
        Dash();
        foreach (var ln in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            string grp = GroupLabel(ln.TaxGroup, ln.TaxGroupAType);
            decimal up = GetEffectiveUnitPrice(ln, invoice.PriceMode);
            Text($" {ln.Name} [{grp}]");
            Row($"  {ln.Quantity:0.###} x {Fmt(up)}", Fmt(ln.AmountTTC));
        }

        // ── TAX BREAKDOWN (per group) ──
        Dbl();
        Text(" DETAIL FISCAL PAR GROUPE", bold: true);
        Dash();
        foreach (var g in invoice.Lines.GroupBy(l => l.TaxGroup).OrderBy(g => g.Key))
        {
            char letter = (char)('A' + (int)g.Key);
            string rateCode = $"{Rate(g.First().TaxRate)}%";
            Row($"TOTAL H.T. [{letter}] Taxable {rateCode}", Fmt(g.Sum(l => l.AmountHT)));
            Row($"TOTAL TVA [{letter}] Taxable {rateCode}", Fmt(g.Sum(l => l.AmountTVA)));
        }
        Dash();
        Row("Total HT", Fmt(invoice.TotalHT));
        Row("Total TVA", Fmt(invoice.TotalTVA));

        // ── TOTAL TTC ──
        Dbl();
        e.Add(new ReceiptElement
        {
            Type = ReceiptElementType.Row,
            Left = "TOTAL TTC",
            Right = $"{Fmt(invoice.TotalTTC)} CDF",
            Bold = true,
            DoubleHeight = true
        });

        // ── ADVANCE BLOCK ──
        WriteAdvanceBlock(e, invoice,
            (l, r) => Row(l, r),
            t => Text(t),
            () => Dbl());

        // ── PAYMENTS ──
        Dbl();
        foreach (var p in invoice.Payments)
            Row(GetPaymentLabel(p.PaymentType), Fmt(p.Amount));

        // ── COMMENTS ──
        WriteComments(e, invoice,
            () => Dash(),
            t => Text(t));

        // ── NORMALISATION + QR ──
        if (!string.IsNullOrEmpty(invoice.CodeDEFDGI))
        {
            Dash();
            Text("── FACTURE NORMALISEE ──", ReceiptAlign.Center);
            Text(" Code DEF/DGI:");
            Text($" {invoice.CodeDEFDGI}");
            if (!string.IsNullOrEmpty(invoice.NIM)) Text($" NIM: {invoice.NIM}");
            if (!string.IsNullOrEmpty(invoice.Counters)) Text($" Compteurs: {invoice.Counters}");
            if (invoice.NormalizedAt.HasValue) Text($" Normalisee le: {invoice.NormalizedAt:dd/MM/yyyy HH:mm:ss}");

            // ⭐ QR — native element, NOT bytes. Empty on proforma (never set).
            if (!string.IsNullOrEmpty(invoice.QRCodeContent))
            {
                Feed();
                e.Add(new ReceiptElement
                {
                    Type = ReceiptElementType.QrCode,
                    Text = invoice.QRCodeContent,     // ← the ready string
                    Align = ReceiptAlign.Center
                });
            }
        }

        // ── FOOTER ──
        Feed();
        Dash();
        Text(doc.FooterText, ReceiptAlign.Center);
        Text($"Imprime le {doc.PrintedAt}", ReceiptAlign.Center);
        Dash();
        Feed(3);

        return doc;
    }

    private static void WriteAdvanceBlock(
        List<ReceiptElement> e, Invoice inv,
        Action<string, string> Row, Action<string> Text, Action Dbl)
    {
        void TextL(string t) => Text(t);
        if (inv.IsAdvanceInvoice)
        {
            Dbl();
            e.Add(new ReceiptElement { Type = ReceiptElementType.Text, Text = " DETAIL ACOMPTE", Bold = true });
            Row("Total commande", Fmt(inv.OrderTotal));
            Row("Acomptes anterieurs", Fmt(inv.PreviousAdvancesTotal));
            Row("Acompte verse", Fmt(inv.AdvanceAmount));
            e.Add(new ReceiptElement
            {
                Type = ReceiptElementType.Row,
                Left = "Reste a percevoir",
                Right = Fmt(inv.RemainingAfterAdvance),
                Bold = true
            });
            if (!string.IsNullOrWhiteSpace(inv.AdvanceGroupId))
                TextL($" Ref projet: {inv.AdvanceGroupId}");
        }
        else if (inv.IsFinalWithAdvances)
        {
            Dbl();
            e.Add(new ReceiptElement { Type = ReceiptElementType.Text, Text = " SOLDE FINAL APRES ACOMPTES", Bold = true });
            Row("Total facture", Fmt(inv.TotalTTC));
            Row("Acomptes percus", Fmt(inv.TotalAdvancesPaid));
            e.Add(new ReceiptElement
            {
                Type = ReceiptElementType.Row,
                Left = "Solde du",
                Right = Fmt(inv.RemainingBalance),
                Bold = true
            });
        }
    }

    private static void WriteComments(
        List<ReceiptElement> e, Invoice invoice, Action Dash, Action<string> Text)
    {
        var comments = new (string id, string val)[]
        {
            ("A", invoice.CommentA), ("B", invoice.CommentB),
            ("C", invoice.CommentC), ("D", invoice.CommentD),
            ("E", invoice.CommentE), ("F", invoice.CommentF),
            ("G", invoice.CommentG), ("H", invoice.CommentH)
        };
        if (!comments.Any(c => !string.IsNullOrWhiteSpace(c.val))) return;

        Dash();
        Text("COMMENTAIRES:");
        foreach (var (id, val) in comments)
            if (!string.IsNullOrWhiteSpace(val))
                Text($" Ligne {id}: {val}");
    }

    private static decimal GetEffectiveUnitPrice(InvoiceLine ln, PriceMode mode)
        => mode == PriceMode.TTC ? ln.UnitPriceTTC : ln.UnitPriceHT;

    private static string GroupLabel(TaxGroup g, TaxGroupAType? aType)
    {
        if (g == TaxGroup.A && aType == TaxGroupAType.HorsChamp) return "A-HC";
        return ((char)('A' + (int)g)).ToString();
    }

    private static string GetTypeLabel(InvoiceType type) => type switch
    {
        InvoiceType.FV => "Facture de Vente",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Vente a l'exportation",
        InvoiceType.ET => "Acompte a l'exportation",
        InvoiceType.FA => "Facture d'avoir",
        InvoiceType.EA => "Avoir a l'exportation",
        _ => type.ToString()
    };

    // Accented labels — Sunmi renders UTF-8 natively, no code-page constraint.
    private static string GetPaymentLabel(PaymentType pt) => pt switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèques",
        PaymentType.Credit => "Crédit",
        _ => pt.ToString()
    };
}