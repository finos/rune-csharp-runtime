using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Functions;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Functions;

/// <summary>
/// Pre- and post-conditions on a generated function.
/// </summary>
/// <remarks>
/// A condition on a function is a contract on its caller, so a breach throws — unlike a condition on
/// a data type, which becomes a validation result. The three-valued part is the subtle bit: a
/// condition over absent data is unknown, and unknown must not fail the call.
/// </remarks>
public class RuneConditionTests
{
    private static readonly IConditionValidator Validator = new DefaultConditionValidator();

    [Fact]
    public void Validate_WhenTheConditionHolds_DoesNotThrow()
    {
        Validator.Validate(() => ComparisonResult.Success(), "always");
    }

    [Fact]
    public void Validate_WhenTheConditionIsBreached_ThrowsWithTheConditionsDescription()
    {
        ConditionException error = Assert.Throws<ConditionException>(
            () => Validator.Validate(() => ComparisonResult.Failure("ignored"), "the description"));

        Assert.Equal("the description", error.Message);
    }

    [Fact]
    public void Validate_WhenTheConditionIsUnknownOverAbsentData_DoesNotThrow()
    {
        Validator.Validate(() => ComparisonResult.OfEmpty(), "unknown");
    }

    [Fact]
    public void Validate_ForAPassingCondition_EvaluatesItExactlyOnce()
    {
        int calls = 0;

        Validator.Validate(
            () => { calls++; return ComparisonResult.Success(); },
            "counted");

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Validate_WithANullConditionDelegate_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Validator.Validate(null!, "x"));
    }
}
