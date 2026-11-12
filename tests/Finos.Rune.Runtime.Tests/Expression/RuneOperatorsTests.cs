using System.Text.RegularExpressions;
using Finos.Rune.Runtime.Expression;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// Message-parity tests. Every expected string here was copied out of
/// ExpressionOperatorsNullSafe.java; the cross-language sweep compares these against the Java
/// implementation's output over the whole CDM test pack, and CDM's own ModelClassValidationTest
/// pins the required-field wording. A failure here means the wire text drifted.
/// </summary>
public class RuneOperatorsTests
{
    [Fact]
    public void CheckCardinality_ForCountsWithinTheBounds_Passes()
    {
        Assert.True(RuneOperators.CheckCardinality("attr", 1, 1, 1).Get());
        Assert.True(RuneOperators.CheckCardinality("attr", 0, 0, 1).Get());
        Assert.True(RuneOperators.CheckCardinality("attr", 3, 1, 0).Get());
    }

    [Fact]
    public void CheckCardinality_WhenARequiredFieldIsAbsent_ReportsItAsRequired()
    {
        ComparisonResult result = RuneOperators.CheckCardinality("product", 0, 1, 1);

        Assert.False(result.Get());
        Assert.Equal("'product' is a required field but does not exist.", result.Error);
    }

    [Fact]
    public void CheckCardinality_WhenBelowTheLowerBound_ReportsTheExpectedMinimum()
    {
        ComparisonResult result = RuneOperators.CheckCardinality("leg", 1, 2, 0);

        Assert.False(result.Get());
        Assert.Equal("Minimum of 2 'leg' is expected but found 1.", result.Error);
    }

    [Fact]
    public void CheckCardinality_WhenAboveTheUpperBound_ReportsTheExpectedMaximum()
    {
        ComparisonResult result = RuneOperators.CheckCardinality("leg", 3, 0, 2);

        Assert.False(result.Get());
        Assert.Equal("Maximum of 2 'leg' are expected but found 3.", result.Error);
    }

    [Fact]
    public void CheckCardinality_WithAnUpperBoundOfZero_TreatsItAsUnbounded()
    {
        Assert.True(RuneOperators.CheckCardinality("attr", 99, 0, 0).Get());
    }

    [Fact]
    public void CheckString_ForANullValue_Passes()
    {
        Assert.True(RuneOperators.CheckString("attr", (string?)null, 5, 10, null).Get());
    }

    [Fact]
    public void CheckString_WhenShorterThanTheMinimumLength_ReportsBothLengths()
    {
        ComparisonResult result = RuneOperators.CheckString("attr", "ab", 3, null, null);

        Assert.Equal(
            "Field 'attr' requires a value with minimum length of 3 characters but value 'ab' has length of 2 characters.",
            result.Error);
    }

    [Fact]
    public void CheckString_WhenLongerThanTheMaximumLength_ReportsBothLengths()
    {
        ComparisonResult result = RuneOperators.CheckString("attr", "abcd", 0, 3, null);

        Assert.Equal(
            "Field 'attr' must have a value with maximum length of 3 characters but value 'abcd' has length of 4 characters.",
            result.Error);
    }

    [Fact]
    public void CheckString_WhenTheValueDoesNotMatchThePattern_ReportsTheValueAndThePattern()
    {
        ComparisonResult result = RuneOperators.CheckString("attr", "xy", 0, null, new Regex("[0-9]+"));

        Assert.Equal("Field 'attr' with value 'xy' does not match the pattern /[0-9]+/.", result.Error);
    }

    [Fact]
    public void CheckString_WhenThePatternMatchesOnlyPartOfTheValue_Fails()
    {
        Assert.False(RuneOperators.CheckString("attr", "a1b", 0, null, new Regex("[0-9]+")).Get());
        Assert.True(RuneOperators.CheckString("attr", "123", 0, null, new Regex("[0-9]+")).Get());
    }

    [Fact]
    public void CheckString_WhenTheLeftmostAlternativeIsShorterThanTheValue_StillMatchesByBacktracking()
    {
        Assert.True(RuneOperators.CheckString("attr", "ab", 0, null, new Regex("a|ab")).Get());
    }

    [Fact]
    public void CheckString_ForAValueWithATrailingNewline_Fails()
    {
        Assert.False(RuneOperators.CheckString("attr", "123\n", 0, null, new Regex("[0-9]+")).Get());
    }

    [Fact]
    public void CheckString_WhenAnInternallyAnchoredPatternFails_QuotesThePatternAsWritten()
    {
        ComparisonResult result = RuneOperators.CheckString("attr", "xy", 0, null, new Regex("[0-9]+"));

        Assert.Equal("Field 'attr' with value 'xy' does not match the pattern /[0-9]+/.", result.Error);
    }

