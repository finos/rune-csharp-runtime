namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Carries the Rune JSON wire name of a generated property.
/// Mirrors com.rosetta.model.lib.annotations.RuneAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class RuneAttributeAttribute : Attribute
{
    public RuneAttributeAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Serialized property name, e.g. "value" or "@data".</summary>
    public string Name { get; }
}
