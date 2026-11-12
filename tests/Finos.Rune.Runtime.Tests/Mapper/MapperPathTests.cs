using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Mapper;

/// <summary>
/// FullPath is interpolated verbatim into data-rule failure messages, so these are parity tests.
/// The expected strings are the shape seen in the Java oracle's output, e.g.
/// "[PriceSchedule-&gt;getValue] does not exist".
/// </summary>
public class MapperPathTests
{
    [Fact]
    public void FullPath_ForARootAndAGetter_JoinsThemWithAnArrow()
    {
        MapperPath path = MapperPath.Builder()
            .AddRoot(typeof(PriceSchedule)).ToBuilder()
            .AddFunctionName("getValue");

        Assert.Equal("PriceSchedule->getValue", path.FullPath);
        Assert.Equal(path.FullPath, path.ToString());
    }

    [Fact]
    public void FullPath_ForAListElementFollowedByAGetter_CarriesTheIndexInBrackets()
    {
        MapperPath path = MapperPath.Builder()
            .AddRoot(typeof(PriceSchedule)).ToBuilder()
            .AddListFunctionName("getDatedValue", 1).ToBuilder()
            .AddFunctionName("getDate");

        Assert.Equal("PriceSchedule->getDatedValue[1]->getDate", path.FullPath);
    }

    [Fact]
    public void Names_ForAGetterElement_StripTheGetPrefixThatGettersKeep()
    {
        MapperPath path = MapperPath.Builder()
            .AddRoot(typeof(PriceSchedule)).ToBuilder()
            .AddFunctionName("getUnitOfAmount");

        Assert.Equal(new[] { "PriceSchedule", "unitOfAmount" }, path.Names);
        Assert.Equal(new[] { "PriceSchedule", "getUnitOfAmount" }, path.Getters);
        Assert.Equal("unitOfAmount", path.LastName);
    }

    [Theory]
    [InlineData("getValue", "value")]
    [InlineData("getURL", "uRL")]
    [InlineData("_getClass", "_getClass")]
    [InlineData("value", "value")]
    [InlineData("get", "get")]
    public void LastName_AcrossGetterSpellings_LowersOnlyTheLeadingCharacter(string getter, string expected)
    {
        MapperPath path = MapperPath.Builder().AddFunctionName(getter);

        Assert.Equal(expected, path.LastName);
    }

    [Fact]
    public void AddNull_OnAPathWithARoot_AppendsTheNullMarker()
    {
        MapperPath path = MapperPath.Builder().AddRoot(typeof(PriceSchedule)).ToBuilder().AddNull();

        Assert.Equal("PriceSchedule->Null", path.FullPath);
    }

    [Fact]
    public void AddRoot_ForAGenericType_UsesTheSimpleNameWithoutArity()
    {
        Assert.Equal("List", MapperPath.Builder().AddRoot(typeof(List<string>)).FullPath);
    }

    [Fact]
    public void Equality_ForPathsWithTheSameElements_AgreesWithHashCodeAndCompareTo()
    {
        MapperPath first = MapperPath.Builder().AddRoot(typeof(PriceSchedule)).ToBuilder().AddFunctionName("getValue");
        MapperPath second = MapperPath.Builder().AddRoot(typeof(PriceSchedule)).ToBuilder().AddFunctionName("getValue");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(0, first.CompareTo(second));
    }

    [Fact]
    public void IndexedElement_ComparedWithTheUnindexedSameName_IsUnequalAndSortsFirst()
    {
        MapperPath indexed = MapperPath.Builder().AddListFunctionName("getFoo", 0);
        MapperPath plain = MapperPath.Builder().AddFunctionName("getFoo");

        Assert.NotEqual(indexed, plain);
        Assert.True(indexed.CompareTo(plain) < 0);
    }

    [Fact]
    public void CompareTo_AcrossDifferingNamesIndicesAndLengths_OrdersInThatPrecedence()
    {
        MapperPath a = MapperPath.Builder().AddFunctionName("getA");
        MapperPath b = MapperPath.Builder().AddFunctionName("getB");
        MapperPath aThenB = MapperPath.Builder().AddFunctionName("getA").ToBuilder().AddFunctionName("getB");
        MapperPath first = MapperPath.Builder().AddListFunctionName("getA", 0);
        MapperPath second = MapperPath.Builder().AddListFunctionName("getA", 1);

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(a.CompareTo(aThenB) < 0);
        Assert.True(first.CompareTo(second) < 0);
    }

    [Fact]
    public void ToBuilder_WhenUsedForTwoBranches_LeavesTheSourcePathUnchanged()
    {
        MapperPath root = MapperPath.Builder().AddRoot(typeof(PriceSchedule));

        MapperPath branchOne = root.ToBuilder().AddFunctionName("getValue");
        MapperPath branchTwo = root.ToBuilder().AddFunctionName("getDatedValue");

        Assert.Equal("PriceSchedule", root.FullPath);
        Assert.Equal("PriceSchedule->getValue", branchOne.FullPath);
        Assert.Equal("PriceSchedule->getDatedValue", branchTwo.FullPath);
    }

    private sealed class PriceSchedule
    {
    }
}