    [Fact]
    public void CheckString_WhenBothLengthBoundsAreViolated_JoinsTheMessagesWithASpace()
    {
        ComparisonResult result = RuneOperators.CheckString("attr", "abcd", 10, 3, null);

        Assert.Equal(
            "Field 'attr' requires a value with minimum length of 10 characters but value 'abcd' has length of 4 characters."
            + " Field 'attr' must have a value with maximum length of 3 characters but value 'abcd' has length of 4 characters.",
            result.Error);
    }

    [Fact]
    public void CheckStringOverAList_WhenSeveralElementsFail_JoinsTheMessagesWithADash()
    {
        ComparisonResult result = RuneOperators.CheckString("attr", ["ab", "cd"], 3, null, null);

        Assert.Equal(
            "Field 'attr' requires a value with minimum length of 3 characters but value 'ab' has length of 2 characters."
            + " - Field 'attr' requires a value with minimum length of 3 characters but value 'cd' has length of 2 characters.",
            result.Error);
    }

    [Fact]
    public void CheckStringOverAList_ForANullListAndForAllValidElements_Passes()
    {
        Assert.True(RuneOperators.CheckString("attr", (IReadOnlyList<string?>?)null, 3, null, null).Get());
        Assert.True(RuneOperators.CheckString("attr", ["abc", "defg"], 3, null, null).Get());
    }

    [Fact]
    public void CheckNumber_ForANullValue_Passes()
    {
        Assert.True(RuneOperators.CheckNumber("attr", (decimal?)null, 2, 1, 0m, 1m).Get());
    }

    [Fact]
    public void CheckNumber_WhenTheTotalDigitsExceedTheMaximum_ReportsBothCounts()
    {
        ComparisonResult result = RuneOperators.CheckNumber("attr", 123.45m, 4, null, null, null);

        Assert.Equal("Expected a maximum of 4 digits for 'attr', but the number 123.45 has 5.", result.Error);
    }

    [Fact]
    public void CheckNumber_WhenTheFractionalDigitsExceedTheMaximum_ReportsBothCounts()
    {
        ComparisonResult result = RuneOperators.CheckNumber("attr", 1.234m, null, 2, null, null);

        Assert.Equal(
            "Expected a maximum of 2 fractional digits for 'attr', but the number 1.234 has 3.", result.Error);
    }

    [Fact]
    public void CheckNumber_WhenBelowTheMinimum_ReportsTheBoundAndTheValue()
    {
        ComparisonResult result = RuneOperators.CheckNumber("attr", -1m, null, null, 0m, null);

        Assert.Equal("Expected a number greater than or equal to 0 for 'attr', but found -1.", result.Error);
    }

    [Fact]
    public void CheckNumber_WhenAboveTheMaximum_ReportsTheBoundAndTheValue()
    {
        ComparisonResult result = RuneOperators.CheckNumber("attr", 1.5m, null, null, null, 1m);

        Assert.Equal("Expected a number less than or equal to 1 for 'attr', but found 1.5.", result.Error);
    }

    [Fact]
    public void CheckNumber_ForValuesExactlyOnTheBounds_Passes()
    {
        Assert.True(RuneOperators.CheckNumber("attr", 0m, null, null, 0m, 1m).Get());
        Assert.True(RuneOperators.CheckNumber("attr", 1m, null, null, 0m, 1m).Get());
    }

    [Fact]
    public void CheckNumber_ForAValueWithATrailingZero_KeepsItsScaleInTheMessage()
    {
        ComparisonResult result = RuneOperators.CheckNumber("attr", 1.50m, null, null, null, 1m);

        Assert.Equal("Expected a number less than or equal to 1 for 'attr', but found 1.50.", result.Error);
    }

    [Fact]
    public void CheckNumber_WhenBothDigitLimitsAreViolated_JoinsTheMessagesWithASpace()
    {
        ComparisonResult result = RuneOperators.CheckNumber("attr", 123.456m, 4, 2, null, null);

        Assert.Equal(
            "Expected a maximum of 4 digits for 'attr', but the number 123.456 has 6."
            + " Expected a maximum of 2 fractional digits for 'attr', but the number 123.456 has 3.",
            result.Error);
    }

    [Fact]
    public void CheckNumberOverAList_WhenSeveralElementsFail_JoinsTheMessagesWithADash()
    {
        ComparisonResult result = RuneOperators.CheckNumber(
            "attr", new List<decimal?> { 1.234m, 5.678m }, null, 2, null, null);

        Assert.Equal(
            "Expected a maximum of 2 fractional digits for 'attr', but the number 1.234 has 3."
            + " - Expected a maximum of 2 fractional digits for 'attr', but the number 5.678 has 3.",
            result.Error);
    }

    [Fact]
    public void CheckNumber_ForAnIntegerOverload_AppliesTheDigitLimit()
    {
        Assert.True(RuneOperators.CheckNumber("attr", (int?)null, 2, null, null, null).Get());
        Assert.False(RuneOperators.CheckNumber("attr", 12345, 4, null, null, null).Get());
        Assert.True(RuneOperators.CheckNumber("attr", 1234, 4, null, null, null).Get());
    }
}
