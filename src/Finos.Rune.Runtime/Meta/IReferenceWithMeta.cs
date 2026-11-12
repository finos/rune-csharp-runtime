using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime.Meta;

/// <summary>
/// Contract of reference wrappers generated from [metadata reference]/[metadata address]
/// attributes. Mirrors com.rosetta.model.lib.meta.ReferenceWithMeta.
/// </summary>
public interface IReferenceWithMeta<T> : IRosettaModelObject
{
    T? Value { get; }

    /// <summary>Global (hash-based) reference; wire name "@ref".</summary>
    string? GlobalReference { get; }

    /// <summary>External (source-document) reference; wire name "@ref:external".</summary>
    string? ExternalReference { get; }

    /// <summary>Scoped reference; unwrapped on the wire as "@ref:scoped".</summary>
    Reference? ScopedReference { get; }
}
