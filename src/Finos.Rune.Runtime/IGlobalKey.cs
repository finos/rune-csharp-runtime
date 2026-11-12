using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime;

/// <summary>
/// Implemented by generated types declaring [metadata key]: they carry a MetaFields
/// attribute holding their global/external key. Mirrors com.rosetta.model.lib.GlobalKey.
/// </summary>
public interface IGlobalKey
{
    MetaFields? Meta { get; }
}
