using System.Numerics;
using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// Exact integral narrowing from the canonical decimal representation.
/// </summary>
/// <remarks>
/// "Exact" is the whole point: <c>2.00</c> is the integer two and converts, <c>2.01</c> is not and
/// throws. C#'s own <c>(int)someDecimal</c> truncates silently, which would turn a modelling error
/// into a wrong number instead of a loud one.
/// </remarks>
public class RuneDecimalNarrowingTests
{
    private static decimal N(string text) =>
        decimal.Parse(text, System.Globalization.NumberStyles.Float, Culture.Invariant);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("-7", -7)]
    [InlineData("2.00", 2)]
    [InlineData("-2.000", -2)]
    [InlineData("1E+3", 1000)]
    [InlineData("2147483647", 2147483647)]
    [InlineData("-2147483648", -2147483648)]
    public void ToInt32Exact_ForIntegralValuesWithinTheIntRange_ConvertsExactly(string text, int expected)
    {
        decimal value = N(text);

        Assert.Equal(expected, RuneDecimal.ToInt32Exact(value));
        Assert.True(RuneDecimal.TryToInt32(value, out int converted));
        Assert.Equal(expected, converted);
    }

    [Theory]
    [InlineData("2.01")]
    [InlineData("0.1")]
    [InlineData("-0.0001")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("79228162514264337593543950335")]
    public void ToInt32Exact_ForFractionsAndValuesOutsideTheIntRange_ThrowsOverflowAndTryToInt32Fails(string text)
    {
        decimal value = N(text);

        Assert.Throws<OverflowException>(() => RuneDecimal.ToInt32Exact(value));
        Assert.False(RuneDecimal.TryToInt32(value, out int converted));
        Assert.Equal(0, converted);
    }

    [Theory]
    [InlineData("9223372036854775807", 9223372036854775807L)]
    [InlineData("-9223372036854775808", -9223372036854775808L)]
    [InlineData("4300000000.0", 4300000000L)]
    public void ToInt64Exact_AtTheLongBoundsAndAboveTheIntRange_ConvertsExactly(string text, long expected)
    {
        decimal value = N(text);

        Assert.Equal(expected, RuneDecimal.ToInt64Exact(value));
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    [InlineData("1.5")]
    public void ToInt64Exact_ForFractionsAndValuesOutsideTheLongRange_ThrowsOverflowAndTryToInt64Fails(string text)
    {
        decimal value = N(text);

        Assert.Throws<OverflowException>(() => RuneDecimal.ToInt64Exact(value));
        Assert.False(RuneDecimal.TryToInt64(value, out _));
    }

    [Fact]
    public void ToBigIntegerExact_ForDecimalMaxValue_ConvertsWithNoLoss()
    {
        decimal value = decimal.MaxValue;

        Assert.Equal(
            BigInteger.Parse("79228162514264337593543950335"),
            RuneDecimal.ToBigIntegerExact(value));
    }

    [Fact]
    public void ToBigIntegerExact_ForAValueWithAFractionalPart_ThrowsAndTryToBigIntegerYieldsZero()
    {
        decimal value = decimal.Parse("1.0000000000000000000000000001", Culture.Invariant);

        Assert.Throws<OverflowException>(() => RuneDecimal.ToBigIntegerExact(value));
        Assert.False(RuneDecimal.TryToBigInteger(value, out BigInteger converted));
        Assert.Equal(BigInteger.Zero, converted);
    }

    [Fact]
    public void Narrowing_ForAnIntegralValueWithStoredScale_RemainsExact()
    {
        decimal value = 1200.00m;

        Assert.Equal(1200, RuneDecimal.ToInt32Exact(value));
        Assert.Equal(new BigInteger(1200), RuneDecimal.ToBigIntegerExact(value));
    }
}
