namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Maps a generated enum to and from its XML wire aliases. The map is supplied explicitly by
/// generated code — there is no reflection over enum members and no attribute lookup.
/// </summary>
public sealed class XmlEnumCodec<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<TEnum, string> _toWire;
    private readonly Dictionary<string, TEnum> _fromWire;
    private readonly Dictionary<string, TEnum> _byMemberName;

    public XmlEnumCodec(IReadOnlyDictionary<TEnum, string> wireNames)
    {
        ArgumentNullException.ThrowIfNull(wireNames);
        _toWire = new Dictionary<TEnum, string>();
        _fromWire = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (KeyValuePair<TEnum, string> entry in wireNames)
        {
            _toWire[entry.Key] = entry.Value;
            _fromWire[entry.Value] = entry.Key;
        }
        _byMemberName = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        foreach (TEnum member in Enum.GetValues<TEnum>())
        {
            if (_toWire.ContainsKey(member))
            {
                _byMemberName[member.ToString()] = member;
            }
        }
    }

    /// <summary>The alias written for <paramref name="value"/>.</summary>
    public string Format(TEnum value) => _toWire.TryGetValue(value, out string? wire)
        ? wire
        : throw new InvalidOperationException(
            "No XML alias is registered for " + typeof(TEnum).Name + "." + value + ".");

    /// <summary>The alias written for a nullable value, or null.</summary>
    public string? Format(TEnum? value) => value.HasValue ? Format(value.Value) : null;

    /// <summary>
    /// Reads a declared wire alias, or failing that the exact name of a declared enum member.
    /// </summary>
    /// <remarks>
    /// Matching is exact and ordinal against two explicit tables. It deliberately does not go
    /// through <c>Enum.TryParse</c>, which would also accept a numeric ordinal (<c>"0"</c>), a
    /// signed ordinal (<c>"-1"</c>), surrounding whitespace and a comma-separated flag list — none
    /// of which is a value any Rune enum can legitimately carry on the wire, and all of which would
    /// silently resolve to a member that the document never named.
    /// </remarks>
    public bool TryParse(string? text, out TEnum value)
    {
        if (text != null)
        {
            if (_fromWire.TryGetValue(text, out value))
            {
                return true;
            }
            if (_byMemberName.TryGetValue(text, out value))
            {
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>Reads an alias back, throwing on an unknown one.</summary>
    public TEnum Parse(string? text) => TryParse(text, out TEnum value)
        ? value
        : throw new FormatException("'" + text + "' is not a known XML alias of " + typeof(TEnum).Name + ".");
}
