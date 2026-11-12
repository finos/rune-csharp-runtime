using System.Numerics;
using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// The digit counts here are what Java's BigDecimal produces for the same literals. Two of them
/// are called out as worked examples in the comments of ExpressionOperatorsNullSafe.checkNumber
/// (0.0012 counts as 5 digits; 12000 counts its stripped trailing zeros again).
/// </summary>
public class RuneDecimalTests
{
    [Theory]
    [InlineData("0", 0, 0)]
    [InlineData("0.00", 0, 0)]
    [InlineData("12", 12, 0)]
    [InlineData("1.50", 15, 1)]
    [InlineData("123.45", 12345, 2)]
    [InlineData("0.0012", 12, 4)]
    [InlineData("12000", 12, -3)]
    [InlineData("-12000", -12, -3)]
    [InlineData("-0.0012", -12, 4)]
    public void StripTrailingZeros_ForZerosOnEitherSideOfThePoint_MatchesBigDecimal(string literal, long expectedUnscaled, int expectedScale)
    {
        (BigInteger unscaled, int scale) = RuneDecimal.StripTrailingZeros(decimal.Parse(literal, Culture.Invariant));

        Assert.Equal(new BigInteger(expectedUnscaled), unscaled);
        Assert.Equal(expectedScale, scale);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    [InlineData(12, 2)]
    [InlineData(-12345, 5)]
    public void Precision_ForZeroAndSignedUnscaledValues_CountsDigitsAndTreatsZeroAsOne(long unscaled, int expected)
    {
        Assert.Equal(expected, RuneDecimal.Precision(new BigInteger(unscaled)));
    }

    [Theory]
    [InlineData("123.45", 5)]
    [InlineData("1.50", 2)]
    [InlineData("0.0012", 5)]
    [InlineData("0.1", 2)]
    [InlineData("12000", 5)]
    [InlineData("-12000", 5)]
    [InlineData("0", 1)]
    [InlineData("0.00", 1)]
    [InlineData("1", 1)]
    public void CountDigits_AcrossLeadingZeroAndNegativeScaleLiterals_MatchesTheCheckNumberCalculation(string literal, int expected)
    {
        Assert.Equal(expected, RuneDecimal.CountDigits(decimal.Parse(literal, Culture.Invariant)));
    }

    [Theory]
    [InlineData("123.45", 2)]
    [InlineData("1.50", 1)]
    [InlineData("0.0012", 4)]
    [InlineData("12000", 0)]
    [InlineData("0", 0)]
    [InlineData("0.00", 0)]
    public void CountFractionalDigits_AcrossPositiveAndNegativeScales_ReturnsTheScaleFlooredAtZero(string literal, int expected)
    {
        Assert.Equal(expected, RuneDecimal.CountFractionalDigits(decimal.Parse(literal, Culture.Invariant)));
    }
}
