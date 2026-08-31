using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SFE.WPF.Views.Pages;
using SFE.WPF.ViewModels;
using Size = System.Windows.Size;
using SFE.Domain.Abstractions;

namespace SFE.WPF.Helpers;

public static class InvoicePrintHelper
{
    // ═══════════════════════════════════════════════════════
    //  PUBLIC — Print  (QuestPDF → temp PDF → system viewer)
    // ═══════════════════════════════════════════════════════

    public static void Print(InvoiceDocumentViewModel viewModel, ITimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(time);

        string tempDir = Path.Combine(Path.GetTempPath(), "SFE_Invoices");
        Directory.CreateDirectory(tempDir);
        CleanTempFiles(tempDir, TimeSpan.FromDays(1));

        // 🆕 Utilise l'horloge injectée (heure locale du POS, cohérence multi-fuseaux)
        var stamp = time.ToLocal(time.UtcNow, viewModel.SourcePos?.TimeZoneId);

        string tempPath = Path.Combine(tempDir,
            SanitizeFileName(
                $"Facture_{viewModel.InvoiceNumber}_{stamp:yyyyMMdd_HHmmss}.pdf"));

        GeneratePdf(viewModel, tempPath, time);

        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
    }

    // ═══════════════════════════════════════════════════════
    //  PUBLIC — Export  (SaveFileDialog → PDF or PNG)
    // ═══════════════════════════════════════════════════════

    public static bool ExportPdf(InvoiceDocumentViewModel viewModel, ITimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(time);

        var dlg = new SaveFileDialog
        {
            Title = "Exporter la facture",
            FileName = SanitizeFileName($"Facture_{viewModel.InvoiceNumber}"),
            Filter = "Document PDF|*.pdf|Image PNG|*.png",
            DefaultExt = ".pdf"
        };

        if (dlg.ShowDialog() != true)
            return false;

        string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

        if (ext == ".png")
            ExportAsPng(viewModel, dlg.FileName);
        else
            GeneratePdf(viewModel, dlg.FileName, time);

        Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        return true;
    }

    // ═══════════════════════════════════════════════════════
    //  CORE — Generate PDF using QuestPDF (proper pagination)
    // ═══════════════════════════════════════════════════════

    public static void GeneratePdf(
        InvoiceDocumentViewModel viewModel,
        string outputPath,
        ITimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(time);

        // ── If we have the raw source data, use the native QuestPDF document ──
        if (viewModel.SourceInvoice != null)
        {
            byte[]? qrBytes = null;
            try
            {
                if (!string.IsNullOrEmpty(viewModel.SourceInvoice.CodeDEFDGI))
                    qrBytes = GenerateQrCodeBytes(viewModel.SourceInvoice.CodeDEFDGI);
            }
            catch { /* non-critical */ }

            // 🆕 On passe explicitement ITimeProvider au document PDF :
            //    InvoicePdfDocument s'en sert pour afficher l'heure d'impression
            //    dans le fuseau du POS (SourcePos?.TimeZoneId), et non dans
            //    celui du serveur d'application.
            var doc = new InvoicePdfDocument(
                viewModel.SourceInvoice,
                time,
                viewModel.SourceCompany,
                viewModel.SourcePos,
                viewModel.SourceExchangeRate,
                viewModel.SourceLogoBytes,
                qrBytes,
                printNumber: viewModel.PrintNumber);

            doc.GeneratePdf(outputPath);
            return;
        }

        // ── Fallback: old WPF-bitmap approach (should not happen anymore) ──
        GeneratePdfFromWpf(viewModel, outputPath);
    }

    // ═══════════════════════════════════════════════════════
    //  QR CODE GENERATION  (requires NuGet: QRCoder)
    // ═══════════════════════════════════════════════════════

