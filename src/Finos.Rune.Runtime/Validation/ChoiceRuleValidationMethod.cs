namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// How many options of a choice may be set.
/// Mirrors com.rosetta.model.lib.validation.ChoiceRuleValidationMethod.
/// </summary>
public enum ChoiceRuleValidationMethod
{
    /// <summary>Rune <c>optional choice</c>: zero or one.</summary>
    Optional,

    /// <summary>Rune <c>required choice</c> and <c>one-of</c>: exactly one.</summary>
    Required,
}

/// <summary>Descriptions and the field-count check for <see cref="ChoiceRuleValidationMethod"/>.</summary>
public static class ChoiceRuleValidationMethodExtensions
{
    /// <summary>The description embedded verbatim in failure messages.</summary>
    public static string GetDescription(this ChoiceRuleValidationMethod method) => method switch
    {
        ChoiceRuleValidationMethod.Optional => "Zero or one field must be set",
        ChoiceRuleValidationMethod.Required => "One and only one field must be set",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown choice rule validation method."),
    };

    /// <summary>True when <paramref name="fields"/> set fields satisfies the method.</summary>
    public static bool Check(this ChoiceRuleValidationMethod method, int fields) => method switch
    {
        ChoiceRuleValidationMethod.Optional => fields is 1 or 0,
        ChoiceRuleValidationMethod.Required => fields == 1,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown choice rule validation method."),
    };
}
