using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// Natural ordering of Rune values, matching Java's <c>Comparable.compareTo</c>.
/// Mirrors com.rosetta.model.lib.expression.CompareHelper.
/// </summary>
/// <remarks>
/// <para>Numbers compare scale-blind, as <c>BigDecimal.compareTo</c>. Strings compare
/// <b>ordinally</b>, as <c>String.compareTo</c> — <c>Comparer&lt;string&gt;.Default</c> is
/// culture-sensitive and would order <c>"B"</c> before <c>"a"</c> differently.</para>
/// <para>Enums compare by ordinal, as Java's <c>Enum.compareTo</c>, which is declaration order
/// and therefore the order the Rune model declares.</para>
/// </remarks>
public static class RuneComparer
{
    /// <summary>Compares two values of the same Rune type. Nulls sort first.</summary>
    public static int Compare(object? left, object? right)
    {
        if (left is null)
        {
            return right is null ? 0 : -1;
        }
        if (right is null)
        {
            return 1;
        }
        if (left is string leftText && right is string rightText)
        {
            return string.CompareOrdinal(leftText, rightText);
        }
        if (TryCompareNumbers(left, right, out int numericComparison))
        {
            return numericComparison;
        }
        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }
        throw new ArgumentException($"{left.GetType()} is not comparable.", nameof(left));
    }

    /// <summary>Compares two values, boxing through <see cref="Compare(object?, object?)"/>.</summary>
    public static int Compare<T>(T? left, T? right) => Compare((object?)left, (object?)right);

    /// <summary>
    /// Widens any Rune numeric to <see cref="decimal"/>, so an <c>int</c> and a <c>number</c>
    /// compare without going through binary floating point.
    /// </summary>
    public static bool TryAsNumber(object value, out decimal number)
    {
        switch (value)
        {
            case decimal dec:
                number = dec;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case short s:
                number = s;
                return true;
            case System.Numerics.BigInteger big
                when big >= new System.Numerics.BigInteger(decimal.MinValue)
                    && big <= new System.Numerics.BigInteger(decimal.MaxValue):
                number = checked((decimal)big);
                return true;
            default:
                number = 0m;
                return false;
        }
    }

    private static bool TryCompareNumbers(object left, object right, out int result)
    {
        if (left is decimal leftDecimal)
        {
            if (right is decimal rightDecimal)
            {
                result = decimal.Compare(leftDecimal, rightDecimal);
                return true;
            }
            if (TryAsInteger(right, out System.Numerics.BigInteger rightInteger))
            {
                result = -RuneDecimal.Compare(rightInteger, leftDecimal);
                return true;
            }
        }
        if (TryAsInteger(left, out System.Numerics.BigInteger leftInteger))
        {
            if (right is decimal rightDecimal)
            {
                result = RuneDecimal.Compare(leftInteger, rightDecimal);
                return true;
            }
            if (TryAsInteger(right, out System.Numerics.BigInteger rightInteger))
            {
                result = leftInteger.CompareTo(rightInteger);
                return true;
            }
        }
        result = 0;
        return false;
    }

    private static bool TryAsInteger(object value, out System.Numerics.BigInteger integer)
    {
        switch (value)
        {
            case System.Numerics.BigInteger big:
                integer = big;
                return true;
            case long l:
                integer = l;
                return true;
            case int i:
                integer = i;
                return true;
            case short s:
                integer = s;
                return true;
            default:
                integer = System.Numerics.BigInteger.Zero;
                return false;
        }
    }

    /// <summary>
    /// Renders a value the way Java string concatenation would, for failure messages.
    /// </summary>
    /// <remarks>
    /// <para>A null renders as the four characters <c>null</c>, a bool as <c>true</c>/<c>false</c>, and
    /// a list as <c>[a, b]</c> — Java's <c>AbstractCollection.toString</c>. Formatting is forced to
    /// the invariant culture: measured under <c>tr-TR</c>, <c>1.5</c> renders as <c>1,5</c>.</para>
    /// <para>Dates and times are ISO-8601, matching what Java's <c>Date</c> and <c>java.time</c> types
    /// print. The invariant culture is <em>not</em> ISO for them — it renders a date as
    /// <c>12/14/1994</c> where Java gives <c>1994-12-14</c>, which showed up as 27 differing
    /// validation messages once everything else matched.</para>
    /// <para>An enum renders as its Rune display name, not its C# member name. Java's generated enums
    /// override <c>toString()</c> to return the display name, and the two differ wherever a Rune name
    /// is not a valid identifier — <c>RollConventionEnum._8</c> against Java's <c>8</c>.</para>
    /// </remarks>
    public static string Describe(object? value) => value switch
    {
        null => "null",
        string text => text,
        bool flag => flag ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => DescribeTime(time),
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        DateTimeOffset zoned =>
            zoned.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
        Enum enumValue => DescribeEnum(enumValue),
        System.Collections.IEnumerable sequence =>
            "[" + string.Join(", ", sequence.Cast<object?>().Select(Describe)) + "]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    private static string DescribeTime(TimeOnly time) =>
        time.Second == 0 && time.Millisecond == 0 && time.Microsecond == 0 && time.Nanosecond == 0
            ? time.ToString("HH\\:mm", CultureInfo.InvariantCulture)
            : time.ToString("HH\\:mm\\:ss.FFFFFFF", CultureInfo.InvariantCulture);

    /// <summary>
    /// Per-enum-type display names, discovered from the generated converter and cached.
    /// </summary>
    /// <remarks>
    /// Reflective by convention — <c>&lt;EnumName&gt;Converter.WireNames</c>, beside the enum — because
    /// this method receives a boxed value and cannot name the generated map statically the way a
    /// generated call site can. Falling back to the member name on a miss keeps a message readable if
    /// the convention ever moves; the parity sweep is what would notice.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, Dictionary<string, string>> DisplayNames = new();

    private static string DescribeEnum(Enum value)
    {
        Dictionary<string, string> names = DisplayNames.GetOrAdd(value.GetType(), FindDisplayNames);
        string memberName = value.ToString();
        return names.TryGetValue(memberName, out string? displayName) ? displayName : memberName;
    }

    private static Dictionary<string, string> FindDisplayNames(Type enumType)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        FieldInfo? field = enumType.Assembly
                .GetType(enumType.FullName + "Converter")
                ?.GetField("WireNames", BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is System.Collections.IEnumerable pairs)
        {
            foreach (object pair in pairs)
            {
                Type pairType = pair.GetType();
                object? key = pairType.GetProperty("Key")?.GetValue(pair);
                if (key != null && pairType.GetProperty("Value")?.GetValue(pair) is string wire)
                {
                    result[key.ToString()!] = wire;
                }
            }
        }
        return result;
    }
}
