using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// One node of a <see cref="GlobalKeyWalk"/>.
/// </summary>
/// <param name="Path">Absolute path from the root, as Java's key map records it.</param>
/// <param name="Instance">The object at that path.</param>
/// <param name="IsGlobalKeyFieldAttribute">
/// Whether the attribute this object hangs off was declared <c>[metadata id]</c>. This is the only
/// thing that earns a metadata wrapper a key, so it cannot be read off the object alone. False for the
/// root, which Java visits with an empty metadata array — so a wrapper handed in as the root gets no
/// key, a case CDM asserts explicitly.
/// </param>
public readonly record struct GlobalKeyNode(
    RosettaPath Path, IRosettaModelObject Instance, bool IsGlobalKeyFieldAttribute)
{
    /// <summary>
    /// The metadata block this object carries, whether it is a generated <c>[metadata key]</c> type or
    /// a metadata wrapper — the two arrive at their <c>MetaFields</c> through different surfaces.
    /// </summary>
    public MetaFields? Meta => Instance switch
    {
        IGlobalKey globalKey => globalKey.Meta,
        IFieldWithMetaView wrapper => wrapper.MetaObject,
        _ => null,
    };

    /// <summary>
    /// Whether this object is eligible for a computed global key — Java's
    /// <c>ReKeyProcessor.isGlobalKey</c>.
    /// </summary>
    /// <remarks>
    /// Every metadata wrapper is a key carrier by type, but only an <c>[metadata id]</c> attribute
    /// actually gets one. That is the difference between
    /// <c>tradeDate: {"@key":"3f1101","@data":"2018-04-01"}</c> and a bare
    /// <c>name: {"@data":"Fund 2"}</c>. The <c>HasData</c> test ignores <c>meta</c>, so an object
    /// carrying nothing but metadata is not keyed.
    /// </remarks>
    public bool IsKeyed => GlobalKeyWalk.IsKeyed(Instance, IsGlobalKeyFieldAttribute);
}

/// <summary>
/// Enumerates every model object reachable from a root, pre-order — the traversal Java's
/// <c>GlobalKeyProcessStep</c> uses to find the objects that need keys.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a <em>different</em> traversal from <see cref="Hashing.GlobalKeyHasher"/>'s.
/// The hash walk prunes: a no-data node, a reference wrapper standing in for a scoped reference, or a
/// <c>[metadata id]</c> attribute all stop it. This walk prunes nothing, because Java drives it
/// through the <em>builder-side</em> <c>processRosetta</c>, which discards the processor's return
/// value and descends into every non-null child regardless. Sharing one walk between the two tiers
/// would get the list behaviour wrong for one of them.
/// </para>
/// <para>
/// Pre-order matters for fidelity but not for results: a parent is keyed before its children, so its
/// hash is taken over a subtree whose keys are not yet assigned. That is harmless only because
/// <c>meta</c> is excluded from hashing — which also makes the whole step idempotent, as it needs to
/// be, since CDM's own helper runs it twice over the same graph.
/// </para>
/// </remarks>
public static class GlobalKeyWalk
{
    /// <summary>
    /// Whether an object is eligible for a computed global key — Java's
    /// <c>ReKeyProcessor.isGlobalKey</c>.
    /// </summary>
    /// <remarks>
    /// Every metadata wrapper is a key carrier by type, but only one hanging off a
    /// <c>[metadata id]</c> attribute actually gets a key — which is the difference between
    /// <c>tradeDate: {"@key":"3f1101","@data":"2018-04-01"}</c> and a bare
    /// <c>name: {"@data":"Fund 2"}</c>. <c>HasData</c> ignores <c>meta</c>, so an object carrying
    /// nothing but metadata is not keyed.
    /// </remarks>
    public static bool IsKeyed(IRosettaModelObject instance, bool isGlobalKeyFieldAttribute) =>
        instance is IGlobalKey or IFieldWithMetaView
        && (instance is not IFieldWithMetaView || isGlobalKeyFieldAttribute)
        && instance.ToBuilder().HasData();

    /// <summary>Every node reachable from <paramref name="root"/>, root first.</summary>
    public static IEnumerable<GlobalKeyNode> Enumerate(IRosettaModelObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Walk(RosettaPath.ValueOf(RuneTypeName.Simple(root.GetType())), root, isGlobalKeyField: false);
    }

    private static IEnumerable<GlobalKeyNode> Walk(
        RosettaPath path, IRosettaModelObject instance, bool isGlobalKeyField)
    {
        yield return new GlobalKeyNode(path, instance, isGlobalKeyField);

        RunePropertyTable table = RunePropertyTable.For(instance.GetType());
        foreach (RunePropertyTable.Entry entry in table.DeclarationOrderEntries)
        {
            if (!typeof(IRosettaModelObject).IsAssignableFrom(entry.DeclaredType))
            {
                continue;
            }

            RosettaPath sub = path.NewSubPath(entry.PathName);
            object? value = entry.Property.GetValue(instance);
            if (entry.ListElementType == null)
            {
                if (value is IRosettaModelObject child)
                {
                    foreach (GlobalKeyNode node in Walk(sub, child, entry.IsGlobalKeyField))
                    {
                        yield return node;
                    }
                }

                continue;
            }

            if (value is not System.Collections.IEnumerable items)
            {
                continue;
            }

            int index = 0;
            foreach (object? element in items)
            {
                if (element is IRosettaModelObject child)
                {
                    foreach (GlobalKeyNode node in Walk(sub.WithIndex(index++), child, entry.IsGlobalKeyField))
                    {
                        yield return node;
                    }
                }
            }
        }
    }
}
