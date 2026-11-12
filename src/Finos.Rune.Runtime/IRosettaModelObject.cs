namespace Finos.Rune.Runtime;

/// <summary>
/// Non-generic contract of every Rune model value (generated data types, choice types,
/// meta wrappers, and the runtime's own MetaFields/Key/Reference).
/// Mirrors com.rosetta.model.lib.RosettaModelObject.
/// </summary>
public interface IRosettaModelObject
{
    /// <summary>Returns a mutable builder pre-populated with this object's data.</summary>
    IRosettaModelObjectBuilder ToBuilder();
}
