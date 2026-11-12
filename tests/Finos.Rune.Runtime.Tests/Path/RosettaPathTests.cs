using Finos.Rune.Runtime.Path;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Path;

public class RosettaPathTests
{
    [Fact]
    public void BuildPath_ForChainedSubPaths_RendersDottedSegmentsWithIndices()
    {
        RosettaPath path = RosettaPath.ValueOf("TradeState")
            .NewSubPath("trade")
            .NewSubPath("tradeLot", 0)
            .NewSubPath("priceQuantity");

        Assert.Equal("TradeState.trade.tradeLot(0).priceQuantity", path.BuildPath());
        Assert.Equal(path.BuildPath(), path.ToString());
    }

    [Fact]
    public void BuildPath_ForAnIndexOfZero_RendersTheParenthesisedZero()
    {
        Assert.Equal("a.b(0)", RosettaPath.ValueOf("a").NewSubPath("b", 0).BuildPath());
    }

    [Theory]
    [InlineData("TradeState")]
    [InlineData("a.b.c")]
    [InlineData("a.b(3).c")]
    [InlineData("a.b[key=value].c")]
    [InlineData("a.*.c")]
    public void ValueOf_AcrossEveryRenderedElementForm_RoundTripsThroughBuildPath(string rendered)
    {
        Assert.Equal(rendered, RosettaPath.ValueOf(rendered).BuildPath());
    }

