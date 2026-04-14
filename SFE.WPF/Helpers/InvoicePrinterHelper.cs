using System.IO;
using System.IO.Packaging;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Microsoft.Win32;
using SFE.WPF.Views.Pages;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Helpers;

public static class InvoicePrintHelper
{
    private const double A4Width = 793.7;   // 210mm at 96 DPI
    private const double A4Height = 1122.5; // 297mm at 96 DPI
    private const double Margin = 40;

    /// <summary>Print the invoice via system PrintDialog.</summary>
    public static void Print(InvoiceDocumentViewModel viewModel)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var view = CreateSizedView(viewModel,
            dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);

        dialog.PrintVisual(view, $"Facture {viewModel.InvoiceNumber}");
    }

    /// <summary>Export as PDF using Microsoft Print to PDF, or XPS fallback.</summary>
    public static void ExportPdf(InvoiceDocumentViewModel viewModel)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exporter la facture",
            FileName = $"Facture_{viewModel.InvoiceNumber.Replace("/", "-")}",
            Filter = "Document XPS|*.xps|Image PNG|*.png",
            DefaultExt = ".xps"
        };

        if (dlg.ShowDialog() != true) return;

        string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

        if (ext == ".png")
            ExportAsPng(viewModel, dlg.FileName);
        else
            ExportAsXps(viewModel, dlg.FileName);
    }

    /// <summary>Print silently to "Microsoft Print to PDF".</summary>
    public static bool PrintToPdf(InvoiceDocumentViewModel viewModel, string outputPath)
    {
        try
        {
            var dlg = new PrintDialog();

            // Try to find the PDF printer
            var server = new LocalPrintServer();
            var pdfPrinter = server.GetPrintQueues()
                .FirstOrDefault(q => q.Name.Contains("PDF", StringComparison.OrdinalIgnoreCase));

            if (pdfPrinter != null)
                dlg.PrintQueue = pdfPrinter;

            var view = CreateSizedView(viewModel, A4Width - Margin * 2, A4Height - Margin * 2);
            dlg.PrintVisual(view, $"Facture {viewModel.InvoiceNumber}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ExportAsXps(InvoiceDocumentViewModel viewModel, string filePath)
    {
        double contentWidth = A4Width - Margin * 2;
        double contentHeight = A4Height;

        var view = CreateSizedView(viewModel, contentWidth, double.PositiveInfinity);

        // Create a FixedDocument
        var fixedPage = new FixedPage
        {
            Width = A4Width,
            Height = Math.Max(A4Height, view.ActualHeight + Margin * 2)
        };

        // We need to reparent: create a VisualBrush
        var container = new Border
        {
            Width = contentWidth,
            Height = view.ActualHeight,
            Background = new VisualBrush(view) { Stretch = Stretch.None }
        };

        FixedPage.SetLeft(container, Margin);
        FixedPage.SetTop(container, Margin);
        fixedPage.Children.Add(container);
        fixedPage.Measure(new Size(A4Width, fixedPage.Height));
        fixedPage.Arrange(new Rect(new Size(A4Width, fixedPage.Height)));

        var pageContent = new PageContent();
        ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);

        var fixedDoc = new FixedDocument();
        fixedDoc.Pages.Add(pageContent);

        // Delete existing file
        if (File.Exists(filePath)) File.Delete(filePath);

        using var package = Package.Open(filePath, FileMode.Create);
        using var xpsDoc = new XpsDocument(package);
        var writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
        writer.Write(fixedDoc);
    }

    private static void ExportAsPng(InvoiceDocumentViewModel viewModel, string filePath)
    {
        double width = A4Width - Margin * 2;
        var view = CreateSizedView(viewModel, width, double.PositiveInfinity);

        double dpi = 192; // 2x for sharp output
        double scale = dpi / 96.0;

        var rtb = new RenderTargetBitmap(
            (int)(view.ActualWidth * scale),
            (int)(view.ActualHeight * scale),
            dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(view);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var fs = File.Create(filePath);
        encoder.Save(fs);
    }

    private static InvoiceDocumentView CreateSizedView(
        InvoiceDocumentViewModel vm, double width, double height)
    {
        var view = new InvoiceDocumentView { DataContext = vm };
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, view.DesiredSize.Width, view.DesiredSize.Height));
        view.UpdateLayout();
        return view;
    }
}