using SFE.Domain.Enums;

namespace SFE.Application.Services;

/// <summary>
/// Central policy for what counts as "sensitive enough to require a manager".
/// Values are hardcoded for now; move to AppSettings once the UI is ready.
/// </summary>
public static class ManagerAuthorizationPolicy
{
    /// <summary>Percentage discount above this triggers ApplyLargeDiscount.</summary>
    public const decimal LargeDiscountPercentThreshold = 20m;

    /// <summary>Fixed-amount discount (in the sale currency) above this triggers it.</summary>
    public const decimal LargeDiscountFixedThreshold = 50_000m;

    public static bool IsLargeDiscount(DiscountType type, decimal value) => type switch
    {
        DiscountType.Percentage => value > LargeDiscountPercentThreshold,
        DiscountType.FixedAmount => value > LargeDiscountFixedThreshold,
        _ => false
    };
}