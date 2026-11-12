using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Base System.Text.Json converter for generated Rune enums. Generated code derives a
/// converter per enum and supplies explicit bidirectional wire-name maps (built from the
/// Rune value names and displayName annotations) — no reflection is involved.
/// </summary>
public abstract class RuneEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly IReadOnlyDictionary<TEnum, string> _toWire;
    private readonly IReadOnlyDictionary<string, TEnum> _fromWire;

    protected RuneEnumConverter(IReadOnlyDictionary<TEnum, string> toWire)
    {
        _toWire = toWire;
        var fromWire = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (var pair in toWire)
        {
            fromWire[pair.Value] = pair.Key;
        }
        _fromWire = fromWire;
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                $"Expected a string for enum {typeof(TEnum).Name} but found {reader.TokenType}.");
        }
        string value = reader.GetString()!;
        if (!_fromWire.TryGetValue(value, out TEnum result))
        {
            throw new JsonException($"Unknown value \"{value}\" for enum {typeof(TEnum).Name}.");
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!_toWire.TryGetValue(value, out string? wireName))
        {
            throw new JsonException($"Enum value {value} of {typeof(TEnum).Name} has no wire name.");
        }
        writer.WriteStringValue(wireName);
    }
}
