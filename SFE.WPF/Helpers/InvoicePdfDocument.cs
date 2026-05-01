using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.Helpers;

/// <summary>
/// QuestPDF-based invoice document with proper A4 pagination.
/// Table headers repeat on each page; content never gets "cut".
/// </summary>
public class InvoicePdfDocument : IDocument
{
    private readonly Invoice _inv;
    private readonly Company? _co;
    private readonly PointOfSale? _pos;
    private readonly decimal _xRate;
    private readonly byte[]? _logo;
    private readonly byte[]? _qrCode;

    // ── Colours ──
    private const string ColPrimary = "#1565C0";
    private const string ColDarkBlue = "#0D47A1";
    private const string ColLightBg = "#F8F9FA";
    private const string ColStripeBg = "#FAFBFC";
    private const string ColBorder = "#DEE2E6";
    private const string ColText = "#212121";
    private const string ColMuted = "#757575";
    private const string ColDanger = "#E53935";
    private const string ColWarning = "#F57C00";
    private const string ColTeal = "#00897B";

    public InvoicePdfDocument(
        Invoice invoice,
        Company? company = null,
        PointOfSale? pos = null,
        decimal exchangeRate = 0,
        byte[]? logoBytes = null,
        byte[]? qrCodeBytes = null)
    {
        _inv = invoice ?? throw new ArgumentNullException(nameof(invoice));
        _co = company;
        _pos = pos;
        _xRate = exchangeRate;
        _logo = logoBytes;
        _qrCode = qrCodeBytes;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Facture {_inv.InvoiceNumber}",
        Author = _co?.Name ?? "SFE",
        Creator = "GECOM2025 - Système de Facturation Électronique"
    };

    // ═══════════════════════════════════════════════════════
    //  COMPOSE — single page definition, QuestPDF paginates
    // ═══════════════════════════════════════════════════════

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(20);
            page.MarginBottom(15);
            page.MarginHorizontal(25);
            page.DefaultTextStyle(ts => ts.FontSize(8).FontColor(ColText));

