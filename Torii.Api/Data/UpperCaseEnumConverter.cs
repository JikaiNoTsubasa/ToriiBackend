using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Torii.Api.Data;

/// <summary>
/// Stocke un enum sous forme de chaîne en MAJUSCULES (ex. <c>InProgress</c> → <c>INPROGRESS</c>,
/// <c>ON_GOING</c> → <c>ON_GOING</c>). La relecture est insensible à la casse.
/// </summary>
public sealed class UpperCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => value.ToString().ToUpperInvariant(),
    value => Enum.Parse<TEnum>(value, true))
    where TEnum : struct, Enum;

public static class EnumConversionExtensions
{
    /// <summary>
    /// Applique <see cref="UpperCaseEnumConverter{TEnum}"/> à toutes les propriétés enum
    /// (nullables comprises) du modèle. À appeler en fin de <c>OnModelCreating</c>, une fois
    /// toutes les entités déclarées. Une conversion explicite déjà configurée est conservée.
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
