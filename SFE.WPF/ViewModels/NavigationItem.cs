namespace SFE.WPF.ViewModels;

public class NavigationItem
{
    public string Label { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty; // Caractère Segoe MDL2 Assets
    public string PageKey { get; set; } = string.Empty;
    public bool IsSeparator { get; set; } = false;
}