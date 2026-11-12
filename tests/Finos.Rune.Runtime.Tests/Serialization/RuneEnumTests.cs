using Finos.Rune.Runtime.Serialization;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Serialization;

/// <summary>
/// Display-name conversion, which backs <c>to-enum</c> and <c>to-string</c> over an enum.
/// </summary>
public class RuneEnumTests
{
    private enum Colour
    {
        Red,
        DeepBlue,
    }

    private static readonly IReadOnlyDictionary<Colour, string> WireNames = new Dictionary<Colour, string>
    {
        [Colour.Red] = "RED",
        [Colour.DeepBlue] = "DEEP_BLUE",
    };

    [Fact]
    public void DisplayName_ForAMappedValue_ConvertsInBothDirections()
    {
        Assert.Equal("DEEP_BLUE", RuneEnum.ToDisplayName(WireNames, Colour.DeepBlue));
        Assert.Equal(Colour.DeepBlue, RuneEnum.FromDisplayName(WireNames, "DEEP_BLUE"));
    }

    [Fact]
    public void FromDisplayName_ForANameOutsideTheMap_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RuneEnum.FromDisplayName(WireNames, "MAUVE"));
    }

    [Fact]
    public void ToDisplayName_ForAValueOutsideTheMap_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RuneEnum.ToDisplayName(WireNames, (Colour)99));
    }

    [Fact]
    public void FromDisplayName_AcrossRepeatedCalls_ResolvesEachNameFromTheCachedReverseMap()
    {
        Assert.Equal(Colour.Red, RuneEnum.FromDisplayName(WireNames, "RED"));
        Assert.Equal(Colour.DeepBlue, RuneEnum.FromDisplayName(WireNames, "DEEP_BLUE"));
        Assert.Equal(Colour.Red, RuneEnum.FromDisplayName(WireNames, "RED"));
    }
}
