using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Mapper;

/// <summary>
/// The parameterless <c>Min</c>/<c>Max</c>, which compare the items themselves.
/// </summary>
public class MapperCExtremeTests
{
    [Fact]
    public void MinAndMax_WithNoKeySelector_CompareTheItemsThemselves()
    {
        MapperC<int?> mapper = MapperC<int?>.Of([3, 1, 2]);

        Assert.Equal(1, mapper.Min().Get());
        Assert.Equal(3, mapper.Max().Get());
    }

    [Fact]
    public void MinAndMax_ForAnEmptyList_AreAbsent()
    {
        MapperC<int?> mapper = MapperC<int?>.OfNull();

        Assert.Null(mapper.Min().Get());
        Assert.Null(mapper.Max().Get());
    }

    [Fact]
    public void MinAndMax_WhenAnItemIsAbsent_DropItRatherThanOrderingItLast()
    {
        MapperC<string?> mapper = MapperC<string?>.Of(["b", null, "a"]);

        Assert.Equal("a", mapper.Min().Get());
        Assert.Equal("b", mapper.Max().Get());
    }

    [Fact]
    public void MinAndMax_WhenTwoItemsTie_ReturnTheFirstOfThem()
    {
        MapperC<string?> mapper = MapperC<string?>.Of(["a", "a", "b"]);

        Assert.Equal("a", mapper.Min().Get());
        Assert.Equal("b", mapper.Max().Get());
    }
}
