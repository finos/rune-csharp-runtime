using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Mapper;

/// <summary>
/// Each test pins one rule from harness/EXPRESSION-PORTING-TRAPS.md — the behaviours a plausible
/// C# translation gets wrong while still compiling and still passing a naive test.
/// </summary>
public class MapperTests
{
    [Fact]
    public void RootPath_ForScalarMapperSValues_UsesTheJavaTypeName()
    {
        Assert.Equal("Integer", MapperS<int?>.Of(5).Paths[0].FullPath);
        Assert.Equal("BigDecimal", MapperS<decimal?>.Of(1m).Paths[0].FullPath);
        Assert.Equal("String", MapperS<string?>.Of("x").Paths[0].FullPath);
        Assert.Equal("Boolean", MapperS<bool?>.Of(true).Paths[0].FullPath);
        Assert.Equal("DateImpl", MapperS<DateOnly?>.Of(new DateOnly(2024, 1, 1)).Paths[0].FullPath);
    }

    [Fact]
    public void OfNull_OnASingleVersusAListMapper_YieldsAnErrorItemOnlyForTheSingle()
    {
        MapperS<int?> single = MapperS<int?>.OfNull();
        MapperC<int?> list = MapperC<int?>.OfNull();

        Assert.Equal(["Null"], single.ErrorPaths.Select(p => p.FullPath));
        Assert.Empty(list.ErrorPaths);
        Assert.Empty(single.Paths);
        Assert.Empty(list.Paths);
        Assert.Empty(single.GetMulti());
        Assert.Empty(list.GetMulti());
    }

    [Fact]
    public void Of_WithANullValue_YieldsAnErrorItemAndNoResults()
    {
        MapperS<string?> mapper = MapperS<string?>.Of(null);

        Assert.Equal(0, mapper.ResultCount);
        Assert.Equal(["Null"], mapper.ErrorPaths.Select(p => p.FullPath));
        Assert.Equal(["Null was null"], mapper.Errors);
    }

    [Fact]
    public void Errors_ForASingleVersusAListMapper_UseTheirOwnWording()
    {
        Assert.Equal(["Null was null"], MapperS<int?>.OfNull().Errors);

        MapperC<int?> withError = MapperS<Holder?>.Of(new Holder(null))
            .MapC<int?>("getNumbers", h => h!.Numbers);
        Assert.Equal(["[Holder->getNumbers] is not set"], withError.Errors);
    }

    [Fact]
    public void Map_ChainedPastAMissingAttribute_LeavesThePathAtThatAttribute()
    {
        MapperS<string?> chained = MapperS<Holder?>.Of(new Holder(null))
            .Map("getInner", h => h!.Inner)
            .Map("getName", i => i!.Name);

        Assert.Equal(["Holder->getInner"], chained.ErrorPaths.Select(p => p.FullPath));
    }

    [Fact]
    public void MapC_ForAnEmptyChildList_YieldsOneUnindexedErrorItem()
    {
        MapperC<int?> mapped = MapperS<Holder?>.Of(new Holder(null) { Numbers = [] })
            .MapC<int?>("getNumbers", h => h!.Numbers);

        Assert.Equal(["Holder->getNumbers"], mapped.ErrorPaths.Select(p => p.FullPath));
        Assert.Equal(0, mapped.ResultCount);
    }

    [Fact]
    public void MapC_ForAPopulatedChildList_IndexesEveryElementInItsPath()
    {
        MapperC<int?> mapped = MapperS<Holder?>.Of(new Holder(null) { Numbers = [1, 2] })
            .MapC<int?>("getNumbers", h => h!.Numbers);

        Assert.Equal(
            ["Holder->getNumbers[0]", "Holder->getNumbers[1]"],
            mapped.Paths.Select(p => p.FullPath));
    }

    [Fact]
    public void ErrorItems_WhenNavigatingThenFiltering_SurviveTheNavigationButNotTheFilter()
    {
        MapperC<int?> withError = MapperC<Holder?>
            .Of([new Holder(null) { Numbers = [1] }, new Holder(null) { Numbers = [] }])
            .MapC<int?>("getNumbers", h => h!.Numbers);

        Assert.Single(withError.ErrorPaths);
        Assert.Empty(withError.FilterItemNullSafe(_ => true).ErrorPaths);
    }

