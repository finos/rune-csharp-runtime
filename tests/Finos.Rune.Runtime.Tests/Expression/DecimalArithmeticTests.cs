using System.Numerics;
using System.Globalization;
using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// Decimal arithmetic semantics selected for both the generated model and expression tiers.
/// </summary>
public class DecimalArithmeticTests
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private static decimal N(string text) => decimal.Parse(text, Styles, Culture.Invariant);

    private static string Text(decimal value) => value.ToString(Culture.Invariant);

    [Theory]
    [InlineData("0", 0, 0, 1)]
    [InlineData("0.00", 0, 0, 1)]
    [InlineData("1.50", 15, 1, 2)]
    [InlineData("12000", 12, -3, 5)]
    [InlineData("0.0012", 12, 4, 5)]
    [InlineData("1E+10", 1, -10, 11)]
    [InlineData("1E-7", 1, 7, 8)]
    [InlineData("-0.0012", -12, 4, 5)]
    [InlineData("-1", -1, 0, 1)]
    public void Parse_AcrossRepresentativeLiterals_UsesDecimalAndRetainsRepresentableScale(
        string text, long unscaled, int scale, int digits)
    {
        decimal value = N(text);
        (BigInteger actualUnscaled, int actualScale) = RuneDecimal.StripTrailingZeros(value);

        Assert.Equal(new BigInteger(unscaled), actualUnscaled);
        Assert.Equal(scale, actualScale);
        Assert.Equal(digits, RuneDecimal.CountDigits(value));
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("")]
    [InlineData("1.2.3")]
    [InlineData("1e")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    public void Parse_ForMalformedOrWhitespacePaddedText_FailsToParse(string text)
    {
        Assert.False(decimal.TryParse(text, Styles, Culture.Invariant, out _));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("0.00", "0.00")]
    [InlineData("1.50", "1.50")]
    [InlineData("12000", "12000")]
    [InlineData("0.0012", "0.0012")]
    [InlineData("1E+10", "10000000000")]
    [InlineData("1E+3", "1000")]
    [InlineData("1E-7", "0.0000001")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("0.0000001", "0.0000001")]
    [InlineData("-0.0012", "-0.0012")]
    public void Rendering_AcrossPlainAndExponentInputs_UsesDecimalPlainNotation(
        string text, string expected)
    {
        Assert.Equal(expected, Text(N(text)));
    }

    [Theory]
    [InlineData("1.50", "2", "3.00")]
    [InlineData("1.50", "0.5", "0.750")]
    [InlineData("2", "3", "6")]
    public void Multiply_AcrossMixedScales_IsExactAndAddsTheScales(string a, string b, string expected)
    {
        Assert.Equal(expected, Text(N(a) * N(b)));
    }

    [Theory]
    [InlineData("1.50", "2.5", "4.00")]
    [InlineData("0.1", "0.2", "0.3")]
    [InlineData("1", "0.001", "1.001")]
    public void Add_AcrossMixedScales_IsExactAndTakesTheLargerScale(string a, string b, string expected)
    {
        Assert.Equal(expected, Text(N(a) + N(b)));
    }

    [Theory]
    [InlineData("1.50", "0.5", "1.00")]
    [InlineData("1", "1", "0")]
    public void Subtract_AcrossMixedScales_IsExactAndTakesTheLargerScale(string a, string b, string expected)
    {
        Assert.Equal(expected, Text(N(a) - N(b)));
    }

    [Fact]
    public void Divide_For505Point1By18_UsesTheAcceptedDecimalPrecision()
    {
        decimal quotient = N("505.1") / N("18");

        // Deliberately differs from Java's 34-digit DECIMAL128 result: decimal keeps 29 digits.
        Assert.Equal("28.061111111111111111111111111", Text(quotient));
    }

    [Fact]
    public void Divide_ForANonTerminatingQuotient_RoundsToDecimalPrecision()
    {
        Assert.Equal(
            "0.3333333333333333333333333333",
            Text(1m / 3m));
    }

    [Fact]
    public void Parse_ForAMagnitudeBelowDecimalScale_UnderflowsToZero()
    {
        Assert.Equal(0m, N("1E-30"));
    }

    [Fact]
    public void Multiply_WhenTheResultExceedsDecimalRange_ThrowsOverflowException()
    {
        decimal value = decimal.MaxValue;
        Assert.Throws<OverflowException>(() => _ = value * 10m);
    }

    [Fact]
    public void Divide_WhenTheQuotientIsExact_StripsBackToThePreferredScale()
    {
        Assert.Equal("1", Text(2m / 2m));
        Assert.Equal("2.5", Text(5m / 2m));
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        decimal zero = 0m;
        Assert.Throws<DivideByZeroException>(() => _ = 1m / zero);
    }

    [Fact]
    public void Divide_WhenTheDividendIsZero_KeepsThePreferredScale()
    {
        Assert.Equal("0.0", Text(N("0.00") / N("1.0")));
    }

    [Fact]
    public void Equality_ForTheSameValueAtDifferentScales_SeparatesRuneMembershipFromComparison()
    {
        decimal a = N("1.0");
        decimal b = N("1.00");

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(RuneDecimal.EqualsIncludingScale(a, b));
        Assert.True(a <= b);
        Assert.True(a >= b);
    }

    [Fact]
    public void Equality_ForNegativeZeroAgainstPositiveZero_Holds()
    {
        Assert.True(RuneDecimal.EqualsIncludingScale(N("-0.0"), N("0.0")));
    }

    [Fact]
    public void Comparison_AcrossDifferingScalesAndSigns_OrdersByValue()
    {
        Assert.True(N("1.9") < N("10"));
        Assert.True(N("-1") < N("0.0001"));
        Assert.True(N("1E+3") > N("999.99"));
    }

    [Theory]
    [InlineData("1.500", 15, -1 + 2)]
    [InlineData("12000", 12, -3)]
    [InlineData("0.00", 0, 0)]
    public void StripTrailingZeros_AcrossFractionalIntegralAndZeroInputs_MatchesBigDecimal(string text, long unscaled, int scale)
    {
        (BigInteger actualUnscaled, int actualScale) = RuneDecimal.StripTrailingZeros(N(text));

        Assert.Equal(new BigInteger(unscaled), actualUnscaled);
        Assert.Equal(scale, actualScale);
    }

    [Fact]
    public void Divide_AtTheDecimalPrecisionBoundary_UsesBankersRounding()
    {
        Assert.Equal("0.5", Text(1m / 2m));
        Assert.Equal("1.6666666666666666666666666667", Text(5m / 3m));
    }

    [Fact]
    public void DecimalStorage_ForRepresentativeValuesIncludingMaxValue_PreservesValueAndScale()
    {
        foreach (decimal value in new[] { 0m, 1m, -1m, 1.50m, 12000m, 0.0012m, 79228162514264337593543950335m })
        {
            decimal parsed = N(Text(value));
            Assert.Equal(value, parsed);
            Assert.Equal(Text(value), Text(parsed));
        }
    }

    [Fact]
    public void Parsing_ForA34DigitQuotient_RoundsToDecimalPrecision()
    {
        Assert.Equal(
            "28.061111111111111111111111111",
            Text(N("28.06111111111111111111111111111111")));
    }

    [Fact]
    public void Multiply_ForSaturatingOperands_ExposesDecimalNonAssociativity()
    {
        const string aText = "1.0000000000000000000000000001";
        const string bText = "1.0000000000000000000000000001";
        const string cText = "3.3333333333333333333333333333";
        decimal a = N(aText);
        decimal b = N(bText);
        decimal c = N(cText);

        Assert.NotEqual((a * b) * c, a * (b * c));
    }

    [Fact]
    public void Multiply_ForOperandsWhoseExactProductStillFitsDecimal_AgreesWithBigDecimal()
    {
        decimal a = N("0.1");
        decimal b = N("123456789012345678901234567.8");
        decimal c = N("3.7");

        Assert.Equal("45679011934567901193456790.086", Text((a * b) * c));
        Assert.Equal("45679011934567901193456790.086", Text(a * (b * c)));
    }
}
