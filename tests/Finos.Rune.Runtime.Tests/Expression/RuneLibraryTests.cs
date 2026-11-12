using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// The three DSL built-in library functions the model actually calls.
/// </summary>
public class RuneLibraryTests
{
    private static decimal N(string text) => decimal.Parse(text, Culture.Invariant);

    [Fact]
    public void MinAndMax_RegardlessOfArgumentOrder_ReturnTheSmallerAndLargerValue()
    {
        Assert.Equal(N("3"), RuneLibrary.Min(N("3"), N("7")));
        Assert.Equal(N("3"), RuneLibrary.Min(N("7"), N("3")));
        Assert.Equal(N("7"), RuneLibrary.Max(N("3"), N("7")));
        Assert.Equal(N("7"), RuneLibrary.Max(N("7"), N("3")));
    }

    [Fact]
    public void MinAndMax_ForOperandsDifferingOnlyInScale_CompareByValue()
    {
        Assert.Equal(2, RuneDecimal.GetScale(RuneLibrary.Min(N("30.00"), N("30"))!.Value));
        Assert.Equal(1, RuneDecimal.GetScale(RuneLibrary.Max(N("2.5"), N("2.50"))!.Value));
    }

    [Fact]
    public void MinAndMax_WithNegativeOperands_OrderThemByValue()
    {
        Assert.Equal(N("-5"), RuneLibrary.Min(N("-5"), N("2")));
        Assert.Equal(N("-2"), RuneLibrary.Max(N("-5"), N("-2")));
    }

    [Fact]
    public void MinMaxAndIsLeapYear_WithAnAbsentArgument_AreAbsentRatherThanThrowing()
    {
        Assert.Null(RuneLibrary.Min(null, N("3")));
        Assert.Null(RuneLibrary.Min(N("3"), null));
        Assert.Null(RuneLibrary.Max(null, null));
        Assert.Null(RuneLibrary.IsLeapYear(null));
    }

    [Theory]
    [InlineData("2024", true)]
    [InlineData("2023", false)]
    [InlineData("1900", false)]
    [InlineData("2000", true)]
    public void IsLeapYear_AcrossOrdinaryAndCenturyYears_FollowsTheGregorianRule(string year, bool expected) =>
        Assert.Equal(expected, RuneLibrary.IsLeapYear(N(year)));

    [Fact]
    public void IsLeapYear_ForAFractionalYear_ReadsTheIntegralPart()
    {
        Assert.True(RuneLibrary.IsLeapYear(N("2024.9")));
        Assert.False(RuneLibrary.IsLeapYear(N("2023.9")));
    }
}
