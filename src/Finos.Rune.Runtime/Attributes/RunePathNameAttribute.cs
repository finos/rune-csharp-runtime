namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Overrides the name a property contributes to a <see cref="Path.RosettaPath"/> when it differs
/// from the wire name.
/// </summary>
/// <remarks>
/// Generated model types never need this: Java builds path elements from the Rune attribute name,
/// which is also the wire name. It exists for the runtime's own meta wrappers, where the two
/// diverge — Java names the members <c>value</c>, <c>globalReference</c>, <c>externalReference</c>
/// and <c>reference</c>, while the wire names them <c>@data</c>, <c>@ref</c>, <c>@ref:external</c>
/// and <c>@ref:scoped</c>. Without this, every validation result underneath a metadata-wrapped
/// attribute would carry a path that does not match the Java reference.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class RunePathNameAttribute : Attribute
{
    public RunePathNameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Path element name, e.g. "globalReference".</summary>
    public string Name { get; }
}
