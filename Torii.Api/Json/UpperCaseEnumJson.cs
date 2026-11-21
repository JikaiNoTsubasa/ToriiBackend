using System.Text.Json;
using System.Text.Json.Serialization;

namespace Torii.Api.Json;

/// <summary>
/// UPPERCASE naming policy, identical to <c>UpperCaseEnumConverter</c> on the database side:
/// an enum value has the same representation in JSON and in the DB.
/// </summary>
public sealed class UpperCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => name.ToUpperInvariant();
}

public static class UpperCaseEnumJson
{
    /// <summary>
    /// Serializes enums as UPPERCASE strings (e.g. <c>"ON_GOING"</c>). Numeric values
    /// are rejected on input: the client must send the name.
    /// </summary>
    public static JsonSerializerOptions UseUpperCaseEnumStrings(this JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(new UpperCaseNamingPolicy(), allowIntegerValues: false));
        return options;
    }
}
