using System.Numerics;
using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// Arithmetic over mappers. The first assertion in every test is implicit: that the call compiles
/// at all. The original signature took IMapper&lt;object?&gt;, which covariance cannot satisfy for
/// a struct element type, so every numeric call site was CS1503 — a whole class that built cleanly
/// and could never be invoked.
/// </summary>
public class MapperMathsTests
{
    private static MapperS<decimal?> N(string literal) =>
        MapperS<decimal?>.Of(decimal.Parse(literal, Culture.Invariant));

    private static MapperS<int?> I(int value) => MapperS<int?>.Of(value);

    [Fact]
    public void Add_ForTwoNumbers_IsExactAndKeepsTheLargerScale()
    {
        Assert.Equal("4.00", MapperMaths.Add<decimal?, decimal?, decimal?>(N("1.50"), N("2.5"))
            .Get()!.Value.ToString(Culture.Invariant));
    }

    [Fact]
    public void Multiply_ForTwoNumbers_IsExactAndSumsTheScales()
    {
        Assert.Equal("0.750", MapperMaths.Multiply<decimal?, decimal?, decimal?>(N("1.50"), N("0.5"))
            .Get()!.Value.ToString(Culture.Invariant));
    }

    [Fact]
    public void Subtract_ForTwoNumbers_IsExactAndKeepsTheLargerScale()
    {
        Assert.Equal("1.00", MapperMaths.Subtract<decimal?, decimal?, decimal?>(N("1.50"), N("0.5"))
            .Get()!.Value.ToString(Culture.Invariant));
    }

    [Fact]
    public void Arithmetic_OverTwoInts_StaysInt()
    {
        Assert.Equal(7, MapperMaths.Add<int?, int?, int?>(I(3), I(4)).Get());
        Assert.Equal(12, MapperMaths.Multiply<int?, int?, int?>(I(3), I(4)).Get());
        Assert.Equal(-1, MapperMaths.Subtract<int?, int?, int?>(I(3), I(4)).Get());
    }

    [Fact]
    public void Divide_OverTwoInts_YieldsANumberRatherThanAnInt()
    {
        MapperS<decimal?> quotient = MapperMaths.Divide<decimal?, int?, int?>(I(1), I(3));

        Assert.Equal("0.3333333333333333333333333333",
            quotient.Get()!.Value.ToString(Culture.Invariant));
    }

    [Fact]
    public void Divide_ForTheSecLendingOperands_UsesDecimalPrecision()
    {
        MapperS<decimal?> quotient =
            MapperMaths.Divide<decimal?, decimal?, decimal?>(N("505.1"), N("18"));

        // Deliberately differs from Java's 34-digit DECIMAL128 result: decimal keeps 29 digits.
        Assert.Equal("28.061111111111111111111111111",
            quotient.Get()!.Value.ToString(Culture.Invariant));
    }

    [Fact]
    public void Add_ForAnIntAndANumber_WidensToNumber()
    {
        Assert.Equal(3.5m, MapperMaths.Add<decimal?, int?, decimal?>(I(1), N("2.5")).Get());
    }

    [Fact]
    public void Arithmetic_OverTwoStrings_ConcatenatesOnAddAndThrowsOtherwise()
    {
        MapperS<string?> a = MapperS<string?>.Of("a");
        MapperS<string?> b = MapperS<string?>.Of("b");

        Assert.Equal("ab", MapperMaths.Add<string?, string?, string?>(a, b).Get());
        Assert.Equal(
            "Cant subtract two strings together",
            Assert.Throws<InvalidOperationException>(
                () => MapperMaths.Subtract<string?, string?, string?>(a, b)).Message);
        Assert.Equal(
            "Cant divide two strings",
            Assert.Throws<InvalidOperationException>(
                () => MapperMaths.Divide<string?, string?, string?>(a, b)).Message);
    }

    [Fact]
    public void Add_WithAnAbsentOperand_IsAbsentWithoutRunningTheOperator()
    {
        Assert.Null(
            MapperMaths.Add<decimal?, decimal?, decimal?>(N("1"), MapperS<decimal?>.OfNull()).Get());
    }

    [Fact]
    public void Add_WithAMultiValuedOperand_IsAbsentRatherThanAnError()
    {
        MapperC<int?> two = MapperC<int?>.Of([1, 2]);

        Assert.Null(MapperMaths.Add<int?, int?, int?>(two, I(1)).Get());
    }

    [Fact]
    public void Add_ForAStringAndAnInt_ThrowsNamingTheJavaTypes()
    {
        Assert.Equal(
            "Cant add two random (String, Integer) together",
            Assert.Throws<InvalidOperationException>(
                () => MapperMaths.Add<object?, string?, int?>(MapperS<string?>.Of("a"), I(1))).Message);
    }

    [Fact]
    public void Add_ForADateAndATime_ThrowsRatherThanQuietlyWorking()
    {
        MapperS<DateOnly?> date = MapperS<DateOnly?>.Of(new DateOnly(2024, 1, 1));
        MapperS<TimeOnly?> time = MapperS<TimeOnly?>.Of(new TimeOnly(10, 0));

        Assert.Equal(
            "Cant add two random (DateImpl, LocalTime) together",
            Assert.Throws<InvalidOperationException>(
                () => MapperMaths.Add<object?, DateOnly?, TimeOnly?>(date, time)).Message);
    }

    [Fact]
    public void Add_OverTwoBigIntegers_StaysExact()
    {
        MapperS<BigInteger?> a = MapperS<BigInteger?>.Of(BigInteger.Parse("123456789012345678901234567890"));
        MapperS<BigInteger?> b = MapperS<BigInteger?>.Of(BigInteger.One);

        Assert.Equal(
            BigInteger.Parse("123456789012345678901234567891"),
            MapperMaths.Add<BigInteger?, BigInteger?, BigInteger?>(a, b).Get());
    }
}
