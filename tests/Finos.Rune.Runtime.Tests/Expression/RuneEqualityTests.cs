using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

public class RuneEqualityTests
{
    private static MapperS<int?> S(int v) => MapperS<int?>.Of(v);

    private static MapperC<int?> C(params int?[] v) => MapperC<int?>.Of(v);

    [Fact]
    public void AreEqual_ForNumbersDifferingOnlyInScale_IsTrueWhereContainsIsNot()
    {
        MapperS<decimal?> a = MapperS<decimal?>.Of(1.0m);
        MapperS<decimal?> b = MapperS<decimal?>.Of(1.00m);

        Assert.True(RuneEquality.AreEqual(a, b, CardinalityOperator.All).Get());
        Assert.False(RuneExpr.Contains(
            MapperC<decimal?>.Of([1.0m]),
            MapperC<decimal?>.Of([1.00m])).Get());
    }

    [Fact]
    public void AreEqual_ForAnIntAndANumberOfTheSameMagnitude_IsTrue()
    {
        Assert.True(RuneEquality.AreEqual(
            S(1), MapperS<decimal?>.Of(1.00m), CardinalityOperator.All).Get());
    }

    [Fact]
    public void AreEqual_AcrossEmptyOperandCombinations_MatchesTwoEmptiesButRejectsOne()
    {
        Assert.True(RuneEquality.AreEqual(
            MapperS<int?>.OfNull(), MapperS<int?>.OfNull(), CardinalityOperator.All).Get());

        ComparisonResult mixed = RuneEquality.AreEqual(
            MapperS<int?>.OfNull(), S(1), CardinalityOperator.All);
        Assert.False(mixed.Get());
        Assert.Contains("cannot be compared to", mixed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void NotEqual_WithOneEmptyOperand_IsTrueWithNoError()
    {
        ComparisonResult result = RuneEquality.NotEqual(
            MapperS<int?>.OfNull(), S(1), CardinalityOperator.Any);

        Assert.True(result.Get());
        Assert.Null(result.Error);
    }

    [Fact]
    public void AreEqual_AcrossAllAndAnyOverTwoLists_RequiresEveryPairOrJustOne()
    {
        Assert.True(RuneEquality.AreEqual(C(1, 2), C(1, 2), CardinalityOperator.All).Get());
        Assert.False(RuneEquality.AreEqual(C(1, 3), C(1, 2), CardinalityOperator.All).Get());
        Assert.True(RuneEquality.AreEqual(C(1, 3), C(1, 2), CardinalityOperator.Any).Get());
        Assert.False(RuneEquality.AreEqual(C(4, 3), C(1, 2), CardinalityOperator.Any).Get());
    }

    [Fact]
    public void AreEqual_ForListsOfDifferentLength_ReportsThemAsUncomparable()
    {
        ComparisonResult result = RuneEquality.AreEqual(C(1, 2, 3), C(1, 2), CardinalityOperator.All);

        Assert.False(result.Get());
        Assert.Contains("cannot be compared to", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AreEqual_ForAListAgainstASingle_ChecksEveryElementInEitherOrder()
    {
        Assert.True(RuneEquality.AreEqual(C(1, 1), S(1), CardinalityOperator.All).Get());
        Assert.False(RuneEquality.AreEqual(C(1, 2), S(1), CardinalityOperator.All).Get());
        Assert.True(RuneEquality.AreEqual(C(1, 2), S(1), CardinalityOperator.Any).Get());
        Assert.True(RuneEquality.AreEqual(S(1), C(1, 1), CardinalityOperator.All).Get());
    }

    [Fact]
    public void AreEqual_ForAnEmptyListAgainstASingle_ReportsThemAsUncomparable()
    {
        ComparisonResult result = RuneEquality.AreEqual(C(), S(1), CardinalityOperator.All);

        Assert.False(result.Get());
        Assert.Contains("cannot be compared to", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AreEqual_ForTheSameInstantAtTwoOffsets_MatchesWithinAShapeButNotAcross()
    {
        var utc = new DateTimeOffset(2007, 12, 3, 10, 15, 30, TimeSpan.Zero);
        var paris = new DateTimeOffset(2007, 12, 3, 11, 15, 30, TimeSpan.FromHours(1));

        Assert.True(RuneEquality.AreEqual(
            MapperS<DateTimeOffset?>.Of(utc),
            MapperS<DateTimeOffset?>.Of(paris),
            CardinalityOperator.All).Get());

        Assert.False(RuneEquality.AreEqual(
            MapperC<DateTimeOffset?>.Of([utc]),
            MapperS<DateTimeOffset?>.Of(paris),
            CardinalityOperator.All).Get());
    }

    [Fact]
    public void AreEqual_WhenTheValuesDiffer_NamesBothSidesInTheError()
    {
        ComparisonResult result = RuneEquality.AreEqual(S(1), S(2), CardinalityOperator.All);

        Assert.Equal("[Integer] [1] does not equal [Integer] [2]", result.Error);
    }

    [Fact]
    public void NotEqual_WhenTheValuesMatch_SaysShouldNotEqualRatherThanDoesNot()
    {
        ComparisonResult result = RuneEquality.NotEqual(S(1), S(1), CardinalityOperator.All);

        Assert.Equal("[Integer] [1] should not equal [Integer] [1]", result.Error);
    }
}
