using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SFE.Infrastructure.Persistence.Converters;

public sealed class UlidToStringConverter : ValueConverter<Ulid, string>
{
    public UlidToStringConverter()
    : base(
    ulid => ulid.ToString(),
    value => Ulid.Parse(value),
    new ConverterMappingHints(size: 26))
    {
    }
}