using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Torii.Api.Data;

/// <summary>
/// Stores an enum as an UPPERCASE string (e.g. <c>InProgress</c> → <c>INPROGRESS</c>,
/// <c>ON_GOING</c> → <c>ON_GOING</c>). Reading back is case-insensitive.
/// </summary>
public sealed class UpperCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => value.ToString().ToUpperInvariant(),
    value => Enum.Parse<TEnum>(value, true))
    where TEnum : struct, Enum;

public static class EnumConversionExtensions
{
    /// <summary>
    /// Applies <see cref="UpperCaseEnumConverter{TEnum}"/> to every enum property
    /// (nullable ones included) of the model. Call it at the end of <c>OnModelCreating</c>, once
    /// every entity is declared. An explicitly configured conversion is kept.
    /// </summary>
    public static ModelBuilder UseUpperCaseEnumStrings(this ModelBuilder modelBuilder)
    {
        var properties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.GetValueConverter() is null);

        foreach (var property in properties)
        {
            var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (!enumType.IsEnum)
                continue;

            var converterType = typeof(UpperCaseEnumConverter<>).MakeGenericType(enumType);
            property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
        }

        return modelBuilder;
    }
}
