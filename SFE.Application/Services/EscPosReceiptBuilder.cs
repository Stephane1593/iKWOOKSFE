using System.Text;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.Application;
using SFE.Application.Helpers;

namespace SFE.Application.Services;

/// <summary>
/// Builds an ESC/POS byte stream for thermal printers (58mm or 80mm).
/// Includes ALL DGI 2026 §1.2 mandatory mentions.
/// </summary>
public static class EscPosReceiptBuilder
{
    // ═══ ESC/POS constants ═══
    private static readonly byte[] INIT = { 0x1B, 0x40 };
    private static readonly byte[] ALIGN_LEFT = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] ALIGN_CENTER = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] ALIGN_RIGHT = { 0x1B, 0x61, 0x02 };
    private static readonly byte[] BOLD_ON = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] BOLD_OFF = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] DOUBLE_ON = { 0x1D, 0x21, 0x11 }; // double height+width
    private static readonly byte[] DOUBLE_H_ON = { 0x1D, 0x21, 0x01 }; // double height only
    private static readonly byte[] SIZE_NORMAL = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] UNDERLINE_ON = { 0x1B, 0x2D, 0x01 };
    private static readonly byte[] UNDERLINE_OFF = { 0x1B, 0x2D, 0x00 };
    private static readonly byte[] FEED_3 = { 0x1B, 0x64, 0x03 };
    private static readonly byte[] FEED_5 = { 0x1B, 0x64, 0x05 };
    private static readonly byte[] CUT_PARTIAL = { 0x1D, 0x56, 0x42, 0x03 };
    private static readonly byte[] LF = { 0x0A };

    // ═══════════════════════════════════════════════════════
    //  RECEIPT CONTEXT — POS-specific print settings
    // ═══════════════════════════════════════════════════════

    private class ReceiptContext
    {
        public int Width { get; set; } = 48;
        public int CodePage { get; set; } = 858;
        public string FooterText { get; set; } = "Merci pour votre achat !";
        public bool PrintLogo { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  PUBLIC: Build full receipt
    // ═══════════════════════════════════════════════════════

    public static byte[] Build(
        Invoice invoice,
        Company company,
        PointOfSale? pos = null,
        decimal exchangeRate = 0m,
        bool isDuplicate = false)
    {
        // ── Resolve paper width & settings from POS config ──
        int paperWidth = pos?.PaperWidthMm ?? 80;
        int charsPerLine = paperWidth >= 80 ? 48 : 32;
        int codePage = pos?.PrinterCodePage ?? 858;
        string footer = pos?.ReceiptFooterText ?? "Merci pour votre achat !";

        var ctx = new ReceiptContext
        {
            Width = charsPerLine,
            CodePage = codePage,
            FooterText = footer,
            PrintLogo = pos?.PrintLogo ?? false
        };

        var ms = new MemoryStream();

        // ── Initialize printer ──
        Write(ms, INIT);

        // ── Set code page: ESC t n ──
        byte cpByte = codePage switch
        {
            437 => 0x00,
            850 => 0x02,
            858 => 0x13,
            1252 => 0x10,
            _ => 0x13
        };
        ms.Write(new byte[] { 0x1B, 0x74, cpByte });

        // ═══════ COMPANY LOGO (optional) ═══════
        if (ctx.PrintLogo && company.Logo != null && company.Logo.Length > 0)
        {
            Write(ms, ALIGN_CENTER);
            WriteBitmapLogo(ms, company.Logo);
            Write(ms, LF);
        }

        // ═══════ (a)(b)(c)(d) HEADER: Company info ═══════
        WriteCompanyHeader(ms, company, pos, ctx);

        // ═══════ INVOICE TYPE BANNER ═══════
        WriteInvoiceTypeBanner(ms, invoice, isDuplicate, ctx);

        // ═══════ (j)(k) REGIME + INVOICE NUMBER ═══════
        WriteInvoiceMeta(ms, invoice, company, ctx);

        // ═══════ (e) CLIENT ═══════
        WriteClientSection(ms, invoice, ctx);

        // ═══════ SEPARATOR ═══════
        WriteDashLine(ms, ctx);

        // ═══════ (l) ARTICLE LINES ═══════
        WriteLineItems(ms, invoice, ctx);

        // ═══════ (m)(n)(o) TAX BREAKDOWN PER GROUP ═══════
        WriteTaxBreakdown(ms, invoice, ctx);
        WriteAdvanceBlock(ms, invoice, ctx);
        // ═══════ (t) SPECIFIC TAX ═══════
        if (invoice.TotalSpecificTax > 0)
        {
            WriteDoubleLine(ms, ctx);
            WriteRow(ms, "TAXE SPÉCIFIQUE", Fmt(invoice.TotalSpecificTax), ctx);
        }

        // ═══════ (p) TOTAL TTC ═══════
        WriteDoubleLine(ms, ctx);
        Write(ms, BOLD_ON);
        Write(ms, DOUBLE_H_ON);
        WriteRow(ms, "TOTAL TTC", $"{Fmt(invoice.TotalTTC)} CDF", ctx);
        Write(ms, SIZE_NORMAL);
        Write(ms, BOLD_OFF);

        // ═══════ (q) AMOUNT IN WORDS ═══════
        WriteDashLine(ms, ctx);
        Write(ms, ALIGN_LEFT);
        WriteText(ms, "Arrêté à la somme de:", ctx);
        Write(ms, BOLD_ON);
        WriteText(ms, NumberToFrenchWords.Convert(invoice.TotalTTC), ctx);
        Write(ms, BOLD_OFF);

        // ═══════ (r) USD + EXCHANGE RATE ═══════
        if (exchangeRate > 0)
        {
            WriteDashLine(ms, ctx);
            decimal usd = Math.Round(invoice.TotalTTC / exchangeRate, 2);
            WriteRow(ms, "Taux de change", $"1 USD = {exchangeRate:N2} CDF", ctx);
            WriteRow(ms, "Montant USD", $"{usd:N2} USD", ctx);
        }
        else if (!string.IsNullOrEmpty(invoice.CurrencyCode) &&
                 invoice.CurrencyCode != "CDF" && invoice.CurrencyRate > 0)
        {
            WriteDashLine(ms, ctx);
            decimal alt = Math.Round(invoice.TotalTTC / invoice.CurrencyRate, 2);
            WriteRow(ms, "Taux de change",
                $"1 {invoice.CurrencyCode} = {invoice.CurrencyRate:N2} CDF", ctx);
            WriteRow(ms, $"Montant {invoice.CurrencyCode}", $"{alt:N2}", ctx);
        }

        // ═══════ (s) PAYMENT MODES ═══════
        WriteDashLine(ms, ctx);
        Write(ms, BOLD_ON);
        WriteText(ms, "MODE(S) DE PAIEMENT", ctx);
        Write(ms, BOLD_OFF);
        foreach (var pay in invoice.Payments)
            WriteRow(ms, GetPaymentLabel(pay.PaymentType), Fmt(pay.Amount), ctx);

        // ═══════ (u)(v)(w) DATE, ISF, OPERATOR ═══════
        WriteDoubleLine(ms, ctx);
        WriteRow(ms, "Date", invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"), ctx);
        WriteRow(ms, "ISF", invoice.ISF, ctx);
        WriteRow(ms, "Opérateur", invoice.OperatorName, ctx);

        // ═══════ SECURITY — never for proforma ═══════
        if (invoice.Type != InvoiceType.PRO && !string.IsNullOrEmpty(invoice.CodeDEFDGI))
        {
            // ═══════ (x) SECURITY ELEMENTS ═══════
            if (!string.IsNullOrEmpty(invoice.CodeDEFDGI))
            {
                WriteDoubleLine(ms, ctx);
                Write(ms, ALIGN_CENTER);
                Write(ms, BOLD_ON);
                WriteText(ms, "── FACTURE NORMALISÉE ──", ctx);
                Write(ms, BOLD_OFF);
                Write(ms, LF);

                Write(ms, ALIGN_LEFT);
                WriteRow(ms, "Code DEF/DGI:", "", ctx);
                Write(ms, BOLD_ON);
                WriteText(ms, invoice.CodeDEFDGI, ctx);
                Write(ms, BOLD_OFF);

                if (!string.IsNullOrEmpty(invoice.NIM))
                    WriteRow(ms, "NIM", invoice.NIM, ctx);
                if (!string.IsNullOrEmpty(invoice.Counters))
                    WriteRow(ms, "Compteurs", invoice.Counters, ctx);
                if (invoice.NormalizedAt.HasValue)
                    WriteRow(ms, "Normalisée",
                        invoice.NormalizedAt.Value.ToString("dd/MM/yyyy HH:mm:ss"), ctx);

                // ── QR CODE ──
                if (!string.IsNullOrEmpty(invoice.QRCodeContent))
                {
                    Write(ms, LF);
                    Write(ms, ALIGN_CENTER);
                    WriteQrCode(ms, invoice.QRCodeContent);
                    Write(ms, LF);
                }
            }
        }
        else if (invoice.Type == InvoiceType.PRO)
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, ALIGN_CENTER);
            WriteText(ms, "Cette proforma ne tient pas lieu", ctx);
            WriteText(ms, "de facture fiscale.", ctx);
            if (invoice.ProformaValidUntil.HasValue)
                WriteText(ms, $"Valable jusqu'au {invoice.ProformaValidUntil:dd/MM/yyyy}", ctx);
        }



        // ═══════ COMMENTS (if any) ═══════
        WriteComments(ms, invoice, ctx);

        // ═══════ FOOTER ═══════
        Write(ms, LF);
        Write(ms, ALIGN_CENTER);
        WriteText(ms, ctx.FooterText, ctx);
        WriteText(ms, $"Imprimé le {DateTime.Now:dd/MM/yyyy HH:mm}", ctx);
        WriteText(ms, "Conforme DGI-RDC 2026", ctx);
        WriteDashLine(ms, ctx);

        // ── Feed & cut ──
        Write(ms, FEED_5);
        Write(ms, CUT_PARTIAL);

        return ms.ToArray();
    }

    // ═══════════════════════════════════════════════════════
    //  SECTIONS
    // ═══════════════════════════════════════════════════════

    private static void WriteCompanyHeader(
        MemoryStream ms, Company company, PointOfSale? pos, ReceiptContext ctx)
    {
        Write(ms, ALIGN_CENTER);

        Write(ms, BOLD_ON);
        Write(ms, DOUBLE_ON);
        WriteText(ms, company.Name, ctx);
        Write(ms, SIZE_NORMAL);
        Write(ms, BOLD_OFF);

        Write(ms, LF);

        // §1.2(b) NIF
        Write(ms, BOLD_ON);
        WriteText(ms, $"NIF: {company.NIF}", ctx);
        Write(ms, BOLD_OFF);

        // §1.2(c) Address where sale occurred
        if (pos != null && !string.IsNullOrWhiteSpace(pos.Address))
            WriteText(ms, $"{pos.Address}, {pos.City}", ctx);
        else if (!string.IsNullOrWhiteSpace(company.Address))
            WriteText(ms, $"{company.Address}, {company.City}", ctx);

        // §1.2(d) Contact
        if (!string.IsNullOrWhiteSpace(company.Phone))
            WriteText(ms, $"Tél: {company.Phone}", ctx);
        if (!string.IsNullOrWhiteSpace(company.Email))
            WriteText(ms, $"Email: {company.Email}", ctx);
        if (!string.IsNullOrWhiteSpace(company.RCCM))
            WriteText(ms, $"RCCM: {company.RCCM}", ctx);

        WriteText(ms, $"ISF: {company.ISF}", ctx);
        Write(ms, LF);
    }

    private static void WriteInvoiceTypeBanner(
        MemoryStream ms, Invoice invoice, bool isDuplicate, ReceiptContext ctx)
    {
        Write(ms, ALIGN_CENTER);
        WriteDoubleLine(ms, ctx);
        Write(ms, BOLD_ON);
        Write(ms, DOUBLE_H_ON);

        // §1.2(f)(g)(h)(i) — Type mentions
        string title = invoice.Type switch
        {
            InvoiceType.FV => "FACTURE DE VENTE",
            InvoiceType.FT => "FACTURE D'ACOMPTE",
            InvoiceType.EV => "FACTURE DE VENTE",
            InvoiceType.ET => "FACTURE D'ACOMPTE",
            InvoiceType.FA => "FACTURE D'AVOIR",
            InvoiceType.EA => "FACTURE D'AVOIR",
            _ => "FACTURE"
        };

        if (invoice.Type == InvoiceType.PRO)
        {
            Write(ms, BOLD_ON);
            Write(ms, DOUBLE_H_ON);
            WriteText(ms, "FACTURE PROFORMA", ctx);
            Write(ms, SIZE_NORMAL);
            WriteText(ms, "(Document non fiscal)", ctx);
            Write(ms, BOLD_OFF);
            WriteDoubleLine(ms, ctx);
            return;
        }

        WriteText(ms, title, ctx);
        Write(ms, SIZE_NORMAL);

        // §1.2(h) — EXPORTATION mention
        if (invoice.Type is InvoiceType.EV or InvoiceType.ET or InvoiceType.EA)
        {
            Write(ms, BOLD_ON);
            WriteText(ms, "EXPORTATION", ctx);
            Write(ms, BOLD_OFF);
        }

        // §1.2(g) — Credit note nature
        if (invoice.Type is InvoiceType.FA or InvoiceType.EA
            && invoice.CreditNoteNature.HasValue)
        {
            string nature = invoice.CreditNoteNature.Value switch
            {
                CreditNoteNature.COR => "Correction",
                CreditNoteNature.RAN => "Annulation",
                CreditNoteNature.RAM => "Avoir suite reprise",
                CreditNoteNature.RRR => "RRR",
                _ => ""
            };
            WriteText(ms, $"Nature: {nature}", ctx);

            if (!string.IsNullOrEmpty(invoice.OriginalInvoiceReference))
                WriteText(ms, $"Réf. orig.: {invoice.OriginalInvoiceReference}", ctx);
        }

        // §1.2(f) — DUPLICATA
        if (isDuplicate)
        {
            Write(ms, BOLD_ON);
            Write(ms, DOUBLE_H_ON);
            WriteText(ms, "*** DUPLICATA ***", ctx);
            Write(ms, SIZE_NORMAL);
            Write(ms, BOLD_OFF);
        }

        Write(ms, BOLD_OFF);
        WriteDoubleLine(ms, ctx);
    }

    private static void WriteInvoiceMeta(
        MemoryStream ms, Invoice invoice, Company company, ReceiptContext ctx)
    {
        Write(ms, ALIGN_LEFT);

        // §1.2(j) Tax regime label
        string regime = company.DefaultPriceMode == PriceMode.TTC
            ? "MODE PRIX TTC" : "MODE PRIX HT";
        WriteRow(ms, "Régime", regime, ctx);

        // §1.2(k) Sequential invoice number
        Write(ms, BOLD_ON);
        WriteRow(ms, "N° Facture", invoice.InvoiceNumber, ctx);
        Write(ms, BOLD_OFF);

        WriteRow(ms, "Type", $"{invoice.Type} — {GetTypeLabel(invoice.Type)}", ctx);
        WriteRow(ms, "Date", invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"), ctx);
    }

    private static void WriteClientSection(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        WriteDashLine(ms, ctx);
        Write(ms, ALIGN_LEFT);

        // §1.2(e) Client info
        string typeMention = invoice.ClientType switch
        {
            ClientType.PP => "[PP] Personne physique",
            ClientType.PM => "[PM] Personne Morale",
            ClientType.PC => "[PC] Pers. phys. commerçante",
            ClientType.PL => "[PL] Profession libérale",
            ClientType.AO => "[AO] Ambassades / Org. int.",
            _ => ""
        };

        WriteRow(ms, "Client", typeMention, ctx);

        if (!string.IsNullOrWhiteSpace(invoice.ClientName))
            WriteRow(ms, "Nom", invoice.ClientName, ctx);
        if (!string.IsNullOrWhiteSpace(invoice.ClientNIF))
            WriteRow(ms, "NIF", invoice.ClientNIF, ctx);
        if (!string.IsNullOrWhiteSpace(invoice.ClientPhone))
            WriteRow(ms, "Tél", invoice.ClientPhone, ctx);
        if (!string.IsNullOrWhiteSpace(invoice.ClientAddress))
            WriteRow(ms, "Adresse", invoice.ClientAddress, ctx);
    }

    private static void WriteLineItems(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        Write(ms, ALIGN_LEFT);
        Write(ms, BOLD_ON);

        // Adapt column headers to paper width
        if (ctx.Width >= 48)
            WriteText(ms, " Article           Grp  Qté    P.U.  Total", ctx);
        else
            WriteText(ms, " Article     Grp Qté   Total", ctx);

        Write(ms, BOLD_OFF);
        WriteDashLine(ms, ctx);

        foreach (var ln in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            char grpLetter = (char)('A' + (int)ln.TaxGroup);
            string tag = ln.ItemType switch
            {
                ItemType.BIE => "[B]",
                ItemType.SER => "[S]",
                ItemType.TAX => "[T]",
                _ => ""
            };

            int maxName = ctx.Width >= 48 ? 30 : 18;
            string name = ln.Name.Length > maxName ? ln.Name[..maxName] : ln.Name;
            WriteText(ms, $" {tag} {name}", ctx);

            decimal unitPrice = GetEffectiveUnitPrice(ln, invoice.PriceMode);

            if (ctx.Width >= 48)
            {
                string detail = $"   {grpLetter}"
                    + $"  {Qty(ln.Quantity),6}"
                    + $"  {FmtCompact(unitPrice),9}"
                    + $"  {FmtCompact(ln.AmountTTC),9}";
                WriteText(ms, detail, ctx);
            }
            else
            {
                string detail = $"  {grpLetter} {Qty(ln.Quantity),4} x {FmtCompact(unitPrice),7}"
                    + $" = {FmtCompact(ln.AmountTTC),7}";
                WriteText(ms, detail, ctx);
            }

            // Discount line — §1.2(l)
            if (ln.DiscountType != DiscountType.None && ln.DiscountAmount > 0)
            {
                string discDesc = ln.DiscountType == DiscountType.Percentage
                    ? $"   Remise {ln.DiscountValue}%: -{Fmt(ln.DiscountAmount)}"
                    : $"   Remise: -{Fmt(ln.DiscountAmount)}";
                WriteText(ms, discDesc, ctx);
            }

            // Specific tax per item
            if (ln.TaxSpecificAmount > 0)
                WriteText(ms, $"   T.S.: {Fmt(ln.TaxSpecificAmount)}", ctx);
        }
    }

    private static void WriteTaxBreakdown(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        WriteDoubleLine(ms, ctx);
        Write(ms, BOLD_ON);
        WriteText(ms, " DÉTAIL FISCAL PAR GROUPE", ctx);
        Write(ms, BOLD_OFF);
        WriteDashLine(ms, ctx);

        // §1.2(m)(n)(o) — Per group: total, rate, tax amount
        var groups = invoice.Lines
            .GroupBy(l => l.TaxGroup)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            char letter = (char)('A' + (int)g.Key);
            decimal rate = g.First().TaxRate;
            decimal groupHT = g.Sum(l => l.AmountHT);
            decimal groupTVA = g.Sum(l => l.AmountTVA);
            decimal groupTTC = g.Sum(l => l.AmountTTC);
            string label = TaxCalculator.GetGroupLabel(g.Key);

            Write(ms, BOLD_ON);
            WriteText(ms, $" Groupe {letter} — {label}", ctx);
            Write(ms, BOLD_OFF);
            WriteRow(ms, "  Taux TVA", $"{rate:N2}%", ctx);   // §1.2(n)
            WriteRow(ms, "  Total HT", Fmt(groupHT), ctx);   // §1.2(m)
            WriteRow(ms, "  TVA", Fmt(groupTVA), ctx);   // §1.2(o)
            WriteRow(ms, "  Total TTC", Fmt(groupTTC), ctx);
        }

        WriteDashLine(ms, ctx);
        WriteRow(ms, "Total HT", Fmt(invoice.TotalHT), ctx);
        WriteRow(ms, "Total TVA", Fmt(invoice.TotalTVA), ctx);
    }

    private static void WriteAdvanceBlock(MemoryStream ms, Invoice inv, ReceiptContext ctx)
    {
        if (inv.IsAdvanceInvoice)
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, BOLD_ON);
            WriteText(ms, " DÉTAIL ACOMPTE", ctx);
            Write(ms, BOLD_OFF);
            WriteRow(ms, "Total commande", Fmt(inv.OrderTotal), ctx);
            WriteRow(ms, "Acomptes antérieurs", Fmt(inv.PreviousAdvancesTotal), ctx);
            WriteRow(ms, "Acompte versé", Fmt(inv.AdvanceAmount), ctx);
            Write(ms, BOLD_ON);
            WriteRow(ms, "Reste à percevoir", Fmt(inv.RemainingAfterAdvance), ctx);
            Write(ms, BOLD_OFF);
            if (!string.IsNullOrWhiteSpace(inv.AdvanceGroupId))
                WriteText(ms, $" Réf projet: {inv.AdvanceGroupId}", ctx);
        }
        else if (inv.IsFinalWithAdvances)
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, BOLD_ON);
            WriteText(ms, " SOLDE FINAL APRÈS ACOMPTES", ctx);
            Write(ms, BOLD_OFF);
            WriteRow(ms, "Total facturé", Fmt(inv.TotalTTC), ctx);
            WriteRow(ms, "Acomptes perçus", Fmt(inv.TotalAdvancesPaid), ctx);
            Write(ms, BOLD_ON);
            WriteRow(ms, "Solde dû", Fmt(inv.RemainingBalance), ctx);
            Write(ms, BOLD_OFF);
        }
    }

    private static void WriteComments(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        var comments = new (string id, string val)[]
        {
            ("A", invoice.CommentA), ("B", invoice.CommentB),
            ("C", invoice.CommentC), ("D", invoice.CommentD),
            ("E", invoice.CommentE), ("F", invoice.CommentF),
            ("G", invoice.CommentG), ("H", invoice.CommentH)
        };

        bool hasAny = comments.Any(c => !string.IsNullOrWhiteSpace(c.val));
        if (!hasAny) return;

        WriteDashLine(ms, ctx);
        Write(ms, ALIGN_LEFT);
        WriteText(ms, "COMMENTAIRES:", ctx);
        foreach (var (id, val) in comments)
        {
            if (!string.IsNullOrWhiteSpace(val))
                WriteText(ms, $" Ligne {id}: {val}", ctx);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  QR CODE — ESC/POS GS ( k
    // ═══════════════════════════════════════════════════════

    private static void WriteQrCode(MemoryStream ms, string content)
    {
        byte[] data = Encoding.UTF8.GetBytes(content);

        // 1. Select model 2
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });

        // 2. Set module size (6 dots — good for 80mm)
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x06 });

        // 3. Set error correction level M (≈15%)
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31 });

        // 4. Store data
        int storeLen = data.Length + 3;
        byte sL = (byte)(storeLen % 256);
        byte sH = (byte)(storeLen / 256);
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, sL, sH, 0x31, 0x50, 0x30 });
        ms.Write(data);

        // 5. Print stored QR code
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });
    }

    // ═══════════════════════════════════════════════════════
    //  BITMAP LOGO — ESC/POS raster print (GS v 0)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Prints a company logo bitmap using GS v 0 raster bit image.
    /// Expects a PNG/BMP stored in Company.Logo.
    /// Auto-scales to fit paper width.
    /// </summary>
    private static void WriteBitmapLogo(MemoryStream ms, byte[] logoData)
    {
        try
        {
            using var logoMs = new MemoryStream(logoData);
            using var bitmap = new System.Drawing.Bitmap(logoMs);

            // Scale to max 384 dots (80mm) or 288 dots (58mm)
            int maxDots = 384;
            int targetWidth = Math.Min(bitmap.Width, maxDots);

            // Width must be multiple of 8
            targetWidth = (targetWidth / 8) * 8;
            if (targetWidth == 0) return;

            float scale = (float)targetWidth / bitmap.Width;
            int targetHeight = (int)(bitmap.Height * scale);

            using var scaled = new System.Drawing.Bitmap(targetWidth, targetHeight);
            using (var g = System.Drawing.Graphics.FromImage(scaled))
            {
                g.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
            }

            // Convert to monochrome raster data
            int widthBytes = targetWidth / 8;
            var rasterData = new byte[widthBytes * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    var pixel = scaled.GetPixel(x, y);
                    int brightness = (pixel.R + pixel.G + pixel.B) / 3;

                    if (brightness < 128) // Dark pixel → print
                    {
                        int byteIndex = y * widthBytes + x / 8;
                        int bitIndex = 7 - (x % 8);
                        rasterData[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
            }

            // GS v 0  m  xL xH  yL yH  d1...dk
            byte xL = (byte)(widthBytes % 256);
            byte xH = (byte)(widthBytes / 256);
            byte yL = (byte)(targetHeight % 256);
            byte yH = (byte)(targetHeight / 256);

            ms.Write(new byte[] { 0x1D, 0x76, 0x30, 0x00, xL, xH, yL, yH });
            ms.Write(rasterData);
        }
        catch
        {
            // Logo print failed — silently skip
        }
    }

    // ═══════════════════════════════════════════════════════
    //  LOW-LEVEL HELPERS
    // ═══════════════════════════════════════════════════════

    private static void Write(MemoryStream ms, byte[] data)
        => ms.Write(data, 0, data.Length);

    private static void WriteText(MemoryStream ms, string text, ReceiptContext ctx)
    {
        // Word-wrap for narrow paper
        if (text.Length > ctx.Width)
        {
            foreach (var line in WordWrap(text, ctx.Width))
            {
                byte[] bytes = Encoding.GetEncoding(ctx.CodePage).GetBytes(line);
                ms.Write(bytes, 0, bytes.Length);
                ms.Write(LF, 0, LF.Length);
            }
        }
        else
        {
            byte[] bytes = Encoding.GetEncoding(ctx.CodePage).GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
            ms.Write(LF, 0, LF.Length);
        }
    }

    /// <summary>Two-column row: left-aligned label, right-aligned value.</summary>
    private static void WriteRow(
        MemoryStream ms, string label, string value, ReceiptContext ctx)
    {
        int maxLabel = ctx.Width - value.Length - 1;
        if (label.Length > maxLabel && maxLabel > 0)
            label = label[..maxLabel];

        int gap = ctx.Width - label.Length - value.Length;
        if (gap < 1) gap = 1;
        string line = label + new string(' ', gap) + value;
        WriteText(ms, line, ctx);
    }

    private static void WriteDashLine(MemoryStream ms, ReceiptContext ctx)
        => WriteText(ms, new string('-', ctx.Width), ctx);

    private static void WriteDoubleLine(MemoryStream ms, ReceiptContext ctx)
        => WriteText(ms, new string('=', ctx.Width), ctx);

    // ═══════════════════════════════════════════════════════
    //  WORD WRAP
    // ═══════════════════════════════════════════════════════

    private static List<string> WordWrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxWidth)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                // Handle words longer than maxWidth
                if (word.Length > maxWidth)
                {
                    for (int i = 0; i < word.Length; i += maxWidth)
                        lines.Add(word.Substring(i,
                            Math.Min(maxWidth, word.Length - i)));
                    continue;
                }
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }

    // ═══════════════════════════════════════════════════════
    //  FORMATTING  — DGI §1.5.1 / §1.5.2
    // ═══════════════════════════════════════════════════════
    private static readonly System.Globalization.CultureInfo FR =
        System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    private static string Fmt(decimal v) => v.ToString("N2", FR); // money 2 dec
    private static string FmtCompact(decimal v) => v.ToString("N2", FR); // narrow inline
    private static string Qty(decimal v) => v.ToString("0.###", FR); // qty 3 dec trim

    private static decimal GetEffectiveUnitPrice(InvoiceLine ln, PriceMode mode)
        => mode == PriceMode.TTC ? ln.UnitPriceTTC : ln.UnitPriceHT;

    private static string GetTypeLabel(InvoiceType type) => type switch
    {
        InvoiceType.FV => "Facture de Vente",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Vente à l'exportation",
        InvoiceType.ET => "Acompte à l'exportation",
        InvoiceType.FA => "Facture d'avoir",
        InvoiceType.EA => "Avoir à l'exportation",
        _ => type.ToString()
    };

    private static string GetPaymentLabel(PaymentType pt) => pt switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèque",
        PaymentType.Credit => "Crédit",
        _ => pt.ToString()
    };
}