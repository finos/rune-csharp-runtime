namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// Quantifier applied when a comparison operand holds several values.
/// Mirrors com.rosetta.model.lib.expression.CardinalityOperator.
/// </summary>
public enum CardinalityOperator
{
    /// <summary>Every value must satisfy the comparison. The default for all operators except <c>&lt;&gt;</c>.</summary>
    All,

    /// <summary>At least one value must satisfy the comparison. The default for <c>&lt;&gt;</c>.</summary>
    Any,
}
