using Finos.Rune.Runtime.Mapper;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// The ordering operators generated expression code calls.
/// Mirrors com.rosetta.model.lib.expression.ExpressionCompareUtilNullSafe.
/// </summary>
/// <remarks>
/// <para>The right-hand side is first REDUCED to a single value, and which value depends on the
/// quantifier: for <c>All</c> the reduction picks the bound that makes the test hardest, for
/// <c>Any</c> the one that makes it easiest. So <c>a &gt; b</c> with <c>All</c> compares every
/// left value against the largest right value.</para>
/// <para>An absent operand is a hard FAILURE here, not an empty result — the null-safe compare
/// calls <c>failure(...)</c>, so <c>IsEmptyOperand</c> is false and <c>GetOrDefault(true)</c>
/// yields false. Emptiness only enters an expression through
/// <see cref="ComparisonResult.OfNullSafe"/>, which wraps raw boolean mappers and never these.</para>
/// </remarks>
public static class RuneCompare
{
    /// <summary>Left greater than right.</summary>
    public static ComparisonResult GreaterThan<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o) =>
        Compare(m1, m2, o, PickLargerFor(o), c => c > 0, ">");

    /// <summary>Left greater than or equal to right.</summary>
    public static ComparisonResult GreaterThanEquals<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o) =>
        Compare(m1, m2, o, PickLargerFor(o), c => c >= 0, ">=");

    /// <summary>Left less than right.</summary>
    public static ComparisonResult LessThan<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o) =>
        Compare(m1, m2, o, PickSmallerFor(o), c => c < 0, "<");

    /// <summary>Left less than or equal to right.</summary>
    public static ComparisonResult LessThanEquals<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o) =>
        Compare(m1, m2, o, PickSmallerFor(o), c => c <= 0, "<=");

    /// <summary>For a &gt; test: All reduces the right side to its largest, Any to its smallest.</summary>
    private static bool PickLargerFor(CardinalityOperator o) => o == CardinalityOperator.All;

    /// <summary>For a &lt; test: All reduces the right side to its smallest, Any to its largest.</summary>
    private static bool PickSmallerFor(CardinalityOperator o) => o != CardinalityOperator.All;

    private static ComparisonResult Compare<T, TOther>(
        IMapper<T> m1,
        IMapper<TOther> m2,
        CardinalityOperator o,
        bool reduceToLarger,
        Func<int, bool> accept,
        string comparatorString)
    {
        if (m1 is null || m2 is null)
        {
            return ComparisonResult.Failure(
                $"Null operand: [{RuneComparer.Describe(m1)}] {comparatorString} [{RuneComparer.Describe(m2)}]");
        }
        if (m1.ResultCount == 0 || m2.ResultCount == 0)
        {
            return ComparisonResult.Failure(
                $"Null operand: [{RuneExpr.Describe(m1.Paths)} : {RuneComparer.Describe(m1.Get())}]"
                + $" {comparatorString} "
                + $"[{RuneExpr.Describe(m2.Paths)} : {RuneComparer.Describe(m2.Get())}]");
        }

        IReadOnlyList<TOther> rightValues = m2.GetMulti();
        TOther bound = rightValues[0];
        for (int i = 1; i < rightValues.Count; i++)
        {
            int comparison = RuneComparer.Compare(rightValues[i], bound);
            if (reduceToLarger ? comparison > 0 : comparison < 0)
            {
                bound = rightValues[i];
            }
        }

        IReadOnlyList<T> leftValues = m1.GetMulti();
        bool result = o == CardinalityOperator.All
            ? leftValues.All(a => accept(RuneComparer.Compare(a, bound)))
            : leftValues.Any(a => accept(RuneComparer.Compare(a, bound)));

        if (result)
        {
            return ComparisonResult.Success();
        }
        return ComparisonResult.Failure(
            $"all elements of paths {RuneExpr.Describe(m1.Paths)} values {RuneComparer.Describe(leftValues)} "
            + $"are not {comparatorString} than "
            + $"all elements of paths {RuneExpr.Describe(m2.Paths)} values {RuneComparer.Describe(rightValues)}");
    }
}