    [Fact]
    public void Get_OnAListMapperWithoutExactlyOneResult_IsAbsent()
    {
        Assert.Null(MapperC<string?>.Of([]).Get());
        Assert.Equal("only", MapperC<string?>.Of(["only"]).Get());
        Assert.Null(MapperC<string?>.Of(["a", "b", "c"]).Get());
        Assert.Equal(3, MapperC<string?>.Of(["a", "b", "c"]).ResultCount);
    }

    [Fact]
    public void FirstLastAndGetItem_ForAnEmptyListOrAnOutOfRangeIndex_AreAbsentRatherThanThrowing()
    {
        MapperC<string?> empty = MapperC<string?>.Of([]);
        MapperC<string?> three = MapperC<string?>.Of(["a", "b", "c"]);

        Assert.Null(empty.First().Get());
        Assert.Null(empty.Last().Get());
        Assert.Equal("a", three.First().Get());
        Assert.Equal("c", three.Last().Get());
        Assert.Equal("b", three.GetItem(MapperS<int?>.Of(1)).Get());
        Assert.Null(three.GetItem(MapperS<int?>.Of(9)).Get());
        Assert.Null(three.GetItem(MapperS<int?>.OfNull()).Get());
    }

    [Fact]
    public void RemoveItem_ForAnInRangeAndAnOutOfRangeIndex_DropsOnlyTheIndexedElement()
    {
        MapperC<string?> three = MapperC<string?>.Of(["a", "b", "c"]);

        Assert.Equal(["a", "c"], three.RemoveItem(MapperS<int?>.Of(1)).GetMulti());
        Assert.Equal(["a", "b", "c"], three.RemoveItem(MapperS<int?>.Of(9)).GetMulti());
    }

    [Fact]
    public void Sort_ForMixedCaseStrings_OrdersOrdinallyNotByCulture()
    {
        Assert.Equal(["B", "a", "b"], MapperC<string?>.Of(["b", "B", "a"]).Sort().GetMulti());
    }

    [Fact]
    public void Sort_WithAKeySelectorOverAbsentKeys_OrdersThemLastAndStably()
    {
        MapperC<Holder?> sorted = MapperC<Holder?>
            .Of([new Holder("b"), new Holder(null), new Holder("a"), new Holder(null)])
            .Sort(m => m.Map("getName", h => h!.Name));

        Assert.Equal(["a", "b", null, null], sorted.GetMulti().Select(h => h!.Name));
    }

    [Fact]
    public void MinAndMax_WithAKeySelectorOverAbsentKeys_DropThemAndKeepTheFirstWinner()
    {
        MapperC<Holder?> holders = MapperC<Holder?>
            .Of([new Holder(null), new Holder("b"), new Holder("a"), new Holder("a")]);

        Assert.Equal("a", holders.Min(m => m.Map("getName", h => h!.Name)).Get()!.Name);
        Assert.Equal("b", holders.Max(m => m.Map("getName", h => h!.Name)).Get()!.Name);
        Assert.Null(MapperC<Holder?>.Of([]).Min(m => m.Map("getName", h => h!.Name)).Get());
    }

    [Fact]
    public void Provenance_AcrossMapItemAndFilterItem_IsDiscardedByTheFirstAndKeptByTheSecond()
    {
        // Half the list operators rebuild from raw values, as in Java; do not "improve" them.
        MapperC<int?> source = MapperS<Holder?>.Of(new Holder(null) { Numbers = [1, 2] })
            .MapC<int?>("getNumbers", h => h!.Numbers);

        Assert.Equal(["Holder->getNumbers[0]", "Holder->getNumbers[1]"], source.Paths.Select(p => p.FullPath));
        Assert.Equal(["Integer", "Integer"], source.MapItem(m => m).Paths.Select(p => p.FullPath));
        Assert.Equal(["Holder->getNumbers[0]"], source.FilterItemNullSafe(m => m.Get() == 1).Paths.Select(p => p.FullPath));
    }

