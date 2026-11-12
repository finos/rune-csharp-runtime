namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Marks a generated Rune data type and carries its model identity.
/// Mirrors com.rosetta.model.lib.annotations.RuneDataType.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class RuneDataTypeAttribute : Attribute
{
    public RuneDataTypeAttribute(string name, string runeNamespace, string version)
    {
        Name = name;
        Namespace = runeNamespace;
        Version = version;
    }

    /// <summary>Rune type name, e.g. "TradeState".</summary>
    public string Name { get; }

    /// <summary>Rune model namespace, e.g. "cdm.event.common".</summary>
    public string Namespace { get; }

    /// <summary>Model version the type was generated from.</summary>
    public string Version { get; }

    /// <summary>Model short name (first namespace segment), serialized as "@model".</summary>
    public string Model => Namespace.Split('.')[0];

    /// <summary>Fully-qualified Rune type name, serialized as "@type".</summary>
    public string QualifiedName => Namespace + "." + Name;
}
