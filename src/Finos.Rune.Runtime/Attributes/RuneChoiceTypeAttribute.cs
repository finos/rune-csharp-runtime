namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Marks a generated Rune choice type: exactly one of its alternative properties is set,
/// and the Rune JSON format serializes the set alternative in unwrapped form.
/// Mirrors com.rosetta.model.lib.annotations.RuneChoiceType.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class RuneChoiceTypeAttribute : Attribute
{
}
