using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// Decimal scale and significant-figure operations used by CDM's rounding functions.
/// </summary>
/// <remarks>
/// Pinned against <c>BigDecimal.setScale</c> and <c>BigDecimal.round(MathContext)</c>. The mode names are
/// defined relative to zero, not toward an infinity, which is where .NET's <c>MidpointRounding</c> would
/// have given different answers for negative values — hence a negative case for every mode.
/// </remarks>
public class RuneDecimalRoundingTests
{
    private static decimal N(string text) => decimal.Parse(text, Culture.Invariant);

    private static string Text(decimal value) => value.ToString(Culture.Invariant);

    [Theory]
    [InlineData("2.341", 2, "2.35")]
    [InlineData("-2.341", 2, "-2.35")]
    [InlineData("2.300", 2, "2.30")]
    public void SetScale_WithRoundingUp_MovesBothSignsAwayFromZero(string value, int scale, string expected)
    {
        Assert.Equal(expected, Text(RuneDecimal.SetScale(N(value), scale, RuneRounding.Up)));
    }

    [Theory]
    [InlineData("2.349", 2, "2.34")]
    [InlineData("-2.349", 2, "-2.34")]
    public void SetScale_WithRoundingDown_MovesBothSignsTowardZero(string value, int scale, string expected)
    {
        Assert.Equal(expected, Text(RuneDecimal.SetScale(N(value), scale, RuneRounding.Down)));
    }

    [Theory]
    [InlineData("2.345", 2, "2.35")]
    [InlineData("-2.345", 2, "-2.35")]
    [InlineData("2.344", 2, "2.34")]
    public void SetScale_WithRoundingHalfUp_BreaksTiesAwayFromZeroForBothSigns(string value, int scale, string expected)
    {
        Assert.Equal(expected, Text(RuneDecimal.SetScale(N(value), scale, RuneRounding.HalfUp)));
    }

    [Fact]
    public void SetScale_ToMorePlacesThanTheValueHas_PadsExactlyAndKeepsTheNewScale()
    {
        decimal widened = RuneDecimal.SetScale(N("1.5"), 4, RuneRounding.Down);

        Assert.Equal(4, RuneDecimal.GetScale(widened));
        Assert.Equal("1.5000", Text(widened));
    }

    [Fact]
    public void SetScale_ToZeroPlaces_YieldsAnIntegerUnderEachMode()
    {
        Assert.Equal(N("3"), RuneDecimal.SetScale(N("2.5"), 0, RuneRounding.HalfUp));
        Assert.Equal(N("2"), RuneDecimal.SetScale(N("2.5"), 0, RuneRounding.Down));
        Assert.Equal(N("3"), RuneDecimal.SetScale(N("2.1"), 0, RuneRounding.Up));
    }

    [Fact]
    public void Round_ForAPrecisionOfThree_KeepsSignificantDigitsNotDecimalPlaces()
    {
        Assert.Equal(N("123"), RuneDecimal.Round(N("123.456"), 3, RuneRounding.HalfUp));
        Assert.Equal(N("0.00123"), RuneDecimal.Round(N("0.001234"), 3, RuneRounding.Down));
        Assert.Equal(N("12000"), RuneDecimal.Round(N("12345"), 2, RuneRounding.HalfUp));
    }

    [Fact]
    public void Round_ToMorePrecisionThanTheValueHas_LeavesValueAndScaleUnchanged()
    {
        decimal value = N("1.5");

        decimal rounded = RuneDecimal.Round(value, 9, RuneRounding.Up);

        Assert.Equal(value, rounded);
        Assert.Equal(RuneDecimal.GetScale(value), RuneDecimal.GetScale(rounded));
    }

    [Fact]
    public void RoundAndSetScale_ForZero_BothReturnZero()
    {
        Assert.Equal(0m, RuneDecimal.Round(0m, 3, RuneRounding.Up));
        Assert.Equal("0.00", Text(RuneDecimal.SetScale(0m, 2, RuneRounding.Up)));
    }

    [Fact]
    public void Round_WithANonPositiveSignificantFigureCount_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuneDecimal.Round(N("1.5"), 0, RuneRounding.Up));
    }

    [Fact]
    public void DivideToScale_ForOneOverThreeAtTenPlaces_KeepsDecimalPlacesNotSignificantDigits()
    {
        decimal third = RuneDecimal.DivideToScale(N("1"), N("3"), 10, RuneRounding.HalfEven);

        Assert.Equal(10, RuneDecimal.GetScale(third));
        Assert.Equal("0.3333333333", Text(third));
    }

    [Fact]
    public void DivideToScale_WithHalfEvenOnATie_RoundsToTheEvenNeighbour()
    {
        Assert.Equal(N("0.2"), RuneDecimal.DivideToScale(N("0.25"), N("1"), 1, RuneRounding.HalfEven));
        Assert.Equal(N("0.4"), RuneDecimal.DivideToScale(N("0.35"), N("1"), 1, RuneRounding.HalfEven));
        Assert.Equal(N("0.3"), RuneDecimal.SetScale(N("0.25"), 1, RuneRounding.HalfUp));
    }

    [Fact]
    public void DivideToScale_WhenEitherOrBothOperandsAreNegative_CarriesTheSignThrough()
    {
        Assert.Equal("-2.00", Text(RuneDecimal.DivideToScale(N("-4"), N("2"), 2, RuneRounding.HalfEven)));
        Assert.Equal("-2.00", Text(RuneDecimal.DivideToScale(N("4"), N("-2"), 2, RuneRounding.HalfEven)));
        Assert.Equal("2.00", Text(RuneDecimal.DivideToScale(N("-4"), N("-2"), 2, RuneRounding.HalfEven)));
    }

    [Fact]
    public void DivideToScale_ByZero_ThrowsDivideByZeroException()
    {
        Assert.Throws<DivideByZeroException>(
            () => RuneDecimal.DivideToScale(N("1"), N("0"), 2, RuneRounding.HalfEven));
    }

    [Fact]
    public void MaxAndMin_ForEqualMagnitudes_PreserveTheLeftOperandsScale()
    {
        Assert.Equal(2, RuneDecimal.GetScale(RuneDecimal.Max(N("2.50"), N("2.5"))));
        Assert.Equal(N("1"), RuneDecimal.Min(N("1"), N("2")));
        Assert.Equal(N("2"), RuneDecimal.Max(N("1"), N("2")));
    }
}
