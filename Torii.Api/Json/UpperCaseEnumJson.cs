using System.Text.Json;
using System.Text.Json.Serialization;

namespace Torii.Api.Json;

/// <summary>
/// Politique de nommage MAJUSCULE, identique à <c>UpperCaseEnumConverter</c> côté base :
/// une valeur d'enum a ainsi la même représentation en JSON et en DB.
/// </summary>
public sealed class UpperCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => name.ToUpperInvariant();
}

public static class UpperCaseEnumJson
{
    /// <summary>
    /// Sérialise les enums en chaîne MAJUSCULE (ex. <c>"ON_GOING"</c>). Les valeurs
    /// numériques sont refusées en entrée : le client doit envoyer le nom.
    /// </summary>
    public static JsonSerializerOptions UseUpperCaseEnumStrings(this JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(new UpperCaseNamingPolicy(), allowIntegerValues: false));
        return options;
    }
}
