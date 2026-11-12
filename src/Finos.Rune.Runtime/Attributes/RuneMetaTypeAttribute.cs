namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Marks a property whose value is a metadata container (MetaFields or a meta wrapper),
/// serialized inline into the parent's metadata section by the Rune JSON format.
/// Mirrors com.rosetta.model.lib.annotations.RuneMetaType.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class RuneMetaTypeAttribute : Attribute
{
}
