using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// How a value reads inside a validation failure message.
/// </summary>
/// <remarks>
/// Message text is part of the validation record the Java parity sweep compares, so this is a
/// contract and not cosmetics. Two of these cases were found by that sweep, once every path, name and
/// verdict matched and only the wording did not: a date rendered as <c>12/14/1994</c> against Java's
/// <c>1994-12-14</c>, and an enum whose Rune name is not a valid C# identifier rendered as its escaped
/// member name.
/// </remarks>
public class RuneDescribeTests
{
    [Fact]
    public void Date_WhereInvariantCultureWouldGiveMonthFirst_RendersAsIso()
    {
        Assert.Equal("1994-12-14", RuneComparer.Describe(new DateOnly(1994, 12, 14)));
    }

    [Fact]
    public void Time_AcrossZeroAndNonZeroSeconds_OmitsSecondsOnlyWhenZero()
    {
        Assert.Equal("10:30", RuneComparer.Describe(new TimeOnly(10, 30)));
        Assert.Equal("10:30:05", RuneComparer.Describe(new TimeOnly(10, 30, 5)));
    }

    [Fact]
    public void DateTime_ForAZonelessValue_RendersAsIsoWithoutAnOffset()
    {
        Assert.Equal("1994-12-14T10:30:05", RuneComparer.Describe(new DateTime(1994, 12, 14, 10, 30, 5)));
    }

    [Fact]
    public void ZonedDateTime_WithANonZeroOffset_RendersTheOffsetAfterTheIsoTime()
    {
        Assert.Equal(
            "1994-12-14T10:30:05+02:00",
            RuneComparer.Describe(new DateTimeOffset(1994, 12, 14, 10, 30, 5, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void Enum_WhenTheRuneNameIsNotAValidIdentifier_RendersTheDisplayName()
    {
        Assert.Equal("30E/360", RuneComparer.Describe(TestEnum._30E_360));
    }

    [Fact]
    public void Enum_WhenTheMemberAndDisplayNamesAgree_RendersThatSharedName()
    {
        Assert.Equal("FirstValue", RuneComparer.Describe(TestEnum.FirstValue));
    }

    [Fact]
    public void Enum_WithNoGeneratedConverter_FallsBackToTheMemberName()
    {
        Assert.Equal(nameof(CardinalityOperator.All), RuneComparer.Describe(CardinalityOperator.All));
    }

    [Fact]
    public void NonTemporalValues_ForNullsBoolsListsAndDecimals_RenderUnchanged()
    {
        Assert.Equal("null", RuneComparer.Describe(null));
        Assert.Equal("true", RuneComparer.Describe(true));
        Assert.Equal("[1, 2]", RuneComparer.Describe(new[] { 1, 2 }));
        Assert.Equal("1.5", RuneComparer.Describe(1.5m));
    }
}
