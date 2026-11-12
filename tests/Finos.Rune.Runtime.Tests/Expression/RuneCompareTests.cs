using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using System.Numerics;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// The ordering operators. The right-hand side is reduced to a single bound before comparing, and
/// which bound depends on the quantifier — that reduction is the part a naive port omits.
/// </summary>
public class RuneCompareTests
{
    private static MapperC<int?> C(params int?[] values) => MapperC<int?>.Of(values);

    private static MapperS<int?> S(int value) => MapperS<int?>.Of(value);

    [Fact]
    public void GreaterThan_WithAll_ComparesEveryLeftValueAgainstTheLargestRight()
    {
        Assert.True(RuneCompare.GreaterThan(C(10, 11), C(1, 5), CardinalityOperator.All).Get());
        Assert.False(RuneCompare.GreaterThan(C(10, 3), C(1, 5), CardinalityOperator.All).Get());
    }

    [Fact]
    public void GreaterThan_WithAny_ComparesAgainstTheSmallestRight()
    {
        Assert.True(RuneCompare.GreaterThan(C(2, 0), C(1, 5), CardinalityOperator.Any).Get());
        Assert.False(RuneCompare.GreaterThan(C(0, 1), C(1, 5), CardinalityOperator.Any).Get());
    }

    [Fact]
    public void LessThan_AcrossAllAndAny_ReducesTheRightSideToTheOppositeBound()
    {
        Assert.True(RuneCompare.LessThan(C(0), C(1, 5), CardinalityOperator.All).Get());
        Assert.False(RuneCompare.LessThan(C(3), C(1, 5), CardinalityOperator.All).Get());
        Assert.True(RuneCompare.LessThan(C(3), C(1, 5), CardinalityOperator.Any).Get());
    }

    [Fact]
    public void Comparisons_ForTwoEqualValues_PassOnlyInTheirInclusiveForms()
    {
        Assert.True(RuneCompare.GreaterThanEquals(S(5), S(5), CardinalityOperator.All).Get());
        Assert.False(RuneCompare.GreaterThan(S(5), S(5), CardinalityOperator.All).Get());
        Assert.True(RuneCompare.LessThanEquals(S(5), S(5), CardinalityOperator.All).Get());
        Assert.False(RuneCompare.LessThan(S(5), S(5), CardinalityOperator.All).Get());
    }

    [Fact]
    public void GreaterThan_WithAnAbsentOperand_IsFalseRatherThanEmpty()
    {
        ComparisonResult result = RuneCompare.GreaterThan(
            MapperS<int?>.OfNull(), S(5), CardinalityOperator.All);

        Assert.False(result.Get());
        Assert.False(result.IsEmptyOperand);
        Assert.False(result.GetOrDefault(true));
        Assert.Equal("Null operand: [[] : null] > [[Integer] : 5]", result.Error);
    }

    [Fact]
    public void GreaterThan_WhenTheComparisonFails_NamesBothSidesInTheError()
    {
        ComparisonResult result = RuneCompare.GreaterThan(C(1), C(5), CardinalityOperator.All);

        Assert.Equal(
            "all elements of paths [Integer] values [1] are not > than "
            + "all elements of paths [Integer] values [5]",
            result.Error);
    }

    [Fact]
    public void Comparisons_ForNumbersDifferingOnlyInScale_TreatThemAsEqual()
    {
        MapperS<decimal?> a = MapperS<decimal?>.Of(1.0m);
        MapperS<decimal?> b = MapperS<decimal?>.Of(1.00m);

        Assert.True(RuneCompare.GreaterThanEquals(a, b, CardinalityOperator.All).Get());
        Assert.True(RuneCompare.LessThanEquals(a, b, CardinalityOperator.All).Get());
        Assert.False(RuneCompare.GreaterThan(a, b, CardinalityOperator.All).Get());
    }

    [Fact]
    public void LessThan_ForAnIntAgainstANumber_ComparesWithoutGoingThroughDouble()
    {
        Assert.True(RuneCompare.LessThan(
            MapperS<int?>.Of(1),
            MapperS<decimal?>.Of(1.5m),
            CardinalityOperator.All).Get());
    }

    [Fact]
    public void Comparisons_ForBigIntegersOutsideDecimalRange_OrderWithoutCrossTypeExceptions()
    {
        BigInteger above = new BigInteger(decimal.MaxValue) + BigInteger.One;
        BigInteger below = new BigInteger(decimal.MinValue) - BigInteger.One;
        MapperS<decimal?> maximum = MapperS<decimal?>.Of(decimal.MaxValue);
        MapperS<decimal?> minimum = MapperS<decimal?>.Of(decimal.MinValue);

        Assert.True(RuneCompare.GreaterThan(
            MapperS<BigInteger?>.Of(above), maximum, CardinalityOperator.All).Get());
        Assert.True(RuneCompare.LessThan(
            MapperS<BigInteger?>.Of(below), minimum, CardinalityOperator.All).Get());
        Assert.True(RuneCompare.LessThan(
            maximum, MapperS<BigInteger?>.Of(above), CardinalityOperator.All).Get());
    }

    [Fact]
    public void LessThan_ForTwoStrings_OrdersThemOrdinally()
    {
        Assert.True(RuneCompare.LessThan(
            MapperS<string?>.Of("B"), MapperS<string?>.Of("a"), CardinalityOperator.All).Get());
    }
}
