using Finos.Rune.Runtime.Hashing;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Hashing;

/// <summary>
/// <see cref="HashAccumulator"/> — the fold and, especially, the hex rendering.
/// </summary>
public class HashAccumulatorTests
{
    [Fact]
    public void Rendering_BeforeAnythingIsAccumulated_IsZero() =>
        Assert.Equal("0", new HashAccumulator().ToString());

    [Fact]
    public void Accumulate_ForAValuelessObject_ShiftsTheResultWithoutAddingATerm()
    {
        Assert.Equal(0, new HashAccumulator().Accumulate().Result);

        Assert.Equal(7, new HashAccumulator().Accumulate(7).Result);
        Assert.Equal(7 * 31, new HashAccumulator().Accumulate(7).Accumulate().Result);
    }

    [Fact]
    public void Fold_WhenTheValueOrderIsReversed_ChangesTheResult()
    {
        int forwards = new HashAccumulator().Accumulate(1).Accumulate(2).Result;
        int backwards = new HashAccumulator().Accumulate(2).Accumulate(1).Result;

        Assert.Equal((1 * 31) + 2, forwards);
        Assert.NotEqual(forwards, backwards);
    }

    [Fact]
    public void Fold_WhenTheIntOverflowsRepeatedly_WrapsInsteadOfThrowing()
    {
        var accumulator = new HashAccumulator();
        for (int i = 0; i < 50; i++)
        {
            accumulator.Accumulate(int.MaxValue);
        }

        Assert.NotEqual(0, accumulator.Result);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(97, "61")]
    [InlineData(0x14C27C88, "14c27c88")]
    [InlineData(0x00B08711, "b08711")]
    [InlineData(-1, "ffffffff")]
    public void Rendering_AcrossUnpaddedAndNegativeResults_MatchesJavaIntegerToHexString(int result, string expected)
    {
        var accumulator = new HashAccumulator();
        accumulator.Accumulate(result);

        Assert.Equal(expected, accumulator.ToString());
    }
}
