using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// The list projection generated coercions use, and the absence case that motivates it.
/// </summary>
/// <remarks>
/// A Rune list attribute reads as an empty list in Java when its value is absent, but as
/// <c>null</c> in C#. <c>Enumerable.Select</c> throws on a null source, so a generated widening
/// built on it turned every absent list into a validation failure with "Value cannot be null.
/// (Parameter 'source')" — 55 of them across the CDM pack, against zero on the Java side.
/// </remarks>
public class RuneListTests
{
    private enum Weekday
    {
        Monday,
        Tuesday,
    }

    [Fact]
    public void Map_ForAnAbsentSource_IsAbsentRatherThanAnEmptyList()
    {
        Assert.Null(RuneList.Map<Weekday, Weekday?>(null, d => d));
    }

    [Fact]
    public void Map_ForAnEmptySource_IsAnEmptyListNotAbsence()
    {
        IReadOnlyList<Weekday?>? result = RuneList.Map<Weekday, Weekday?>([], d => d);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Map_OverSeveralElements_ProjectsThemInSourceOrder()
    {
        IReadOnlyList<Weekday?>? result =
            RuneList.Map<Weekday, Weekday?>([Weekday.Tuesday, Weekday.Monday], d => d);

        Assert.Equal([Weekday.Tuesday, Weekday.Monday], result);
    }

    [Fact]
    public void Map_WhenWideningToANullableItem_ProducesAListOfTheNullableType()
    {
        IReadOnlyList<Weekday> narrow = [Weekday.Monday];

        IReadOnlyList<Weekday?>? wide = RuneList.Map<Weekday, Weekday?>(narrow, d => d);

        Assert.Equal(typeof(List<Weekday?>), wide!.GetType());
    }

    [Fact]
    public void Map_WhenItsAbsentResultReachesAMapper_ReadsAsNotExisting()
    {
        MapperC<Weekday?> mapper = MapperC<Weekday?>.Of(RuneList.Map<Weekday, Weekday?>(null, d => d));

        Assert.False(RuneExpr.Exists(mapper).GetOrDefault(false));
    }

    [Fact]
    public void Map_AfterAnAbsentNullableMapperIsFlattened_DoesNotFabricateADefaultValue()
    {
        IReadOnlyList<int?> nullableValues = MapperS<int?>.OfNull().GetMulti();

        IReadOnlyList<int>? values = RuneList.Map<int?, int>(nullableValues, value => value!.Value);

        Assert.NotNull(values);
        Assert.Empty(values);
    }

    [Fact]
    public void Map_WithANullSelectorAndNothingToProject_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RuneList.Map<Weekday, Weekday?>(null, null!));
    }

    [Fact]
    public void AddRange_AcrossSuccessiveCalls_AppendsEveryItem()
    {
        var target = new List<int> { 1 };

        RuneList.AddRange(target, [2, 3]);
        RuneList.AddRange(target, [4]);

        Assert.Equal([1, 2, 3, 4], target);
    }

    [Fact]
    public void AddRange_ForAnAbsentSource_LeavesTheTargetUnchanged()
    {
        var target = new List<int> { 1 };

        RuneList.AddRange(target, null);

        Assert.Equal([1], target);
    }

    [Fact]
    public void ReplaceWith_OverAPopulatedTarget_DiscardsTheEarlierItems()
    {
        var target = new List<int> { 1, 2 };

        RuneList.ReplaceWith(target, [9]);

        Assert.Equal([9], target);
    }

    [Fact]
    public void ReplaceWith_ForAnAbsentSource_EmptiesTheTarget()
    {
        var target = new List<int> { 1, 2 };

        RuneList.ReplaceWith(target, null);

        Assert.Empty(target);
    }

    [Fact]
    public void AddRangeAndReplaceWith_WithANullTarget_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => RuneList.AddRange<int>(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => RuneList.ReplaceWith<int>(null!, [1]));
    }
}
