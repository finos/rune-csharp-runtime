using System.Numerics;
using Finos.Rune.Runtime.Mapper;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// Arithmetic over mappers. Mirrors com.rosetta.model.lib.expression.MapperMaths.
/// </summary>
/// <remarks>
/// <para>Both operands must hold exactly one value; otherwise the result is absent and the
/// operator never runs. So <c>a + b</c> with either side empty is empty, not an error.</para>
/// <para>Dispatch is on the runtime types of the two values, and the fall-through cases throw with
/// message text that is reproduced verbatim.</para>
/// <para><c>TResult</c> is the caller's <em>nullable</em> result type, so callers
/// write <c>Multiply&lt;decimal?, …&gt;</c> and get a <c>MapperS&lt;decimal?&gt;</c>. Declaring
/// the return as <c>MapperS&lt;TResult?&gt;</c> and taking the non-nullable type would read better and
/// be wrong: <c>T?</c> on an unconstrained parameter is an annotation, not <c>Nullable&lt;T&gt;</c>, so
/// substituting a struct erases it and the method would return <c>MapperS&lt;decimal&gt;</c> — a
/// different closed type from the one the generator declares, and one that breaks inference wherever
/// a call is one of two branches needing a common type. A <c>where TResult : struct</c> constraint
/// would fix the numbers and reject <c>Add</c> on strings.</para>
/// </remarks>
public static class MapperMaths
{
    /// <summary>Sum, or string concatenation. Absent when either operand is not a single value.</summary>
    public static MapperS<TResult> Add<TResult, TLeft, TRight>(IMapper<TLeft> left, IMapper<TRight> right) =>
        Apply<TResult, TLeft, TRight>(left, right, OperatorPlus);

    /// <summary>Difference. Absent when either operand is not a single value.</summary>
    public static MapperS<TResult> Subtract<TResult, TLeft, TRight>(IMapper<TLeft> left, IMapper<TRight> right) =>
        Apply<TResult, TLeft, TRight>(left, right, OperatorMinus);

    /// <summary>Product. Absent when either operand is not a single value.</summary>
    public static MapperS<TResult> Multiply<TResult, TLeft, TRight>(IMapper<TLeft> left, IMapper<TRight> right) =>
        Apply<TResult, TLeft, TRight>(left, right, OperatorMultiply);

    /// <summary>
    /// Quotient in <see cref="decimal"/> precision. There is no integer fast path, so
    /// <c>int / int</c> yields a decimal number, not an int.
    /// </summary>
    public static MapperS<TResult> Divide<TResult, TLeft, TRight>(IMapper<TLeft> left, IMapper<TRight> right) =>
        Apply<TResult, TLeft, TRight>(left, right, OperatorDivide);

    private static MapperS<TResult> Apply<TResult, TLeft, TRight>(
        IMapper<TLeft> left, IMapper<TRight> right, Func<object?, object?, object> op)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.ResultCount != 1 || right.ResultCount != 1)
        {
            return MapperS<TResult>.OfNull();
        }
        return MapperS<TResult>.Of((TResult)op(left.Get(), right.Get()));
    }

    private static object OperatorPlus(object? leftOrNull, object? rightOrNull)
    {
        (object a, object b) = RequireBoth(leftOrNull, rightOrNull);
        if (a is int leftInt && b is int rightInt)
        {
            return leftInt + rightInt;
        }
        if (a is long leftLong && b is long rightLong)
        {
            return leftLong + rightLong;
        }
        if (a is BigInteger leftBig && b is BigInteger rightBig)
        {
            return leftBig + rightBig;
        }
        if (TryNumbers(a, b, out decimal x, out decimal y))
        {
            return x + y;
        }
        if (a is string leftText && b is string rightText)
        {
            return leftText + rightText;
        }
        throw new InvalidOperationException(
            $"Cant add two random ({JavaTypeNames.Of(a)}, {JavaTypeNames.Of(b)}) together");
    }

    private static object OperatorMinus(object? leftOrNull, object? rightOrNull)
    {
        (object a, object b) = RequireBoth(leftOrNull, rightOrNull);
        if (a is int leftInt && b is int rightInt)
        {
            return leftInt - rightInt;
        }
        if (a is long leftLong && b is long rightLong)
        {
            return leftLong - rightLong;
        }
        if (a is BigInteger leftBig && b is BigInteger rightBig)
        {
            return leftBig - rightBig;
        }
        if (TryNumbers(a, b, out decimal x, out decimal y))
        {
            return x - y;
        }
        if (a is string && b is string)
        {
            throw new InvalidOperationException("Cant subtract two strings together");
        }
        throw new InvalidOperationException(
            $"Cant subtract two random ({JavaTypeNames.Of(a)}, {JavaTypeNames.Of(b)}) together");
    }

    private static object OperatorMultiply(object? leftOrNull, object? rightOrNull)
    {
        (object a, object b) = RequireBoth(leftOrNull, rightOrNull);
        if (a is int leftInt && b is int rightInt)
        {
            return leftInt * rightInt;
        }
        if (a is long leftLong && b is long rightLong)
        {
            return leftLong * rightLong;
        }
        if (a is BigInteger leftBig && b is BigInteger rightBig)
        {
            return leftBig * rightBig;
        }
        if (TryNumbers(a, b, out decimal x, out decimal y))
        {
            return x * y;
        }
        if (a is string && b is string)
        {
            throw new InvalidOperationException("Cant multiply two strings together");
        }
        throw new InvalidOperationException(
            $"Cant multiply two random ({JavaTypeNames.Of(a)}, {JavaTypeNames.Of(b)}) together");
    }

    private static object OperatorDivide(object? leftOrNull, object? rightOrNull)
    {
        (object a, object b) = RequireBoth(leftOrNull, rightOrNull);
        // No integer branch: like Java, int / int yields a decimal.
        if (TryNumbers(a, b, out decimal x, out decimal y))
        {
            return x / y;
        }
        if (a is string && b is string)
        {
            throw new InvalidOperationException("Cant divide two strings");
        }
        throw new InvalidOperationException(
            $"Cant divide two random ({JavaTypeNames.Of(a)}, {JavaTypeNames.Of(b)})");
    }

    /// <summary>
    /// Java's date arms are deliberately NOT reproduced.
    /// </summary>
    /// <remarks>
    /// <c>MapperMaths</c> tests for <c>java.time.LocalDate</c>, but a Rune <c>date</c> is
    /// <c>records.Date</c>, so those arms are unreachable and <c>date + time</c> actually falls
    /// through to "Cant add two random (DateImpl, LocalTime) together". Special-casing
    /// <see cref="DateOnly"/> here would make C# succeed where Java throws. CDM routes date
    /// arithmetic through the <c>[codeImplementation]</c> DateDifference function instead.
    /// </remarks>
    private static (object Left, object Right) RequireBoth(object? a, object? b)
    {
        if (a == null || b == null)
        {
            throw new ArgumentException(
                $"Unhandled parameter types: [{RuneComparer.Describe(a)}, {RuneComparer.Describe(b)}]");
        }
        return (a, b);
    }

    private static bool TryNumbers(object a, object b, out decimal x, out decimal y)
    {
        if (RuneComparer.TryAsNumber(a, out x) && RuneComparer.TryAsNumber(b, out y))
        {
            return true;
        }
        y = 0m;
        return false;
    }
}
