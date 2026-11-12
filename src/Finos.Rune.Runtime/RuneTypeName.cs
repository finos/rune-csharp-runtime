namespace Finos.Rune.Runtime;

/// <summary>
/// The name Java's <c>getClass().getSimpleName()</c> would produce for a model type, which is what a
/// <see cref="Path.RosettaPath"/> root is built from.
/// </summary>
internal static class RuneTypeName
{
    /// <summary>
    /// Generated C# type names equal Rune type names, so this is the Rune name — except for the
    /// runtime's generic wrappers, whose CLR name carries an arity suffix
    /// (<c>FieldWithMeta`1</c>) that has to come off.
    /// </summary>
    /// <remarks>
    /// Java has no such suffix to strip because it generates a concrete class per wrapped type
    /// (<c>FieldWithMetaDate</c>), so the two names differ for wrappers. That is harmless where the
    /// path is only a label, and both tiers that build one are in that position: validation never
    /// reports against a wrapper root, and the global-key hash reads the path for a single
    /// <c>externalReference</c> test that no root participates in. What is <em>not</em> harmless is
    /// leaving the backtick in — <c>RosettaPath.ValueOf</c> rejects it, and every
    /// <c>[metadata id]</c> wrapper is a hash root.
    /// </remarks>
    internal static string Simple(Type type)
    {
        string name = type.Name;
        int arity = name.IndexOf('`', StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }
}
