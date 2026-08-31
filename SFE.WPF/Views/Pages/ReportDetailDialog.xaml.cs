// File: SFE.WPF/Views/Pages/ReportDetailDialog.xaml.cs
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QColors = QuestPDF.Helpers.Colors;
using QPageSizes = QuestPDF.Helpers.PageSizes;

namespace SFE.WPF.Views.Pages;

public partial class ReportDetailDialog : Window
{
    private string _content = "";

    public ReportDetailDialog()
    {
        InitializeComponent();
    }

    // ── Public properties (set by caller) ──

    public string ReportTitle
    {
        get => TitleBlock.Text;
        set => TitleBlock.Text = value;
    }

    public string ReportSubtitle
    {
        get => SubtitleBlock.Text;
        set => SubtitleBlock.Text = value;
    }

    public string ReportContent
    {
        get => _content;
        set
        {
            _content = value;
            ContentBlock.Text = value;
        }
    }

    // ── Title bar drag + double-click maximize ──

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    // ── Window chrome buttons ──

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ── Actions ──

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_content))
        {
            Clipboard.SetText(_content);

            if (sender is Button btn)
            {
                var original = btn.Content;
                btn.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new MaterialDesignThemes.Wpf.PackIcon
                        {
                            Kind = MaterialDesignThemes.Wpf.PackIconKind.Check,
                            Width = 15, Height = 15,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)),
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0)
                        },
                        new TextBlock
                        {
                            Text = "Copié !",
                            VerticalAlignment = VerticalAlignment.Center,
                            FontFamily = (FontFamily)FindResource("AppFont"),
                            Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99))
                        }
                    }
                };

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5)
                };
                timer.Tick += (_, _) =>
                {
                    btn.Content = original;
                    timer.Stop();
                };
                timer.Start();
            }
        }
    }

    /// <summary>
    /// Generates a temp PDF and opens it in the system viewer for preview + print.
    /// </summary>
    private void PrintClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_content)) return;

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SFE_Reports");
            Directory.CreateDirectory(tempDir);
            CleanOldTempFiles(tempDir, TimeSpan.FromDays(1));

            string title = ReportTitle ?? "Rapport";
            string tempPath = Path.Combine(tempDir,
                SanitizeFileName($"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"));

            GenerateReportPdf(_content, title, ReportSubtitle ?? "", tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erreur d'impression : {ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Save dialog → PDF (default) or TXT, then auto-opens the file.
    /// </summary>
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_content)) return;

        try
        {
            string title = ReportTitle ?? "Rapport";
            string baseName = SanitizeFileName($"{title}_{DateTime.Now:yyyyMMdd_HHmm}");

            var dlg = new SaveFileDialog
            {
                FileName = baseName,
                DefaultExt = ".pdf",
                Filter = "Document PDF (*.pdf)|*.pdf|Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

            if (ext == ".txt")
            {
                File.WriteAllText(dlg.FileName, _content);
            }
            else
            {
                GenerateReportPdf(_content, title, ReportSubtitle ?? "", dlg.FileName);
            }

            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erreur d'export : {ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // ═══════════════════════════════════════
    //  PDF GENERATION (QuestPDF)
    // ═══════════════════════════════════════

    private static void GenerateReportPdf(
        string textContent,
        string title,
        string subtitle,
        string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(QPageSizes.A4);
                page.MarginVertical(30);
                page.MarginHorizontal(35);

                page.DefaultTextStyle(ts => ts.FontSize(8));

                // ── Header ──
                page.Header().Column(col =>
                {
                    col.Item().Text(title)
                        .FontSize(13)
                        .Bold()
                        .FontColor(QColors.Grey.Darken3);

                    if (!string.IsNullOrEmpty(subtitle))
                    {
                        col.Item().PaddingTop(2).Text(subtitle)
                            .FontSize(8)
                            .FontColor(QColors.Grey.Medium);
                    }

                    col.Item().PaddingTop(6)
                        .LineHorizontal(0.5f)
                        .LineColor(QColors.Grey.Lighten2);

                    col.Item().PaddingBottom(8);
                });

                // ── Content — monospaced report text ──
                page.Content().Text(textContent)
                    .FontFamily("Courier New")
                    .FontSize(8)
                    .LineHeight(1.35f);

                // ── Footer — page numbers ──
                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(ts => ts.FontSize(7).FontColor(QColors.Grey.Medium));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        })
        .GeneratePdf(outputPath);
    }

    // ═══════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static void CleanOldTempFiles(string directory, TimeSpan maxAge)
    {
        try
        {
            var cutoff = DateTime.Now - maxAge;
            foreach (var file in Directory.GetFiles(directory, "*.pdf"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* non-critical */ }
    }
}