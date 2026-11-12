using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Finos.Rune.Runtime.Hashing;

/// <summary>
/// Recovers the Java enum constant name for a generated C# enum value.
/// </summary>
/// <remarks>
/// <para>
/// A global-key hash reduces an enum to Java's <c>name()</c>, and that string is recoverable from
/// neither of the two the generated C# would otherwise offer:
/// </para>
/// <list type="bullet">
/// <item>
/// The <b>member name</b> is <c>toFirstUpper</c> of the Rune value name, so
/// <c>TradeIdentifierTypeEnum.UniqueTransactionIdentifier</c> where Java has
/// <c>UNIQUE_TRANSACTION_IDENTIFIER</c>.
/// </item>
/// <item>
/// The <b>wire name</b> is the Rune <c>displayName</c> when one is declared, so
/// <c>DayCountFractionEnum.Act360</c> goes out as <c>"ACT/360"</c> — a different string again, and
/// for 1,736 of 4,440 generated values it differs from the member name.
/// </item>
/// </list>
/// <para>
/// So the generator emits a third map, <c>JavaNames</c>, on each enum's converter, filled by calling
/// rune-dsl's own <c>EnumHelper.formatEnumName</c>. This type reads it. Deriving the name here
/// instead — by re-running that transform over the C# member name — very nearly works, but
/// <c>formatEnumName</c> short-circuits on <c>^[A-Z0-9_]*$</c>, so a Rune value named <c>aBC</c>
/// (member <c>ABC</c>) would yield <c>"ABC"</c> where Java gives <c>"A_BC"</c>. No CDM 7 enum trips
/// that, which is exactly what makes it a bad thing to depend on across a CDM bump.
/// </para>
/// </remarks>
public static class RuneEnumNames
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<object, string>> Cache = new();

    /// <summary>The Java constant name for a boxed generated enum value.</summary>
    /// <exception cref="InvalidOperationException">
    /// The enum carries no generated <c>JavaNames</c> map, or the value is absent from it. This
    /// throws rather than falling back to the member name: a silently wrong constant name produces a
    /// plausible-looking hash that is simply not Java's, and every key containing it would be wrong
    /// with nothing to point at the cause.
    /// </exception>
    public static string JavaNameOf(object enumValue)
    {
        ArgumentNullException.ThrowIfNull(enumValue);
        Type type = enumValue.GetType();
        IReadOnlyDictionary<object, string> names = Cache.GetOrAdd(type, Load);
        if (!names.TryGetValue(enumValue, out string? name))
        {
            throw new InvalidOperationException(
                $"No Java constant name for {type.Name}.{enumValue} in its generated JavaNames map.");
        }

        return name;
    }

    private static IReadOnlyDictionary<object, string> Load(Type enumType)
    {
        Type converter = enumType.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
            ?? throw new InvalidOperationException(
                $"{enumType.Name} has no [JsonConverter], so its JavaNames map cannot be located. "
                + "Global-key hashing needs the generated enum converter.");

        FieldInfo field = converter.GetField("JavaNames", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"{converter.Name} has no static JavaNames map. Regenerate the model: the enum "
                + "generator emits it alongside WireNames.");

        var map = (IDictionary)field.GetValue(null)!;
        var result = new Dictionary<object, string>(map.Count);
        foreach (DictionaryEntry entry in map)
        {
            result[entry.Key] = (string)entry.Value!;
        }

        return result;
    }
}
