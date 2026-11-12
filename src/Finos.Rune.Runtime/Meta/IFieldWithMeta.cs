using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime.Meta;

/// <summary>
/// Contract of value-with-metadata wrappers (generated per value type, plus the
/// hand-written basic-value wrappers in Finos.Rune.Runtime.Metafields).
/// Mirrors com.rosetta.model.lib.meta.FieldWithMeta.
/// </summary>
public interface IFieldWithMeta<T> : IRosettaModelObject
{
    T? Value { get; }

    MetaFields? Meta { get; }
}
