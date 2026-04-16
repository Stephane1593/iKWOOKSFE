using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SFE.WPF.Views.Pages;

public partial class ReportDetailDialog : Window
{
    private string _content = "";

    public ReportDetailDialog()
    {
        InitializeComponent();
    }

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

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_content))
        {
            Clipboard.SetText(_content);

            // Brief visual feedback
            if (sender is Button btn)
            {
                var original = btn.Content;
                btn.Content = "✓ Copié !";
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

    private void PrintClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_content)) return;

        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var doc = new FlowDocument(
                    new Paragraph(new Run(_content)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10
                    }))
                {
                    PageWidth = printDialog.PrintableAreaWidth,
                    PagePadding = new Thickness(40)
                };

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, ReportTitle);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur d'impression : {ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}