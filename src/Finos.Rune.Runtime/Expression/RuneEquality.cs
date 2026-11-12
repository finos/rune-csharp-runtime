using Finos.Rune.Runtime.Mapper;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// The equality operators. Mirrors com.rosetta.model.lib.expression.ExpressionEqualityUtilNullSafe.
/// </summary>
/// <remarks>
/// <para>Dispatch is on SHAPE, not element type. Java tests <c>m1.getClass() == m2.getClass()</c>,
/// which erasure makes a shape test; C# generics are reified, so the same test would distinguish
/// <c>MapperS&lt;int?&gt;</c> from <c>MapperS&lt;decimal?&gt;</c> and take the wrong branch. The
/// non-generic <see cref="IMapperSingle"/>/<see cref="IMapperCollection"/> markers carry the shape
/// instead, and nothing is ever downcast to a constructed generic.</para>
/// <para>Numbers compare scale-BLIND here (via compareTo), unlike <c>contains</c> and
/// <c>disjoint</c>, which use equals. So <c>1.0 = 1.00</c> is true while a list of <c>1.0</c> does
/// not contain <c>1.00</c>.</para>
/// <para>The empty-operand rules are asymmetric and deliberately so: equality on two empties
/// succeeds, equality with one empty fails "cannot be compared to", and inequality with either
/// empty succeeds with a <b>null</b> error string.</para>
/// </remarks>
public static class RuneEquality
{
    /// <summary>Rune <c>=</c>. Defaults to <see cref="CardinalityOperator.All"/> at the call site.</summary>
    public static ComparisonResult AreEqual<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o)
    {
        ArgumentNullException.ThrowIfNull(m1);
        ArgumentNullException.ThrowIfNull(m2);
        bool leftSingle = m1 is IMapperSingle;
        bool rightSingle = m2 is IMapperSingle;
        if (leftSingle == rightSingle)
        {
            return AreEqualSame(m1, m2, o, useInstantArm: true);
        }
        return leftSingle
            ? AreEqualDifferent(m2, m1, o)
            : AreEqualDifferent(m1, m2, o);
    }

    /// <summary>Rune <c>&lt;&gt;</c>. Defaults to <see cref="CardinalityOperator.Any"/> at the call site.</summary>
    public static ComparisonResult NotEqual<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o)
    {
        ArgumentNullException.ThrowIfNull(m1);
        ArgumentNullException.ThrowIfNull(m2);
        bool leftSingle = m1 is IMapperSingle;
        bool rightSingle = m2 is IMapperSingle;
        if (leftSingle == rightSingle)
        {
            return NotEqualSame(m1, m2, o);
        }
        return leftSingle
            ? NotEqualDifferent(m2, m1, o)
            : NotEqualDifferent(m1, m2, o);
    }

    private static ComparisonResult AreEqualSame<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o, bool useInstantArm)
    {
        IReadOnlyList<T> left = m1.GetMulti();
        IReadOnlyList<TOther> right = m2.GetMulti();

        if (left.Count == 0 || right.Count == 0)
        {
            return left.Count == 0 && right.Count == 0
                ? ComparisonResult.Success()
                : CannotCompare(m1, m2);
        }

        int shared = Math.Min(left.Count, right.Count);
        for (int i = 0; i < shared; i++)
        {
            bool equal = ValuesEqual(left[i], right[i], useInstantArm);
            if (!equal && o == CardinalityOperator.All)
            {
                return DoesNotEqual(m1, m2);
            }
            if (equal && o == CardinalityOperator.Any)
            {
                return ComparisonResult.Success();
            }
        }

        if (left.Count != right.Count)
        {
            return CannotCompare(m1, m2);
        }
        return o == CardinalityOperator.All ? ComparisonResult.Success() : DoesNotEqual(m1, m2);
    }

    private static ComparisonResult AreEqualDifferent<TMany, TOne>(
        IMapper<TMany> many, IMapper<TOne> one, CardinalityOperator o)
    {
        IReadOnlyList<TMany> values = many.GetMulti();
        TOne single = one.Get();

        if (values.Count == 0)
        {
            return CannotCompare(many, one);
        }
        foreach (TMany value in values)
        {
            // Java compares only numbers here, so equal instants at different offsets are unequal.
            bool equal = ValuesEqual(value, single, useInstantArm: false);
            if (!equal && o == CardinalityOperator.All)
            {
                return DoesNotEqual(many, one);
            }
            if (equal && o == CardinalityOperator.Any)
            {
                return ComparisonResult.Success();
            }
        }
        return o == CardinalityOperator.All ? ComparisonResult.Success() : DoesNotEqual(many, one);
    }

    private static ComparisonResult NotEqualSame<T, TOther>(
        IMapper<T> m1, IMapper<TOther> m2, CardinalityOperator o)
    {
        if (m1.ResultCount == 0 || m2.ResultCount == 0)
        {
            return ComparisonResult.Success();
        }
        // Java compares only numbers here, so equal instants at different offsets are unequal.
        ComparisonResult equal = AreEqualSame(m1, m2, Invert(o), useInstantArm: false);
        return Negate(equal, m1, m2);
    }

    private static ComparisonResult NotEqualDifferent<TMany, TOne>(
        IMapper<TMany> many, IMapper<TOne> one, CardinalityOperator o)
    {
        if (many.ResultCount == 0)
        {
            return ComparisonResult.Success();
        }
        ComparisonResult equal = AreEqualDifferent(many, one, Invert(o));
        return Negate(equal, many, one);
    }

    private static ComparisonResult Negate<T, TOther>(
        ComparisonResult equal, IMapper<T> m1, IMapper<TOther> m2) =>
        equal.Get() == true ? ShouldNotEqual(m1, m2) : ComparisonResult.Success();

    private static CardinalityOperator Invert(CardinalityOperator o) =>
        o == CardinalityOperator.All ? CardinalityOperator.Any : CardinalityOperator.All;

    /// <summary>
    /// Numbers compare by magnitude, so scale is ignored. Zoned date-times compare by instant, but
    /// only when both mappers have the same shape — Java's cross-shape path omits that arm.
    /// </summary>
    private static bool ValuesEqual(object? left, object? right, bool useInstantArm)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        if (RuneComparer.TryAsNumber(left, out decimal leftNumber)
            && RuneComparer.TryAsNumber(right, out decimal rightNumber))
        {
            return leftNumber == rightNumber;
        }
        if (left is DateTimeOffset leftZoned && right is DateTimeOffset rightZoned)
        {
            // DateTimeOffset.Equals compares instants; Java's equals also compares the offset.
            return useInstantArm
                ? leftZoned.UtcDateTime == rightZoned.UtcDateTime
                : leftZoned.EqualsExact(rightZoned);
        }
        return left.Equals(right);
    }

    private static ComparisonResult CannotCompare<T, TOther>(IMapper<T> m1, IMapper<TOther> m2) =>
        ComparisonResult.Failure(Format(m1) + " cannot be compared to " + Format(m2));

    private static ComparisonResult DoesNotEqual<T, TOther>(IMapper<T> m1, IMapper<TOther> m2) =>
        ComparisonResult.Failure(Format(m1) + " does not equal " + Format(m2));

    /// <summary>
    /// The failure of a <c>&lt;&gt;</c>, which the reference words differently from a failed <c>=</c>.
    /// </summary>
    /// <remarks>
    /// "should not equal", not "does not equal": Java's <c>notEqualSame</c>/<c>notEqualDifferent</c>
    /// build their own message rather than negating the equality one. Reads oddly for the
    /// {@code Any} arm — "should not equal" for operands that all matched — but the wording is part of
    /// the validation record the parity sweep compares.
    /// </remarks>
    private static ComparisonResult ShouldNotEqual<T, TOther>(IMapper<T> m1, IMapper<TOther> m2) =>
        ComparisonResult.Failure(Format(m1) + " should not equal " + Format(m2));

    /// <summary>Mirrors ErrorHelper.formatEqualsComparisonResultError.</summary>
    private static string Format<T>(IMapper<T> o) =>
        o.ResultCount > 0
            ? RuneExpr.Describe(o.Paths) + " " + RuneExpr.FormatMultiError(o)
            : RuneExpr.Describe(o.ErrorPaths);
}
