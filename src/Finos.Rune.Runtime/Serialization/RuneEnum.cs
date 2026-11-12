using System.Collections.Concurrent;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Display-name conversion for generated Rune enums, backing the <c>to-enum</c> and
/// <c>to-string</c> operators.
/// </summary>
/// <remarks>
/// <para>Reads the same generated <c>WireNames</c> map the JSON converter uses, rather than a
/// second table: the Rune display name and the wire name are the same string, and two tables would
/// be two things to keep in step.</para>
/// <para>Failure is an <see cref="ArgumentException"/> to match Java's
/// <c>IllegalArgumentException</c> — the expression tier catches that exact type and reports the
/// conversion as absent rather than fatal.</para>
/// </remarks>
public static class RuneEnum
{
    private static readonly ConcurrentDictionary<Type, object> ReverseMaps = new();

    /// <summary>The display name of an enum value.</summary>
    public static string ToDisplayName<TEnum>(IReadOnlyDictionary<TEnum, string> wireNames, TEnum value)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(wireNames);
        if (!wireNames.TryGetValue(value, out string? displayName))
        {
            throw new ArgumentException($"Enum value {value} of {typeof(TEnum).Name} has no display name.");
        }
        return displayName;
    }

    /// <summary>The enum value with the given display name.</summary>
    public static TEnum FromDisplayName<TEnum>(IReadOnlyDictionary<TEnum, string> wireNames, string displayName)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(wireNames);
        Dictionary<string, TEnum> reverse = Reverse(wireNames);
        if (!reverse.TryGetValue(displayName, out TEnum value))
        {
            throw new ArgumentException($"Unknown display name \"{displayName}\" for enum {typeof(TEnum).Name}.");
        }
        return value;
    }

    private static Dictionary<string, TEnum> Reverse<TEnum>(IReadOnlyDictionary<TEnum, string> wireNames)
        where TEnum : struct, Enum =>
        (Dictionary<string, TEnum>)ReverseMaps.GetOrAdd(typeof(TEnum), _ =>
        {
            var reverse = new Dictionary<string, TEnum>(StringComparer.Ordinal);
            foreach (KeyValuePair<TEnum, string> pair in wireNames)
            {
                reverse[pair.Value] = pair.Key;
            }
            return reverse;
        });
}
