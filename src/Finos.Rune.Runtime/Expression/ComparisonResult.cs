using System.Globalization;
using Finos.Rune.Runtime.Mapper;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// Outcome of a Rune boolean expression: true, false, or empty, carrying the message explaining a
/// false. Mirrors com.rosetta.model.lib.expression.ComparisonResult.
/// </summary>
/// <remarks>
/// <para>Three-valued: <see cref="Get"/> returns null when every operand was absent. That is not
/// the same as false — <c>a &gt; b</c> with <c>a</c> absent is empty, and an empty condition
/// passes validation (<c>GetOrDefault(true)</c> at the call site).</para>
/// <para>The error strings are reproduced exactly, including the <c>" and "</c> joiner used by
/// both <see cref="AndNullSafe"/> and <see cref="OrNullSafe"/> — they are compared against the
/// Java implementation's output.</para>
/// <para>Java's deprecated <c>emptyOperand</c> overloads (<c>successEmptyOperand</c>,
/// <c>failureEmptyOperand</c>, <c>and</c>, <c>or</c>, <c>andIgnoreEmptyOperand</c>,
/// <c>orIgnoreEmptyOperand</c>, <c>of</c>) are not ported: the generator emits only the null-safe
/// forms.</para>
/// </remarks>
public sealed class ComparisonResult : IMapper<bool?>
{
    private static readonly IReadOnlyList<IMapperPath> NoPaths = [];

    private readonly bool? _result;

    private ComparisonResult(bool? result, string? error)
    {
        _result = result;
        Error = error;
    }

    /// <summary>A passing result.</summary>
    public static ComparisonResult Success() => new(true, null);

    /// <summary>A failing result carrying the reason.</summary>
    public static ComparisonResult Failure(string? error) => new(false, error);

    /// <summary>The empty result: every operand was absent.</summary>
    public static ComparisonResult OfEmpty() => new(null, null);

    /// <summary>
    /// Collapses a boolean mapper: empty when it holds nothing or only nulls, otherwise true iff
    /// every non-null value is true.
    /// </summary>
    public static ComparisonResult OfNullSafe(IMapper<bool?> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        IReadOnlyList<bool?> values = result.GetMulti();
        if (values.Count == 0 || values.All(v => v is null))
        {
            return OfEmpty();
        }
        return new ComparisonResult(values.Where(v => v is not null).All(v => v == true), null);
    }

    /// <summary>The reason for a false result, or null.</summary>
    public string? Error { get; }

    /// <inheritdoc />
    public bool? Get() => _result;

    /// <summary>The result, or <paramref name="defaultValue"/> when empty.</summary>
    public bool GetOrDefault(bool defaultValue) => _result ?? defaultValue;

    /// <summary>True when no operand carried a value.</summary>
    public bool IsEmptyOperand => _result is null;

    /// <summary>
    /// This result as a single-valued mapper, so a boolean expression can flow on into mapper
    /// operations.
    /// </summary>
    /// <remarks>
    /// Empty becomes <see cref="MapperS{T}.OfNull"/>, not a mapper holding null: the two differ in
    /// path and error reporting, and Java's <c>asMapper</c> makes the same distinction.
    /// </remarks>
    public MapperS<bool?> AsMapper() => IsEmptyOperand ? MapperS<bool?>.OfNull() : MapperS<bool?>.Of(Get());

    /// <summary>
    /// Conjunction. Empty only when both sides are empty; otherwise an empty side counts as false
    /// and contributes <c>"left of `and` operation is empty"</c> to the message.
    /// </summary>
    public ComparisonResult AndNullSafe(ComparisonResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmptyOperand && other.IsEmptyOperand)
        {
            return OfEmpty();
        }

        bool newResult = GetOrDefault(false) && other.GetOrDefault(false);
        string newError = string.Empty;
        if (!GetOrDefault(false))
        {
            newError += Error ?? string.Format(
                CultureInfo.InvariantCulture, "left of `and` operation is {0}", DescribeOperand(this));
        }
        if (!other.GetOrDefault(false))
        {
            if (!GetOrDefault(false))
            {
                newError += " and ";
            }
            newError += other.Error ?? string.Format(
                CultureInfo.InvariantCulture, "right of `and` operation is {0}", DescribeOperand(other));
        }
        return new ComparisonResult(newResult, newError);
    }

    /// <summary>
    /// Disjunction. Empty only when both sides are empty. On failure the message concatenates both
    /// sides' errors with <c>" and "</c> — matching Java, which does not special-case a null error
    /// here, so a null side renders as the empty string.
    /// </summary>
    public ComparisonResult OrNullSafe(ComparisonResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmptyOperand && other.IsEmptyOperand)
        {
            return OfEmpty();
        }

        bool newResult = GetOrDefault(false) || other.GetOrDefault(false);
        string newError = JavaStringOf(Error) + " and " + JavaStringOf(other.Error);
        return new ComparisonResult(newResult, newResult ? null : newError);
    }

    private static string DescribeOperand(ComparisonResult result) =>
        result.IsEmptyOperand ? "empty" : JavaBool(result.GetOrDefault(false));

    private static string JavaStringOf(string? value) => value ?? "null";

    private static string JavaBool(bool value) => value ? "true" : "false";

    /// <inheritdoc />
    public IReadOnlyList<bool?> GetMulti() => [Get()];

    /// <inheritdoc />
    public int ResultCount => IsEmptyOperand ? 0 : 1;

    /// <inheritdoc />
    public IReadOnlyList<IMapperPath> Paths => NoPaths;

    /// <inheritdoc />
    public IReadOnlyList<IMapperPath> ErrorPaths => NoPaths;

    /// <inheritdoc />
    public IReadOnlyList<string?> Errors => [Error];
}
