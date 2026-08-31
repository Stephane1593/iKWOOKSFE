using System.Globalization;
using SFE.Domain.Entities;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Single builder for both Proforma and Fiscal receipts (Sunmi thermal printer).
/// Only difference: Fiscal includes QR code + certification section.
/// </summary>
public static class ReceiptJsonBuilder
{
    private static readonly CultureInfo MONEY = CultureInfo.InvariantCulture;

    private static string Fmt(decimal v) => v.ToString("N2", MONEY);
    private static string FmtCompact(decimal v) => v.ToString("N0", MONEY);
    private static string Qty(decimal v) => v.ToString("0.###", MONEY);
    private static string Rate(decimal v) => (v * 100m).ToString("0.##", MONEY);

    public static ReceiptDocument Build(
        Invoice invoice,
        Company company,
        PointOfSale? pos,
        ITimeProvider time,
        decimal exchangeRate,
        bool isDuplicate,
        bool asProforma,
        int paperWidthMm = 58)
    {
        int width = paperWidthMm >= 80 ? 48 : 32;

        var doc = new ReceiptDocument
        {
            Width = width,
            IsProforma = asProforma,
            IsDuplicate = isDuplicate,
            PrintedAt = time.UtcNow.ToString("dd/MM/yyyy HH:mm"),
            Elements = new()
        };
        var e = doc.Elements;

        void Text(string t, ReceiptAlign a = ReceiptAlign.Left, bool bold = false, bool dbl = false)
            => e.Add(new ReceiptElement { Type = ReceiptElementType.Text, Text = t, Align = a, Bold = bold, DoubleSize = dbl });
        void Row(string l, string r) => e.Add(new ReceiptElement { Type = ReceiptElementType.Row, Left = l, Right = r });
        void Dash() => e.Add(new ReceiptElement { Type = ReceiptElementType.DashLine });
        void Dbl() => e.Add(new ReceiptElement { Type = ReceiptElementType.DoubleLine });
        void Feed(int n = 1) => e.Add(new ReceiptElement { Type = ReceiptElementType.Feed, FeedLines = n });

        // ════════════════════════════════════════════════════════
        // 1. HEADER
        // ════════════════════════════════════════════════════════

        // Logo (if exists)
        if (company.Logo is { Length: > 0 })
        {
            e.Add(new ReceiptElement
            {
                Type = ReceiptElementType.Logo,
                Text = Convert.ToBase64String(company.Logo),
                Align = ReceiptAlign.Center
            });
        }

        // Company name (bold, double size)
        Text(company.Name, ReceiptAlign.Center, bold: true, dbl: true);

        // NIF + ISF + RCCM + Phone
        if (!string.IsNullOrWhiteSpace(company.NIF))
            Text($"NIF: {company.NIF}", ReceiptAlign.Center);
        if (!string.IsNullOrWhiteSpace(company.ISF))
            Text($"ISF: {company.ISF}", ReceiptAlign.Center);
        if (!string.IsNullOrWhiteSpace(company.RCCM))
            Text($"RCCM: {company.RCCM}", ReceiptAlign.Center);
        if (!string.IsNullOrWhiteSpace(company.Phone))
            Text($"Tel: {company.Phone}", ReceiptAlign.Center);

        // ════════════════════════════════════════════════════════
        // 2. PROFORMA / DUPLICATA BANNER
        // ════════════════════════════════════════════════════════

        Dbl();

        if (asProforma)
        {
            Text("*** PROFORMA - SANS VALEUR FISCALE ***", ReceiptAlign.Center, bold: true);
            Dbl();
        }

        if (isDuplicate)
        {
            Text("*** DUPLICATA ***", ReceiptAlign.Center, bold: true);
            Dbl();
        }

        // ════════════════════════════════════════════════════════
        // 3. INVOICE META
        // ════════════════════════════════════════════════════════

        string docType = asProforma ? "PROFORMA" : GetTypeLabel(invoice.Type);
        Row(docType, invoice.InvoiceNumber);
        Row("Date:", invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm"));
        if (pos != null && !string.IsNullOrWhiteSpace(pos.Name))
            Row("Caisse:", pos.Name);
        if (!string.IsNullOrWhiteSpace(invoice.OperatorName))
            Row("Operateur:", invoice.OperatorName);
        if (!string.IsNullOrWhiteSpace(invoice.ISF))
            Row("ISF:", invoice.ISF);
        if (!string.IsNullOrWhiteSpace(invoice.ClientName))
            Row("Client:", invoice.ClientName);
        if (!string.IsNullOrWhiteSpace(invoice.ClientNIF))
            Row("NIF Client:", invoice.ClientNIF);

        Dash();

        // ════════════════════════════════════════════════════════
        // 4. LINE ITEMS (with tax group + unit price)
        // ════════════════════════════════════════════════════════

        PriceMode mode = invoice.PriceMode;
        foreach (var ln in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            string grp = GroupLabel(ln.TaxGroup, ln.TaxGroupAType);
            decimal up = mode == PriceMode.TTC ? ln.UnitPriceTTC : ln.UnitPriceHT;

            Text($" {ln.Name} [{grp}]");
            Row($"  {Qty(ln.Quantity)} x {Fmt(up)}", FmtCompact(ln.AmountTTC));

            if (ln.DiscountAmount > 0)
            {
                string discDesc = ln.DiscountType == DiscountType.Percentage
                    ? $"   Remise {Rate(ln.DiscountValue)}%"
                    : "   Remise";
                Row(discDesc, $"-{FmtCompact(ln.DiscountAmount)}");
            }
        }

        Dash();

        // ════════════════════════════════════════════════════════
        // 5. TAX BREAKDOWN (per group)
        // ════════════════════════════════════════════════════════

        Text(" DETAIL FISCAL PAR GROUPE", bold: true);
        Dash();
        foreach (var g in invoice.Lines.GroupBy(l => l.TaxGroup).OrderBy(g => g.Key))
        {
            char letter = (char)('A' + (int)g.Key);
            decimal rate = g.First().TaxRate;
            string rateCode = $"{Rate(rate)}%";

            Row($"TOTAL H.T. [{letter}] {rateCode}", FmtCompact(g.Sum(l => l.AmountHT)));
            Row($"TOTAL TVA [{letter}] {rateCode}", FmtCompact(g.Sum(l => l.AmountTVA)));
        }

        Dash();
        Row("Total HT", FmtCompact(invoice.TotalHT));
        Row("Total TVA", FmtCompact(invoice.TotalTVA));

        if (invoice.TotalSpecificTax > 0)
        {
            Row("Taxe specifique", FmtCompact(invoice.TotalSpecificTax));
        }

        // ════════════════════════════════════════════════════════
        // 6. TOTAL TTC (bold, double height)
        // ════════════════════════════════════════════════════════

        Dbl();
        e.Add(new ReceiptElement
        {
            Type = ReceiptElementType.Row,
            Left = "TOTAL TTC",
            Right = $"{FmtCompact(invoice.TotalTTC)} CDF",
            Bold = true,
            DoubleHeight = true
        });
        Dbl();

        // ════════════════════════════════════════════════════════
        // 7. ADVANCE / CREDIT NOTE BLOCKS (same for both)
        // ════════════════════════════════════════════════════════

        if (invoice.IsAdvanceInvoice)
        {
            Text(" DETAIL ACOMPTE", bold: true);
            Row("Total commande", FmtCompact(invoice.OrderTotal));
            Row("Acomptes anterieurs", FmtCompact(invoice.PreviousAdvancesTotal));
            Row("Acompte verse", FmtCompact(invoice.AdvanceAmount));
            e.Add(new ReceiptElement
            {
                Type = ReceiptElementType.Row,
                Left = "Reste a percevoir",
                Right = FmtCompact(invoice.RemainingAfterAdvance),
                Bold = true
            });
            if (!string.IsNullOrWhiteSpace(invoice.AdvanceGroupId))
                Text($" Ref projet: {invoice.AdvanceGroupId}");
            Dash();
        }
        else if (invoice.IsFinalWithAdvances && invoice.TotalAdvancesPaid > 0)
        {
            Text(" SOLDE FINAL APRES ACOMPTES", bold: true);
            Row("Total facture", FmtCompact(invoice.TotalTTC));
            Row("Acomptes percus", FmtCompact(invoice.TotalAdvancesPaid));
            e.Add(new ReceiptElement
            {
                Type = ReceiptElementType.Row,
                Left = "Solde du",
                Right = FmtCompact(invoice.RemainingBalance),
                Bold = true
            });
            Dash();
        }

        if (invoice.IsCreditNote && invoice.OriginalInvoiceId.HasValue)
        {
            Text("FACTURE D'AVOIR", ReceiptAlign.Center, bold: true);
            if (!string.IsNullOrEmpty(invoice.OriginalInvoiceReference))
                Row("Facture orig.:", invoice.OriginalInvoiceReference);
            if (invoice.CreditNoteNature.HasValue)
            {
                string nature = invoice.CreditNoteNature.Value switch
                {
                    CreditNoteNature.COR => "COR (Correction)",
                    CreditNoteNature.RAN => "RAN (Annulation)",
                    CreditNoteNature.RAM => "RAM (Avoir)",
                    CreditNoteNature.RRR => "RRR (Remise/Rabais)",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(nature))
                    Text(nature);
            }
            Dash();
        }

        // ════════════════════════════════════════════════════════
        // 8. PAYMENTS (same for both)
        // ════════════════════════════════════════════════════════

        if (invoice.Payments != null && invoice.Payments.Count > 0)
        {
            foreach (var pay in invoice.Payments)
                Row(GetPaymentLabel(pay.PaymentType), FmtCompact(pay.Amount));
            Dash();
        }

        // ════════════════════════════════════════════════════════
        // 9. ONLY FOR FISCAL: QR CODE + CERTIFICATION SECTION
        // ════════════════════════════════════════════════════════

        if (!asProforma && !string.IsNullOrEmpty(invoice.CodeDEFDGI))
        {
            Text("── FACTURE NORMALISEE ──", ReceiptAlign.Center, bold: true);
            Text($"Code: {invoice.CodeDEFDGI}", ReceiptAlign.Center);
            if (!string.IsNullOrEmpty(invoice.NIM))
                Text($"NIM: {invoice.NIM}", ReceiptAlign.Center);
            if (!string.IsNullOrEmpty(invoice.Counters))
                Text($"Compteurs: {invoice.Counters}", ReceiptAlign.Center);
            if (invoice.NormalizedAt.HasValue)
                Text($"Normalisee le: {invoice.NormalizedAt:dd/MM/yyyy HH:mm:ss}", ReceiptAlign.Center);

            // QR code (ONLY for fiscal, NOT proforma)
            if (!string.IsNullOrEmpty(invoice.QRCodeContent))
            {
                Feed();
                e.Add(new ReceiptElement
                {
                    Type = ReceiptElementType.QrCode,
                    Text = invoice.QRCodeContent,
                    Align = ReceiptAlign.Center
                });
            }
        }

        // ════════════════════════════════════════════════════════
        // 10. COMMENTS (A-H) — same for both
        // ════════════════════════════════════════════════════════

        var comments = new (string id, string val)[]
        {
            ("A", invoice.CommentA), ("B", invoice.CommentB),
            ("C", invoice.CommentC), ("D", invoice.CommentD),
            ("E", invoice.CommentE), ("F", invoice.CommentF),
            ("G", invoice.CommentG), ("H", invoice.CommentH)
        };
        if (comments.Any(c => !string.IsNullOrWhiteSpace(c.val)))
        {
            Dash();
            foreach (var (id, val) in comments)
                if (!string.IsNullOrWhiteSpace(val))
                    Text($" Ligne {id}: {val}");
        }

        // ════════════════════════════════════════════════════════
        // 11. FOOTER
        // ════════════════════════════════════════════════════════

        Dash();
        Text("Merci pour votre achat !", ReceiptAlign.Center, bold: true);
        Dash();
        Feed(3);

        return doc;
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════

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