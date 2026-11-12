namespace Finos.Rune.Runtime;

/// <summary>
/// Non-generic contract of every model-object builder.
/// Mirrors com.rosetta.model.lib.RosettaModelObjectBuilder.
/// </summary>
public interface IRosettaModelObjectBuilder
{
    /// <summary>Builds the immutable object. Empty lists are normalized to null and child
    /// builders are built recursively.</summary>
    IRosettaModelObject Build();

    /// <summary>Recursively removes model-object children that carry no data. Scalar
    /// values are untouched.</summary>
    IRosettaModelObjectBuilder Prune();

    /// <summary>True when this builder carries any data, ignoring the metadata ("meta")
    /// attribute. Presence counts for scalar values; model-object children are checked
    /// recursively on their unwrapped value.</summary>
    bool HasData();
}