    [Fact]
    public void ValueOf_ForAMalformedElement_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RosettaPath.ValueOf("a.b c"));
        Assert.Throws<ArgumentException>(() => RosettaPath.ValueOf("a.b(x)"));
    }

    [Fact]
    public void Equality_ForAnAbsentIndexAgainstZero_HoldsDespiteDifferentRendering()
    {
        RosettaPath withoutIndex = RosettaPath.ValueOf("a").NewSubPath("b");
        RosettaPath withZero = RosettaPath.ValueOf("a").NewSubPath("b", 0);

        Assert.Equal(withoutIndex, withZero);
        Assert.Equal(withoutIndex.GetHashCode(), withZero.GetHashCode());
        Assert.Equal(0, withoutIndex.CompareTo(withZero));
        Assert.Equal("a.b", withoutIndex.BuildPath());
        Assert.Equal("a.b(0)", withZero.BuildPath());
    }

    [Fact]
    public void Equality_ForDifferingIndices_FailsAndTheLowerIndexSortsFirst()
    {
        RosettaPath first = RosettaPath.ValueOf("a").NewSubPath("b", 1);
        RosettaPath second = RosettaPath.ValueOf("a").NewSubPath("b", 2);

        Assert.NotEqual(first, second);
        Assert.True(first.CompareTo(second) < 0);
    }

    [Fact]
    public void CompareTo_ForDifferingNamesAndDepths_OrdersByNameThenDepth()
    {
        Assert.True(RosettaPath.ValueOf("a").CompareTo(RosettaPath.ValueOf("b")) < 0);
        Assert.True(RosettaPath.ValueOf("a").CompareTo(RosettaPath.ValueOf("a.b")) < 0);
        Assert.True(RosettaPath.ValueOf("a.b").CompareTo(RosettaPath.ValueOf("a")) > 0);
        Assert.Equal(0, RosettaPath.ValueOf("a.b").CompareTo(RosettaPath.ValueOf("a.b")));
    }

    [Fact]
    public void NewSubPath_OnTheNullPath_StartsAFreshRoot()
    {
        Assert.Equal("trade", RosettaPath.Null.NewSubPath("trade").BuildPath());
        Assert.Equal("trade(2)", RosettaPath.Null.NewSubPath("trade", 2).BuildPath());
    }

    [Fact]
    public void TrimFirst_ForAThreeElementPath_DropsTheRootElement()
    {
        RosettaPath? trimmed = RosettaPath.ValueOf("TradeState.trade.product").TrimFirst();

        Assert.NotNull(trimmed);
        Assert.Equal("trade.product", trimmed.BuildPath());
    }

    [Fact]
    public void AllElementPaths_ForANestedPath_ListsTheNamesRootFirst()
    {
        Assert.Equal(
            new[] { "TradeState", "trade", "product" },
            RosettaPath.ValueOf("TradeState.trade.product").AllElementPaths());
    }

    [Fact]
    public void ToIndexless_ForAPathWithSeveralIndices_StripsThemAll()
    {
        RosettaPath path = RosettaPath.ValueOf("a(1).b(2).c");

        Assert.Equal("a.b.c", path.ToIndexless().BuildPath());
    }

    [Fact]
    public void StartsWith_ForCandidatePrefixes_HoldsOnlyForActualPrefixes()
    {
        RosettaPath path = RosettaPath.ValueOf("a.b.c");

        Assert.True(path.StartsWith(RosettaPath.ValueOf("a")));
        Assert.True(path.StartsWith(RosettaPath.ValueOf("a.b")));
        Assert.True(path.StartsWith(RosettaPath.ValueOf("a.b.c")));
        Assert.False(path.StartsWith(RosettaPath.ValueOf("b")));
        Assert.False(path.StartsWith(RosettaPath.ValueOf("a.b.c.d")));
    }

    [Fact]
    public void EndsWith_ForSuffixesWildcardsAndNull_HoldsOnlyForRealSuffixes()
    {
        RosettaPath path = RosettaPath.ValueOf("a.b.c");

        Assert.True(path.EndsWith(RosettaPath.ValueOf("c")));
        Assert.True(path.EndsWith(RosettaPath.ValueOf("b.c")));
        Assert.True(path.EndsWith(RosettaPath.ValueOf("*")));
        Assert.True(path.EndsWith(RosettaPath.ValueOf("*.c")));
        Assert.True(path.EndsWith(null));
        Assert.False(path.EndsWith(RosettaPath.ValueOf("b")));
    }

    [Fact]
    public void MatchesIgnoringIndex_WhenOnlyTheIndicesDiffer_MatchesOnNamesAlone()
    {
        Assert.True(RosettaPath.ValueOf("a(1).b(2)").MatchesIgnoringIndex(RosettaPath.ValueOf("a(9).b")));
        Assert.False(RosettaPath.ValueOf("a.b").MatchesIgnoringIndex(RosettaPath.ValueOf("a.c")));
        Assert.False(RosettaPath.ValueOf("a.b").MatchesIgnoringIndex(RosettaPath.ValueOf("b")));
    }

    [Fact]
    public void ElementAttributes_WhenTheirOrderIsReversed_RenderInOrderYetCompareEqual()
    {
        RosettaPath.Element ordered = RosettaPath.Element.Create(
            "b", null, [new KeyValuePair<string, string>("x", "1"), new KeyValuePair<string, string>("y", "2")]);
        RosettaPath.Element reversed = RosettaPath.Element.Create(
            "b", null, [new KeyValuePair<string, string>("y", "2"), new KeyValuePair<string, string>("x", "1")]);

        Assert.Equal("b[x=1,y=2]", ordered.AsPathString());
        Assert.Equal("b[y=2,x=1]", reversed.AsPathString());
        Assert.Equal(ordered, reversed);
        Assert.Equal(ordered.GetHashCode(), reversed.GetHashCode());
    }

    [Fact]
    public void CreatePathFromElements_ForTheElementsOfAnExistingPath_RebuildsThatPath()
    {
        RosettaPath original = RosettaPath.ValueOf("a.b(1).c");

        RosettaPath? rebuilt = RosettaPath.CreatePathFromElements(original.AllElements());

        Assert.NotNull(rebuilt);
        Assert.Equal(original, rebuilt);
        Assert.Equal(original.BuildPath(), rebuilt.BuildPath());
    }

    [Fact]
    public void CreatePathFromElements_ForAnEmptyElementList_ReturnsNull()
    {
        Assert.Null(RosettaPath.CreatePathFromElements([]));
    }
}
