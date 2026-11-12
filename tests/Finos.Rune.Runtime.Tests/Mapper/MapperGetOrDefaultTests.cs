using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Mapper;

/// <summary>
/// <c>GetOrDefault</c> and <c>ComparisonResult.AsMapper</c> — the two shapes the coercion service
/// needs to move between a mapper and a bare value.
/// </summary>
public class MapperGetOrDefaultTests
{
    [Fact]
    public void GetOrDefault_OnAPresentSingleMapper_ReturnsTheValue()
    {
        Assert.Equal(7, MapperS<int?>.Of(7).GetOrDefault(-1));
        Assert.Equal("a", MapperS<string?>.Of("a").GetOrDefault("fallback"));
    }

    [Fact]
    public void GetOrDefault_OnAnAbsentOrNullHoldingSingleMapper_ReturnsTheFallback()
    {
        Assert.Equal(-1, MapperS<int?>.OfNull().GetOrDefault(-1));
        Assert.Equal("fallback", MapperS<string?>.OfNull().GetOrDefault("fallback"));
        Assert.Equal(-1, MapperS<int?>.Of(null).GetOrDefault(-1));
    }

    [Fact]
    public void GetOrDefault_OnAListMapper_ReturnsTheFallbackUnlessExactlyOneItem()
    {
        Assert.Equal(7, MapperC<int?>.Of([7]).GetOrDefault(-1));
        Assert.Equal(-1, MapperC<int?>.OfNull().GetOrDefault(-1));
        Assert.Equal(-1, MapperC<int?>.Of([7, 8]).GetOrDefault(-1));
    }

    [Fact]
    public void AsMapper_ForSuccessAndFailure_CarriesTheBooleanThrough()
    {
        Assert.True(ComparisonResult.Success().AsMapper().Get());
        Assert.False(ComparisonResult.Failure("nope").AsMapper().Get());
    }

    [Fact]
    public void AsMapper_ForAnEmptyComparisonResult_YieldsAnAbsentMapperWithAnErrorPath()
    {
        MapperS<bool?> mapper = ComparisonResult.OfEmpty().AsMapper();

        Assert.Null(mapper.Get());
        Assert.Equal(0, mapper.ResultCount);
        Assert.Single(mapper.ErrorPaths);
    }
}