    [Fact]
    public void BuilderProvenance_AcrossOperatorsThatRerootPaths_RemainsAttachedToTheValues()
    {
        TestChild child = TestChild.CreateBuilder().SetName("child").Build();
        TestPojo parent = TestPojo.CreateBuilder().SetStringAttr("parent").SetChildren([child]).Build();

        MapperC<TestPojo?> mapped = MapperC<TestPojo?>.OfOutputBuilder([parent]).MapItem(m => m);
        MapperC<string?> sorted = MapperC<string?>.OfOutputBuilder(["b", "a"]).Sort();
        MapperC<TestChild?> flattened = MapperC<TestPojo?>.OfOutputBuilder([parent])
            .MapItemToList(m => m.MapC<TestChild?>("getChildren", p => p!.Children))
            .FlattenList();

        Assert.All(((IMapperShape)mapped).Items, item => Assert.True(item.FromOutputBuilder));
        Assert.All(((IMapperShape)sorted).Items, item => Assert.True(item.FromOutputBuilder));
        Assert.All(((IMapperShape)flattened).Items, item => Assert.True(item.FromOutputBuilder));
        Assert.Equal(["TestPojo"], mapped.Paths.Select(path => path.FullPath));
    }

    [Fact]
    public void Sum_OverAnEmptyList_IsZeroRatherThanAbsent()
    {
        Assert.Equal(0, MapperC<int?>.Of([]).SumInt().Get());
        Assert.Equal(0m, MapperC<decimal?>.Of([]).SumDecimal().Get());
    }

    [Fact]
    public void Sum_OverScaledDecimals_AddsExactlyAndPreservesTheScale()
    {
        MapperC<decimal?> numbers = MapperC<decimal?>.Of([1.50m, 2.25m]);

        Assert.Equal("3.75", numbers.SumDecimal().Get()!.Value.ToString(Culture.Invariant));
    }

    [Fact]
    public void Join_WithAPresentThenAnAbsentDelimiter_UsesItThenTreatsItAsEmpty()
    {
        MapperC<string?> parts = MapperC<string?>.Of(["a", "b"]);

        Assert.Equal("a-b", parts.Join(MapperS<string?>.Of("-")).Get());
        Assert.Equal("ab", parts.Join(MapperS<string?>.OfNull()).Get());
    }

    [Fact]
    public void Reduce_OverASingleItem_ReturnsItWithoutInvokingTheFunction()
    {
        MapperS<int?> reduced = MapperC<int?>.Of([7])
            .Reduce<int?>((a, b) => throw new InvalidOperationException("must not run"));

        Assert.Equal(7, reduced.Get());
    }

    [Fact]
    public void Reduce_OverSeveralItems_FoldsThemWithTheFunction()
    {
        MapperS<int?> reduced = MapperC<int?>.Of([1, 2, 3])
            .Reduce<int?>((a, b) => MapperS<int?>.Of(a.Get() + b.Get()));

        Assert.Equal(6, reduced.Get());
    }

    [Fact]
    public void ListOfLists_BuiltWithMapItemToList_FlattensAndFiltersByInnerList()
    {
        MapperListOfLists<int?> lists = MapperC<Holder?>
            .Of([new Holder(null) { Numbers = [1, 2] }, new Holder(null) { Numbers = [3] }])
            .MapItemToList(m => m.MapC<int?>("getNumbers", h => h!.Numbers));

        Assert.Equal([1, 2, 3], lists.FlattenList().GetMulti());
        Assert.Equal([[1, 2]], lists.FilterListNullSafe(c => c.ResultCount == 2).GetMulti());
    }

    [Fact]
    public void CheckedMap_ForWhitespaceAndOverflow_ReturnsErrorItems()
    {
        MapperS<int?> whitespace = MapperS<string?>.Of(" 7")
            .CheckedMap<int?, FormatException>("to-int", value => RuneConvert.ParseInt(value!));
        MapperS<int?> overflow = MapperS<string?>.Of("99999999999")
            .CheckedMap<int?, FormatException>("to-int", value => RuneConvert.ParseInt(value!));

        Assert.Equal(0, whitespace.ResultCount);
        Assert.Single(whitespace.ErrorPaths);
        Assert.Equal(0, overflow.ResultCount);
        Assert.Single(overflow.ErrorPaths);
    }

    private sealed class Holder(string? name)
    {
        public string? Name { get; } = name;

        public Holder? Inner { get; init; }

        public IReadOnlyList<int?>? Numbers { get; init; }
    }
}
