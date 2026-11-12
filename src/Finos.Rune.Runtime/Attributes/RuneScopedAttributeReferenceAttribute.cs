namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Marks a property generated from [metadata address "pointsTo"=...]: its value is a
/// scoped reference ("@ref:scoped") resolved against a scoped key elsewhere in the
/// document. Mirrors com.rosetta.model.lib.annotations.RuneScopedAttributeReference.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class RuneScopedAttributeReferenceAttribute : Attribute
{
}
