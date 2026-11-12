using System.Globalization;
using System.Text.RegularExpressions;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// Free functions the generated validators call.
/// Mirrors com.rosetta.model.lib.expression.ExpressionOperatorsNullSafe.
/// </summary>
/// <remarks>
/// <para>Only the structural checks are here — cardinality and the string/number type-format
/// constraints. The comparison and list operators arrive with the expression compiler.</para>
/// <para>Every failure message is reproduced character for character. They are compared against
/// the Java implementation's output over the whole CDM test pack, and CDM's own
/// <c>ModelClassValidationTest</c> pins the required-field wording. Do not reword them.</para>
/// </remarks>
public static class RuneOperators
{
    /// <summary>
    /// Checks an attribute's value count against its declared cardinality. A <paramref name="max"/>
    /// of 0 means unbounded, matching the generator, which passes <c>0</c> when there is no upper
    /// bound.
    /// </summary>
    public static ComparisonResult CheckCardinality(string msgPrefix, int actual, int min, int max)
    {
        if (actual < min)
        {
            if (actual == 0)
            {
                return ComparisonResult.Failure(
                    "'" + msgPrefix + "' is a required field but does not exist.");
            }
            return ComparisonResult.Failure(
                "Minimum of " + Int(min) + " '" + msgPrefix + "' is expected but found " + Int(actual) + ".");
        }
        if (max > 0 && actual > max)
        {
            return ComparisonResult.Failure(
                "Maximum of " + Int(max) + " '" + msgPrefix + "' are expected but found " + Int(actual) + ".");
        }
        return ComparisonResult.Success();
    }

