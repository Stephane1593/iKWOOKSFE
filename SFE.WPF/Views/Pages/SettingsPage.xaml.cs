using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SFE.WPF.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void JumpToCompany(object sender, RoutedEventArgs e) => ScrollTo(SectionCompany);
    private void JumpToDevice(object sender, RoutedEventArgs e) => ScrollTo(SectionDevice);
    private void JumpToBilling(object sender, RoutedEventArgs e) => ScrollTo(SectionBilling);
    private void JumpToLoyalty(object sender, RoutedEventArgs e) => ScrollTo(SectionLoyalty);
    private void JumpToLicense(object sender, RoutedEventArgs e) => ScrollTo(SectionLicense);

    private void ScrollTo(FrameworkElement target)
    {
        if (target == null) return;
        var transform = target.TransformToAncestor(MainScroll);
        var offset = transform.Transform(new Point(0, 0)).Y;
        MainScroll.ScrollToVerticalOffset(MainScroll.VerticalOffset + offset - 16);
    }
}