            page.Header().Element(ComposePageHeader);
            page.Content().Element(ComposeBody);
            page.Footer().Element(ComposePageFooter);
        });
    }

    // ═══════════════════════════════════════════════════════
    //  PAGE HEADER — repeats on every page
    // ═══════════════════════════════════════════════════════

    private void ComposePageHeader(IContainer container)
    {
        container.Column(col =>
        {
            // ── Company + Invoice badge ──
            col.Item().Row(row =>
            {
                // Left: company info
                row.RelativeItem(3).Column(left =>
                {
                    if (_logo is { Length: > 0 })
                    {
                        left.Item().Height(40).Image(_logo).FitHeight();
                        left.Item().PaddingTop(2);
                    }

                    left.Item().Text(_co?.Name ?? "").Bold().FontSize(12);

                    if (!string.IsNullOrEmpty(_co?.Address))
                        left.Item().Text(_co.Address).FontSize(7).FontColor(ColMuted);
                    if (!string.IsNullOrEmpty(_co?.Phone))
                        left.Item().Text($"Tél: {_co.Phone}").FontSize(7).FontColor(ColMuted);
                    if (!string.IsNullOrEmpty(_co?.Email))
                        left.Item().Text($"Email: {_co.Email}").FontSize(7).FontColor(ColMuted);
                    if (!string.IsNullOrEmpty(_co?.NIF))
                        left.Item().Text($"NIF: {_co.NIF}").FontSize(7);
                    if (!string.IsNullOrEmpty(_co?.RCCM))
                        left.Item().Text($"RCCM: {_co.RCCM}").FontSize(7);
                    if (!string.IsNullOrEmpty(_co?.ISF))
                        left.Item().Text($"Id. Nat.: {_co.ISF}").FontSize(7);
                });

                // Right: invoice type badge + meta
                row.RelativeItem(2).AlignRight().Column(right =>
                {
                    // Type badge
                    right.Item().AlignRight()
                         .Background(TypeColor(_inv.Type))
                         .Padding(5).PaddingHorizontal(10)
                         .Text(TypeName(_inv.Type))
                         .Bold().FontSize(10).FontColor(Colors.White);

                    right.Item().PaddingTop(4).AlignRight()
                         .Text(_inv.InvoiceNumber ?? "—").Bold().FontSize(10);

                    right.Item().AlignRight()
                         .Text($"Date: {_inv.CreatedAt:dd/MM/yyyy HH:mm:ss}").FontSize(7);

                    if (!string.IsNullOrEmpty(_inv.OperatorName))
                        right.Item().AlignRight()
                             .Text($"Opérateur: {_inv.OperatorName}").FontSize(7);

                    if (!string.IsNullOrEmpty(_inv.ISF))
                        right.Item().AlignRight()
                             .Text($"ISF: {_inv.ISF}").FontSize(7);

                    if (_pos != null && !string.IsNullOrEmpty(_pos.Name))
                        right.Item().AlignRight()
                             .Text($"PdV: {_pos.Name}").FontSize(7);
                });
            });

            // ── Client band ──
            col.Item().PaddingTop(6)
               .BorderTop(1).BorderBottom(1).BorderColor(ColBorder)
               .PaddingVertical(4)
               .Row(row =>
               {
                   row.RelativeItem().Column(c =>
                   {
                       c.Item().Text("Client").FontSize(6).FontColor(ColMuted);
                       c.Item().Text(_inv.ClientName ?? "Client comptoir").FontSize(9).Bold();
                   });

                   row.RelativeItem().Column(c =>
                   {
                       c.Item().Text("NIF").FontSize(6).FontColor(ColMuted);
                       c.Item().Text(_inv.ClientNIF ?? "—").FontSize(9);
                   });

                   row.RelativeItem().Column(c =>
                   {
                       c.Item().Text("Adresse").FontSize(6).FontColor(ColMuted);
                       c.Item().Text(_inv.ClientAddress ?? "—").FontSize(8);
                   });
               });

            col.Item().PaddingBottom(4);
        });
    }

    // ═══════════════════════════════════════════════════════
    //  BODY — flows & paginates automatically
    // ═══════════════════════════════════════════════════════

    private void ComposeBody(IContainer container)
    {
        container.Column(col =>
        {
            // 1. Items table — auto-paginates with repeating header
            col.Item().Element(ComposeItemsTable);

            // 2. Article count
            var count = _inv.Lines?.Count ?? 0;
            col.Item().PaddingTop(4)
               .Text($"Nombre d'articles :   {count}").Bold().FontSize(8);

            // 3. Fiscal summary + grand totals  — keep together
            col.Item().PaddingTop(6).ShowEntire().Element(ComposeFiscalSummary);

            // 4. Amount in words
            col.Item().PaddingTop(6).ShowEntire().Element(ComposeAmountInWords);

            // 5. Comments + Payments — keep together
            col.Item().PaddingTop(6).ShowEntire().Element(ComposeCommentsAndPayments);

            // 6. Security elements (DEF / QR)
            if (!string.IsNullOrEmpty(_inv.CodeDEFDGI))
                col.Item().PaddingTop(8).ShowEntire().Element(ComposeSecurityBlock);
        });
    }

    // ═══════════════════════════════════════════════════════
    //  ITEMS TABLE — with auto-repeating header
    // ═══════════════════════════════════════════════════════

    private void ComposeItemsTable(IContainer container)
    {
        var lines = _inv.Lines?.OrderBy(l => l.LineNumber).ToList()
                    ?? new List<InvoiceLine>();

        bool hasDiscount = lines.Any(l => l.DiscountAmount > 0);
        bool hasTS = lines.Any(l => l.TaxSpecificAmount > 0);

        container.Table(table =>
        {
            // ── Column widths ──
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(18);        // #
                cols.ConstantColumn(52);        // Code
                cols.RelativeColumn();          // Désignation  (stretches)
                cols.ConstantColumn(58);        // [Grp][Type]
                cols.ConstantColumn(60);        // P.U. HT
                cols.ConstantColumn(30);        // Qté
                cols.ConstantColumn(32);        // Unité
                if (hasDiscount) cols.ConstantColumn(48);  // Remise
                if (hasTS) cols.ConstantColumn(52);  // T.S.
                cols.ConstantColumn(62);        // H.T.
                cols.ConstantColumn(52);        // TVA
                cols.ConstantColumn(62);        // TTC
            });

            // ── Header row (repeats on every page) ──
            table.Header(header =>
            {
                void H(IContainer c, string t, bool right = false)
                {
                    var cell = c.Background(ColPrimary)
                                .PaddingHorizontal(3).PaddingVertical(2.5f);
                    if (right)
                        cell.AlignRight().Text(t).FontSize(6.5f).Bold().FontColor(Colors.White);
                    else
                        cell.Text(t).FontSize(6.5f).Bold().FontColor(Colors.White);
                }

                H(header.Cell(), "#");
                H(header.Cell(), "Code");
                H(header.Cell(), "Désignation");
                H(header.Cell(), "[Grp][Type]");
                H(header.Cell(), "P.U. HT", true);
                H(header.Cell(), "Qté", true);
                H(header.Cell(), "Unité");
                if (hasDiscount) H(header.Cell(), "Remise", true);
                if (hasTS) H(header.Cell(), "T.S.", true);
                H(header.Cell(), "H.T.", true);
                H(header.Cell(), "TVA", true);
                H(header.Cell(), "TTC", true);
            });

            // ── Data rows ──
            for (int i = 0; i < lines.Count; i++)
            {
                var ln = lines[i];
                var bg = i % 2 == 1 ? ColStripeBg : Colors.White.ToString();

                void C(IContainer c, string t, bool right = false, bool bold = false)
                {
                    var cell = c.Background(bg)
                                .BorderBottom(0.5f).BorderColor(ColBorder)
                                .PaddingHorizontal(3).PaddingVertical(1.5f);
                    var txt = right ? cell.AlignRight() : cell;

                    if (bold)
                        txt.Text(t).FontSize(7).Bold();
                    else
                        txt.Text(t).FontSize(7);
                }

                C(table.Cell(), ln.LineNumber.ToString());
                C(table.Cell(), ln.Code ?? "");
                C(table.Cell(), ln.Name ?? "");
                C(table.Cell(), $"[{ln.TaxGroup}][{ln.ItemType}]");
                C(table.Cell(), Fmt(ln.UnitPrice), true);
                C(table.Cell(), ln.Quantity.ToString("G"), true);
                C(table.Cell(), ln.Unit ?? "pce");
                if (hasDiscount)
                    C(table.Cell(), ln.DiscountAmount > 0 ? Fmt(ln.DiscountAmount) : "", true);
                if (hasTS)
                    C(table.Cell(), ln.TaxSpecificAmount > 0 ? Fmt(ln.TaxSpecificAmount) : "", true);
                C(table.Cell(), Fmt(ln.AmountHT), true);
                C(table.Cell(), Fmt(ln.AmountTVA), true);
                C(table.Cell(), Fmt(ln.AmountTTC), true, bold: true);
            }
        });
    }

    // ═══════════════════════════════════════════════════════
    //  FISCAL SUMMARY — left: group breakdown, right: totals
    // ═══════════════════════════════════════════════════════

    private void ComposeFiscalSummary(IContainer container)
    {
        var lines = _inv.Lines?.ToList() ?? new();
        var groups = lines
            .GroupBy(l => l.TaxGroup)
            .OrderBy(g => g.Key)
            .ToList();

        container.Row(row =>
        {
            // ── Left: tax group breakdown ──
            row.RelativeItem(3).Column(col =>
            {
                foreach (var g in groups)
                {
                    var rate = g.First().TaxRate;
                    var ht = g.Sum(l => l.AmountHT);
                    var tva = g.Sum(l => l.AmountTVA);
                    var ts = g.Sum(l => l.TaxSpecificAmount);

                    if (rate == 0)
                    {
                        SummaryLine(col, "EXONÉRÉS ET HORS CHAMP", ht);
                    }
                    else
                    {
                        SummaryLine(col, $"H.T [{g.Key}] Taxable {rate:N2}%", ht);
                        SummaryLine(col, $"TVA [{g.Key}] Taxable {rate:N2}%", tva);
                        if (ts > 0)
                            SummaryLine(col, $"T.S. [{g.Key}] Taxable {rate:N2}%", ts);
                    }
                }

                if (_inv.TotalSpecificTax > 0)
                    SummaryLine(col, "Total [N] TVA spécifique", _inv.TotalSpecificTax);
            });

            // ── Right: grand totals ──
            row.RelativeItem(2).PaddingLeft(15).Column(col =>
            {
                if (_xRate > 0)
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Taux de change :").FontSize(8);
                        r.ConstantItem(100).AlignRight()
                         .Text(_xRate.ToString("N4")).FontSize(8).Bold();
                    });
                }

                col.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL TVA").FontSize(9).Bold();
                    r.ConstantItem(100).AlignRight()
                     .Text(Fmt(_inv.TotalTVA)).FontSize(9).Bold();
                });

                // ── Big TTC box ──
                col.Item().PaddingTop(4)
                   .Background(ColPrimary).Padding(7).Row(r =>
                   {
                       r.RelativeItem().Text("Total TTC : CDF")
                        .FontSize(11).Bold().FontColor(Colors.White);
                       r.ConstantItem(120).AlignRight()
                        .Text(Fmt(_inv.TotalTTC))
                        .FontSize(11).Bold().FontColor(Colors.White);
                   });

                if (_xRate > 0)
                {
                    var usd = Math.Round(_inv.TotalTTC / _xRate, 2);
                    col.Item().PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text("Montant TTC USD").FontSize(8);
                        r.ConstantItem(100).AlignRight()
                         .Text(usd.ToString("N2")).FontSize(8).Bold();
                    });
                }
            });
        });
    }

    private static void SummaryLine(ColumnDescriptor col, string label, decimal val)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(7);
            r.ConstantItem(90).AlignRight().Text(Fmt(val)).FontSize(7);
        });
    }

    // ═══════════════════════════════════════════════════════
    //  AMOUNT IN WORDS
    // ═══════════════════════════════════════════════════════

    private void ComposeAmountInWords(IContainer container)
    {
        var words = NumberToFrenchWords(_inv.TotalTTC);
        container.BorderTop(1).BorderColor(ColBorder).PaddingTop(4)
                 .Text(text =>
                 {
                     text.DefaultTextStyle(x => x.FontSize(7).Italic());
                     text.Span("Arrêté la présente facture à la somme de ");
                     text.Span(words).Bold();
                     text.Span(" francs congolais toutes taxes comprises");
                 });
    }

    // ═══════════════════════════════════════════════════════
    //  COMMENTS + PAYMENTS
    // ═══════════════════════════════════════════════════════

    private void ComposeCommentsAndPayments(IContainer container)
    {
        container.Row(row =>
        {
            // ── Left: comments ──
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Text("COMMENTAIRES").FontSize(7).Bold();
                col.Item().Border(0.5f).BorderColor(ColBorder)
                   .MinHeight(50).Padding(4)
                   .Text(BuildComments()).FontSize(7);   // ← FIX #1
            });

            row.ConstantItem(15);   // spacer

            // ── Right: payments ──
            row.RelativeItem(2).Column(col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.ConstantColumn(90);
                    });

                    t.Header(h =>
                    {
                        h.Cell().Background(ColLightBg)
                         .BorderBottom(1).BorderColor(ColBorder)
                         .Padding(3).Text("MODE DE PAIEMENT").FontSize(7).Bold();
                        h.Cell().Background(ColLightBg)
                         .BorderBottom(1).BorderColor(ColBorder)
                         .Padding(3).AlignRight().Text("MONTANT").FontSize(7).Bold();
                    });

                    var payments = _inv.Payments?.ToList() ?? new();
                    foreach (var pay in payments)
                    {
                        t.Cell().BorderBottom(0.5f).BorderColor(ColBorder).Padding(3)
                         .Text(FormatPaymentType(pay.PaymentType)).FontSize(7);  // ← FIX #2
                        t.Cell().BorderBottom(0.5f).BorderColor(ColBorder).Padding(3)
                         .AlignRight().Text(Fmt(pay.Amount)).FontSize(7);
                    }

                    // Total row
                    t.Cell().Padding(3).Text("");
                    t.Cell().Padding(3).AlignRight()
                     .Text(Fmt(payments.Sum(p => p.Amount))).FontSize(8).Bold();
                });
            });
        });
    }

    private string BuildComments()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_inv.CommentA)) parts.Add(_inv.CommentA);
        if (!string.IsNullOrWhiteSpace(_inv.CommentB)) parts.Add(_inv.CommentB);
        if (!string.IsNullOrWhiteSpace(_inv.CommentC)) parts.Add(_inv.CommentC);
        if (!string.IsNullOrWhiteSpace(_inv.CommentD)) parts.Add(_inv.CommentD);
        if (!string.IsNullOrWhiteSpace(_inv.CommentE)) parts.Add(_inv.CommentE);
        if (!string.IsNullOrWhiteSpace(_inv.CommentF)) parts.Add(_inv.CommentF);
        if (!string.IsNullOrWhiteSpace(_inv.CommentG)) parts.Add(_inv.CommentG);
        if (!string.IsNullOrWhiteSpace(_inv.CommentH)) parts.Add(_inv.CommentH);

        return parts.Count > 0 ? string.Join("\n", parts) : "";
    }

    // ═══════════════════════════════════════════════════════
    //  SECURITY ELEMENTS (DEF / QR)
    // ═══════════════════════════════════════════════════════

    private void ComposeSecurityBlock(IContainer container)
    {
        container.BorderTop(1).BorderColor(ColBorder).PaddingTop(6)
                 .Column(col =>
                 {
                     col.Item().Text("----ELEMENTS DE SECURITE DE LA FACTURE")
                        .FontSize(7).Bold();

                     col.Item().PaddingTop(2).Text(text =>
                     {
                         text.Span("CODE DEF/DGI   ").FontSize(7).Bold();
                         text.Span(_inv.CodeDEFDGI ?? "—")
                             .FontSize(8).Bold().FontColor(ColDarkBlue);
                     });

                     col.Item().PaddingTop(6).Row(row =>
                     {
                         // QR code
                         if (_qrCode is { Length: > 0 })
                         {
                             row.ConstantItem(80).Height(80).Image(_qrCode).FitArea();
                         }
                         else
                         {
                             row.ConstantItem(80).Height(80)
                                .Border(0.5f).BorderColor(ColBorder)
                                .AlignCenter().AlignMiddle()
                                .Text("[QR]").FontSize(8).FontColor(ColMuted);
                         }

                         row.ConstantItem(12);   // spacer

                         row.RelativeItem().PaddingTop(5).Column(c =>
                         {
                             if (!string.IsNullOrEmpty(_inv.NIM))
                                 c.Item().Text($"DEF /NID").FontSize(7).Bold();

                             if (!string.IsNullOrEmpty(_inv.Counters))
                                 c.Item().Text(text =>
                                 {
                                     text.Span("DEF Compteurs:   ").FontSize(7).Bold();
                                     text.Span(_inv.Counters).FontSize(7).Bold();
                                 });

                             if (_inv.NormalizedAt.HasValue)
                                 c.Item().Text(text =>
                                 {
                                     text.Span("DEF Heure   ").FontSize(7).Bold();
                                     text.Span(_inv.NormalizedAt.Value.ToString("dd/MM/yyyy HH:mm:ss"))
                                         .FontSize(7).Bold();
                                 });
                         });
                     });
                 });
    }

    // ═══════════════════════════════════════════════════════
    //  PAGE FOOTER — repeats on every page
    // ═══════════════════════════════════════════════════════

    private void ComposePageFooter(IContainer container)
    {
        container.BorderTop(0.5f).BorderColor(ColBorder).PaddingTop(3)
                 .AlignCenter().Text(text =>
                 {
                     text.DefaultTextStyle(x => x.FontSize(7).FontColor(ColMuted));
                     text.Span("GECOM2025 — Système de Facturation Électronique    |    Page ");
                     text.CurrentPageNumber();
                     text.Span(" / ");
                     text.TotalPages();
                 });
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    private static string Fmt(decimal v) => v.ToString("N2");

    private static string TypeColor(InvoiceType t) => t switch
    {
        InvoiceType.FV => ColPrimary,
        InvoiceType.FA => ColDanger,
        InvoiceType.FT => ColWarning,
        InvoiceType.EV => ColTeal,
        InvoiceType.EA => "#C62828",
        InvoiceType.ET => "#EF6C00",
        _ => ColMuted
    };

    private static string TypeName(InvoiceType t) => t switch
    {
        InvoiceType.FV => "FACTURE DE VENTE",
        InvoiceType.FA => "FACTURE D'AVOIR",
        InvoiceType.FT => "FACTURE D'ACOMPTE",
        InvoiceType.EV => "FACTURE EXPORT VENTE",
        InvoiceType.EA => "FACTURE EXPORT AVOIR",
        InvoiceType.ET => "FACTURE EXPORT ACOMPTE",
        _ => t.ToString()
    };

    private static string FormatPaymentType(PaymentType type)
    {
        return type switch
        {
            PaymentType.Especes => "ESPECES",
            PaymentType.Virement => "VIREMENT",
            PaymentType.CarteBancaire => "CARTE BANCAIRE",
            PaymentType.MobileMoney => "MOBILE MONEY",
            PaymentType.Cheques => "CHÈQUE",
            PaymentType.Credit => "CRÉDIT",
            PaymentType.Autre => "AUTRE",
            _ => type.ToString().ToUpperInvariant()
        };
    }

    // ═══════════════════════════════════════════════════════
    //  FRENCH NUMBER → WORDS
    // ═══════════════════════════════════════════════════════

    public static string NumberToFrenchWords(decimal amount)
    {
        long whole = (long)Math.Truncate(Math.Abs(amount));
        int cents = (int)Math.Round((Math.Abs(amount) - whole) * 100);

        string result;
        if (whole == 0)
            result = "zéro";
        else
            result = IntToFrench(whole);

        if (cents > 0)
            result += $" virgule {IntToFrench(cents)}";

        // Capitalize first letter
        if (result.Length > 0)
            result = char.ToUpper(result[0]) + result[1..];

        return result;
    }

    private static string IntToFrench(long n)
    {
        if (n == 0) return "";
        if (n < 0) return "moins " + IntToFrench(-n);

        string[] u =
        {
            "", "un", "deux", "trois", "quatre", "cinq",
            "six", "sept", "huit", "neuf", "dix",
            "onze", "douze", "treize", "quatorze", "quinze",
            "seize", "dix-sept", "dix-huit", "dix-neuf"
        };

        if (n < 20)
            return u[n];

        if (n < 100)
        {
            int t = (int)(n / 10);
            int r = (int)(n % 10);

            // 70-79 → soixante + (10-19)
            if (t == 7)
            {
                int sub = (int)(n - 60);
                return sub == 11
                    ? "soixante et onze"
                    : "soixante-" + IntToFrench(sub);
            }

            // 90-99 → quatre-vingt + (10-19)
            if (t == 9)
                return "quatre-vingt-" + IntToFrench((int)(n - 80));

            string tens = t switch
            {
                2 => "vingt",
                3 => "trente",
                4 => "quarante",
                5 => "cinquante",
                6 => "soixante",
                8 => "quatre-vingt",
                _ => ""
            };

            if (r == 0)
                return t == 8 ? tens + "s" : tens;          // quatre-vingts (with s)

            if (r == 1 && t >= 2 && t <= 6)
                return tens + " et un";

            return tens + "-" + u[r];
        }

        if (n < 1_000)
        {
            long h = n / 100;
            long r = n % 100;

            string prefix = h == 1 ? "cent" : IntToFrench(h) + " cent";

            if (r == 0 && h > 1) return prefix + "s";       // deux cents
            if (r == 0) return prefix;
            return prefix + " " + IntToFrench(r);
        }

        if (n < 1_000_000)
        {
            long th = n / 1_000;
            long r = n % 1_000;

            string prefix = th == 1 ? "mille" : IntToFrench(th) + " mille";
            return r == 0 ? prefix : prefix + " " + IntToFrench(r);
        }

        if (n < 1_000_000_000)
        {
            long m = n / 1_000_000;
            long r = n % 1_000_000;

            string prefix = IntToFrench(m) + (m == 1 ? " million" : " millions");
            return r == 0 ? prefix : prefix + " " + IntToFrench(r);
        }

        // Milliards
        long b = n / 1_000_000_000;
        long br = n % 1_000_000_000;

        string bp = IntToFrench(b) + (b == 1 ? " milliard" : " milliards");
        return br == 0 ? bp : bp + " " + IntToFrench(br);
    }
}