    private static byte[]? GenerateQrCodeBytes(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.M);
            using var qrCode = new QRCoder.PngByteQRCode(qrData);
            return qrCode.GetGraphic(8);  // 8 pixels per module
        }
        catch
        {
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  PNG EXPORT  (still uses WPF rendering — single image)
    // ═══════════════════════════════════════════════════════

    private static void ExportAsPng(InvoiceDocumentViewModel viewModel, string filePath)
    {
        var view = CreateRenderedView(viewModel);

        const double dpi = 192;
        double scale = dpi / 96.0;

        int pxW = (int)Math.Ceiling(view.ActualWidth * scale);
        int pxH = (int)Math.Ceiling(view.ActualHeight * scale);

        if (pxW < 1 || pxH < 1)
            throw new InvalidOperationException(
                "Le document est vide — impossible d'exporter en PNG.");

        var rtb = new RenderTargetBitmap(pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);

        var bg = new DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(Brushes.White, null,
                new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        rtb.Render(bg);
        rtb.Render(view);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var fs = File.Create(filePath);
        encoder.Save(fs);
    }

    // ═══════════════════════════════════════════════════════
    //  LEGACY FALLBACK — WPF bitmap slicing (old approach)
    // ═══════════════════════════════════════════════════════

    private const double A4WidthDips = 793.7;
    private const double A4HeightDips = 1122.5;
    private const double PageMarginDips = 40;
    private const double ExportDpi = 288;
    private const double MinRenderWidth = 800;

    private static void GeneratePdfFromWpf(InvoiceDocumentViewModel viewModel, string outputPath)
    {
        var view = CreateRenderedView(viewModel);

        double viewW = view.ActualWidth;
        double viewH = view.ActualHeight;

        if (viewW < 1 || viewH < 1)
            throw new InvalidOperationException(
                "Le document est vide — impossible de générer le PDF.");

        double a4ContentW = A4WidthDips - 2 * PageMarginDips;
        double a4ContentH = A4HeightDips - 2 * PageMarginDips;
        double fitScale = a4ContentW / viewW;
        double pageSliceH = a4ContentH / fitScale;

        int pageCount = Math.Max(1, (int)Math.Ceiling(viewH / pageSliceH));
        double dpiScale = ExportDpi / 96.0;

        var pageImages = new List<byte[]>(pageCount);

        for (int i = 0; i < pageCount; i++)
        {
            double yOffset = i * pageSliceH;
            double sliceH = Math.Min(pageSliceH, viewH - yOffset);

            int pxW = (int)Math.Ceiling(viewW * dpiScale);
            int pxH = (int)Math.Ceiling(sliceH * dpiScale);
            if (pxW < 1 || pxH < 1) continue;

            var rtb = new RenderTargetBitmap(
                pxW, pxH, ExportDpi, ExportDpi, PixelFormats.Pbgra32);

            var bgVis = new DrawingVisual();
            using (var dc = bgVis.RenderOpen())
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, viewW, sliceH));
            rtb.Render(bgVis);

            var slice = new DrawingVisual();
            using (var dc = slice.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(0, -yOffset));
                dc.DrawRectangle(
                    new VisualBrush(view)
                    {
                        Stretch = Stretch.None,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top
                    },
                    null, new Rect(0, 0, viewW, viewH));
                dc.Pop();
            }
            rtb.Render(slice);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            pageImages.Add(ms.ToArray());
        }

        Document.Create(c =>
        {
            foreach (byte[] img in pageImages)
                c.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin((float)(PageMarginDips * 0.75));
                    p.Content().Image(img).FitWidth();
                });
        }).GeneratePdf(outputPath);
    }

    // ═══════════════════════════════════════════════════════
    //  WPF VIEW RENDERER
    // ═══════════════════════════════════════════════════════

    private static InvoiceDocumentView CreateRenderedView(InvoiceDocumentViewModel vm)
    {
        var view = new InvoiceDocumentView { DataContext = vm };

        if (view.Background == null
            || view.Background == Brushes.Transparent
            || (view.Background is SolidColorBrush scb && scb.Color.A == 0))
            view.Background = Brushes.White;

        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double naturalW = Math.Max(view.DesiredSize.Width, MinRenderWidth);
        double naturalH = Math.Max(view.DesiredSize.Height, 1);

        view.Arrange(new Rect(0, 0, naturalW, naturalH));
        view.UpdateLayout();
        FlushDispatcher();

        view.Measure(new Size(naturalW, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, view.DesiredSize.Width, view.DesiredSize.Height));
        view.UpdateLayout();
        FlushDispatcher();

        view.Measure(new Size(view.ActualWidth, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        view.UpdateLayout();

        return view;
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    private static void FlushDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new DispatcherOperationCallback(f =>
            {
                ((DispatcherFrame)f!).Continue = false;
                return null;
            }), frame);
        Dispatcher.PushFrame(frame);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static void CleanTempFiles(string directory, TimeSpan maxAge)
    {
        try
        {
            // 🆕 Intentionnellement basé sur DateTime.Now du système de fichiers :
            //    c'est l'horloge du disque qu'on compare aux métadonnées de fichier,
            //    pas la logique métier. Pas besoin d'ITimeProvider ici.
            var cutoff = DateTime.Now - maxAge;
            foreach (var file in Directory.GetFiles(directory, "*.pdf"))
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
        }
        catch { /* non-critical */ }
    }
}