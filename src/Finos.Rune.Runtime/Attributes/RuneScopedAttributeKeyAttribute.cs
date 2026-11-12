namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Marks a property generated from [metadata location]: its value carries a scoped key
/// ("@key:scoped") that scoped references within the same document can point to.
/// Mirrors com.rosetta.model.lib.annotations.RuneScopedAttributeKey.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class RuneScopedAttributeKeyAttribute : Attribute
{
}
