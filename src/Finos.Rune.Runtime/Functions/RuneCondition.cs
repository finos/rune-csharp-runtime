using Finos.Rune.Runtime.Expression;

namespace Finos.Rune.Runtime.Functions;

/// <summary>
/// Thrown when a function's pre- or post-condition does not hold.
/// </summary>
/// <remarks>
/// Mirrors <c>ConditionValidator.ConditionException</c>. A condition on a function is a contract on
/// its caller, not a validation report — unlike a condition on a data type, which produces a
/// <see cref="Validation.IValidationResult"/> — so a breach is an exception rather than a value.
/// </remarks>
public class ConditionException : Exception
{
    public ConditionException()
    {
    }

    public ConditionException(string message)
        : base(message)
    {
    }

    public ConditionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Evaluates the pre- and post-conditions declared by a Rune function.</summary>
public interface IConditionValidator
{
    /// <summary>
    /// Applies the configured policy to <paramref name="condition"/>.
    /// </summary>
    void Validate(Func<ComparisonResult> condition, string description);
}

/// <summary>Java-equivalent condition policy: false throws and an absent result passes.</summary>
public sealed class DefaultConditionValidator : IConditionValidator
{
    /// <inheritdoc />
    /// <remarks>
    /// <para><c>GetOrDefault(true)</c>, so a condition over absent data passes. That is the whole
    /// reason this is not a plain <c>if</c>: a Rune condition is three-valued, and an unknown must not
    /// fail the call.</para>
    /// <para>The condition arrives as a delegate rather than a value to keep the reference's laziness —
    /// nothing evaluates it unless the check runs.</para>
    /// </remarks>
    public void Validate(Func<ComparisonResult> condition, string description)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (!condition().GetOrDefault(true))
        {
            throw new ConditionException(description);
        }
    }
}
