using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SFE.Infrastructure.Persistence.Converters;

/// <summary>
/// SQLite n'a pas de type natif DateTimeOffset.
/// On stocke la valeur en UtcTicks (INTEGER 64-bit) :
///   - Traduit correctement en SQL pour &gt;=, &lt;=, tri, BETWEEN, etc.
///   - Tri chronologique parfait.
///   - Round-trip sans perte (sauf l'offset local, mais on bosse en UTC partout).
/// </summary>
public static class DateTimeOffsetConverters
{
    public static readonly ValueConverter<DateTimeOffset, long> ToTicks =
        new(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

    public static readonly ValueConverter<DateTimeOffset?, long?> ToNullableTicks =
        new(
            v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
}