namespace SFE.WPF.ViewModels;

public class NavigationItem
{
    public string Label { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public string PageKey { get; set; } = string.Empty;
    public bool IsSeparator { get; set; } = false;

    /// <summary>Permission key required to see this item (e.g. "invoicing").</summary>
    public string? RequiredPermission { get; set; }
}