    /// <summary>
    /// Checks a string against its declared length and pattern constraints. A null value passes:
    /// absence is the cardinality check's business, not this one.
    /// </summary>
    public static ComparisonResult CheckString(
        string msgPrefix, string? value, int minLength, int? maxLength, Regex? pattern)
    {
        if (value == null)
        {
            return ComparisonResult.Success();
        }
        var failures = new List<string>();
        if (value.Length < minLength)
        {
            failures.Add("Field '" + msgPrefix + "' requires a value with minimum length of "
                + Int(minLength) + " characters but value '" + value + "' has length of "
                + Int(value.Length) + " characters.");
        }
        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            failures.Add("Field '" + msgPrefix + "' must have a value with maximum length of "
                + Int(maxLength.Value) + " characters but value '" + value + "' has length of "
                + Int(value.Length) + " characters.");
        }
        if (pattern != null && !IsFullMatch(pattern, value))
        {
            failures.Add("Field '" + msgPrefix + "' with value '" + value
                + "' does not match the pattern /" + pattern + "/.");
        }
        if (failures.Count == 0)
        {
            return ComparisonResult.Success();
        }
        return ComparisonResult.Failure(string.Join(" ", failures));
    }

    /// <summary>Checks every element of a list; failures are joined with <c>" - "</c>.</summary>
    public static ComparisonResult CheckString(
        string msgPrefix, IReadOnlyList<string?>? values, int minLength, int? maxLength, Regex? pattern)
    {
        if (values == null)
        {
            return ComparisonResult.Success();
        }
        var failures = new List<string>();
        foreach (string? value in values)
        {
            ComparisonResult result = CheckString(msgPrefix, value, minLength, maxLength, pattern);
            if (result.Get() != true)
            {
                failures.Add(result.Error ?? string.Empty);
            }
        }
        if (failures.Count == 0)
        {
            return ComparisonResult.Success();
        }
        return ComparisonResult.Failure(string.Join(" - ", failures));
    }

    /// <summary>
    /// Checks a number against its declared digit and range constraints. A null value passes.
    /// </summary>
    public static ComparisonResult CheckNumber(
        string msgPrefix, decimal? value, int? digits, int? fractionalDigits, decimal? min, decimal? max)
    {
        if (!value.HasValue)
        {
            return ComparisonResult.Success();
        }
        decimal actualValue = value.Value;
        var failures = new List<string>();

        if (digits.HasValue)
        {
            int actual = RuneDecimal.CountDigits(actualValue);
            if (actual > digits.Value)
            {
                failures.Add("Expected a maximum of " + Int(digits.Value) + " digits for '" + msgPrefix
                    + "', but the number " + Num(actualValue) + " has " + Int(actual) + ".");
            }
        }
        if (fractionalDigits.HasValue)
        {
            int actual = RuneDecimal.CountFractionalDigits(actualValue);
            if (actual > fractionalDigits.Value)
            {
                failures.Add("Expected a maximum of " + Int(fractionalDigits.Value)
                    + " fractional digits for '" + msgPrefix + "', but the number " + Num(actualValue)
                    + " has " + Int(actual) + ".");
            }
        }
        if (min.HasValue && actualValue < min.Value)
        {
            failures.Add("Expected a number greater than or equal to " + Num(min.Value) + " for '"
                + msgPrefix + "', but found " + Num(actualValue) + ".");
        }
        if (max.HasValue && actualValue > max.Value)
        {
            failures.Add("Expected a number less than or equal to " + Num(max.Value) + " for '"
                + msgPrefix + "', but found " + Num(actualValue) + ".");
        }

        if (failures.Count == 0)
        {
            return ComparisonResult.Success();
        }
        return ComparisonResult.Failure(string.Join(" ", failures));
    }

    /// <summary>Checks a nullable int against number constraints, widening to decimal first.</summary>
    public static ComparisonResult CheckNumber(
        string msgPrefix, int? value, int? digits, int? fractionalDigits, decimal? min, decimal? max) =>
        CheckNumber(msgPrefix, value.HasValue ? value.Value : (decimal?)null, digits, fractionalDigits, min, max);

    /// <summary>Checks every element of a list; failures are joined with <c>" - "</c>.</summary>
    public static ComparisonResult CheckNumber(
        string msgPrefix, IReadOnlyList<decimal?>? values, int? digits, int? fractionalDigits,
        decimal? min, decimal? max)
    {
        if (values == null)
        {
            return ComparisonResult.Success();
        }
        var failures = new List<string>();
        foreach (decimal? value in values)
        {
            ComparisonResult result = CheckNumber(msgPrefix, value, digits, fractionalDigits, min, max);
            if (result.Get() != true)
            {
                failures.Add(result.Error ?? string.Empty);
            }
        }
        if (failures.Count == 0)
        {
            return ComparisonResult.Success();
        }
        return ComparisonResult.Failure(string.Join(" - ", failures));
    }

    /// <summary>Checks every element of an int list; failures are joined with <c>" - "</c>.</summary>
    public static ComparisonResult CheckNumber(
        string msgPrefix, IReadOnlyList<int?>? values, int? digits, int? fractionalDigits,
        decimal? min, decimal? max)
    {
        if (values == null)
        {
            return ComparisonResult.Success();
        }
        var widened = new List<decimal?>(values.Count);
        foreach (int? value in values)
        {
            widened.Add(value.HasValue ? value.Value : (decimal?)null);
        }
        return CheckNumber(msgPrefix, widened, digits, fractionalDigits, min, max);
    }

    // Generated code passes non-nullable lists, which do not convert to nullable-element lists.

    /// <summary>Checks every element of a non-nullable decimal list.</summary>
    public static ComparisonResult CheckNumber(
        string msgPrefix, IReadOnlyList<decimal>? values, int? digits, int? fractionalDigits,
        decimal? min, decimal? max)
    {
        if (values == null)
        {
            return ComparisonResult.Success();
        }
        var widened = new List<decimal?>(values.Count);
        foreach (decimal value in values)
        {
            widened.Add(value);
        }
        return CheckNumber(msgPrefix, widened, digits, fractionalDigits, min, max);
    }

    /// <summary>Checks every element of a non-nullable int list.</summary>
    public static ComparisonResult CheckNumber(
        string msgPrefix, IReadOnlyList<int>? values, int? digits, int? fractionalDigits,
        decimal? min, decimal? max)
    {
        if (values == null)
        {
            return ComparisonResult.Success();
        }
        var widened = new List<decimal?>(values.Count);
        foreach (int value in values)
        {
            widened.Add(value);
        }
        return CheckNumber(msgPrefix, widened, digits, fractionalDigits, min, max);
    }

    /// <summary>
    /// Java's <c>Matcher.matches()</c> requires the whole input to match, and lets the engine
    /// backtrack to find an alternative that does. <c>Regex.IsMatch</c> is a search, and checking
    /// the leftmost match's index and length is not equivalent — for <c>a|ab</c> against
    /// <c>"ab"</c> the leftmost match is <c>"a"</c>, so a length check would reject an input Java
    /// accepts. Anchoring the pattern is the only faithful form.
    /// </summary>
    /// <remarks>
    /// The anchored copy is cached per source regex: the raw one is still needed verbatim for the
    /// failure message, so the anchors must not be baked into the emitted pattern.
    /// <c>\A(?:...)\z</c> rather than <c>^...$</c> because the latter is line-anchored under
    /// <see cref="RegexOptions.Multiline"/> and <c>$</c> also matches before a trailing newline.
    /// </remarks>
    private static bool IsFullMatch(Regex pattern, string value) =>
        AnchoredPatterns.GetValue(pattern, static p =>
            new Regex(@"\A(?:" + p.ToString() + @")\z", p.Options)).IsMatch(value);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Regex, Regex> AnchoredPatterns =
        new();

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Num(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
