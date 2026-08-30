namespace SFE.Licensing.Domain;

/// <summary>
/// Feature flags gated by the license. Every SFE build contains all the code;
/// the license only unlocks features. Values are the string tokens embedded
/// in the license blob — do NOT rename without a migration.
/// </summary>
public enum Feature
{
    BulkInvoicing,
    Loyalty,
    StockTransfers,
    MultiPos,
    SunmiTerminal,
    EmcfFallback,
    AdvancedReports,
    RemoteSupport
}

public static class FeatureTokens
{
    // Two-way map for wire format stability.
    private static readonly Dictionary<Feature, string> _toToken = new()
    {
        [Feature.BulkInvoicing] = "bulk_invoicing",
        [Feature.Loyalty] = "loyalty",
        [Feature.StockTransfers] = "stock_transfers",
        [Feature.MultiPos] = "multi_pos",
        [Feature.SunmiTerminal] = "sunmi_terminal",
        [Feature.EmcfFallback] = "emcf_fallback",
        [Feature.AdvancedReports] = "advanced_reports",
        [Feature.RemoteSupport] = "remote_support"
    };

    private static readonly Dictionary<string, Feature> _fromToken =
        _toToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToToken(this Feature f) => _toToken[f];

    public static bool TryParse(string token, out Feature feature)
        => _fromToken.TryGetValue(token, out feature);
}