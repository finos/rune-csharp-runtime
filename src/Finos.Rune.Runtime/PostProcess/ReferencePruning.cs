using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Shapes a graph for the wire: drops a reference made redundant by another on the same node, drops the
/// value behind any surviving reference, and drops a global key nothing refers to — Java's
/// pre-serialization processor.
/// </summary>
/// <remarks>
/// <para>
/// Reference resolution and key assignment both populate more than the wire format carries. A referring
/// site serializes as a bare reference rather than a second copy of its target; a node names itself by
/// whichever reference kind actually points at it; and a global key nothing references is noise. This is
/// where that reduction happens, and it happens on a copy, so the in-memory graph keeps everything
/// expressions need to navigate.
/// </para>
/// <para><b>Four passes, in this order, because two of them are coupled:</b></para>
/// <list type="number">
/// <item>Collect every keyed object's keys, from the <em>unpruned</em> tree.</item>
/// <item>Null a redundant external reference, then a redundant global one, then the value behind whatever
/// reference survives.</item>
/// <item>Collect the global references that remain, from the <em>already-pruned</em> tree.</item>
/// <item>Null any global key absent from that set.</item>
/// </list>
/// <para>
/// Passes 2 and 4 are coupled through pass 3: a global reference nulled as redundant in pass 2 is invisible
/// to pass 3, so it no longer protects its target's key in pass 4. Reordering them changes the output.
/// </para>
/// <para>
/// <b>No pass visits the root.</b> Java drives each through <c>builder.process(path, …)</c>, which walks
/// attributes and never hands over the root itself. That is why every one of the 796 CDM expectation files
/// keeps a root <c>@key</c> that nothing references — and why a port that pruned the root would strip all
/// 796 of them.
/// </para>
/// </remarks>
public static class ReferencePruning
{
    /// <summary>
    /// The three lookups pass 2 consults, keyed by <c>(type, key)</c> with the type compared by
    /// <em>identity</em> — never by assignability, unlike the resolver's write site.
    /// </summary>
    private sealed class KeyLookup
    {
        internal Dictionary<(Type Type, string Key), string> AddressToExternal { get; } = new();

        internal Dictionary<(Type Type, string Key), string> AddressToGlobal { get; } = new();

        internal Dictionary<(Type Type, string Key), string> ExternalToGlobal { get; } = new();

        internal bool AddressAndExternalShareObject(Type type, string? address, string? external) =>
            Matches(AddressToExternal, type, address, external);

        internal bool AddressAndGlobalShareObject(Type type, string? address, string? global) =>
            Matches(AddressToGlobal, type, address, global);

        internal bool ExternalAndGlobalShareObject(Type type, string? external, string? global) =>
            Matches(ExternalToGlobal, type, external, global);

        private static bool Matches(
            Dictionary<(Type Type, string Key), string> map, Type type, string? lookup, string? expected) =>
            lookup != null && expected != null
            && map.TryGetValue((type, lookup), out string? found)
            && string.Equals(found, expected, StringComparison.Ordinal);
    }

    /// <summary><paramref name="instance"/> reduced to what the wire carries.</summary>
    public static T PruneForSerialization<T>(T instance)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);

        KeyLookup keys = CollectKeys(instance);
        IRosettaModelObject pruned = RuneTreeRewriter.Rewrite(
            instance, entry: null, (rebuilt, entry, _) => PruneReferences(rebuilt, entry, keys));

        HashSet<(Type, string)> referenced = CollectGlobalReferences(pruned);
        return (T)RuneTreeRewriter.Rewrite(
            pruned, entry: null, (rebuilt, entry, _) => PruneGlobalKeys(rebuilt, entry, referenced));
    }

    /// <summary>Pass 1: index each keyed object's keys against one another.</summary>
    /// <remarks>
    /// The gate is Java's <c>instanceof GlobalKey</c>, which covers generated keyed types <em>and</em>
    /// metadata wrappers but excludes reference wrappers and <c>MetaFields</c> itself. For a wrapper the
    /// type recorded is what it <em>wraps</em>, not the wrapper — so a wrapped string indexes under
    /// <c>string</c>.
    /// </remarks>
    private static KeyLookup CollectKeys(IRosettaModelObject root)
    {
        var keys = new KeyLookup();
        foreach (GlobalKeyNode node in GlobalKeyWalk.Enumerate(root).Skip(1))
        {
            if (node.Instance is IReferenceWithMetaView || TypeOf(node.Instance) is not { } type)
            {
                continue;
            }

            MetaFields? meta = node.Meta;
            if (meta == null)
            {
                continue;
            }

            if (meta.ExternalKey != null && meta.GlobalKey != null)
            {
                keys.ExternalToGlobal[(type, meta.ExternalKey)] = meta.GlobalKey;
            }

            if (meta.ScopedKey is not { } address)
            {
                continue;
            }

            if (meta.ExternalKey != null)
            {
                keys.AddressToExternal[(type, address)] = meta.ExternalKey;
            }

            if (meta.GlobalKey != null)
            {
                keys.AddressToGlobal[(type, address)] = meta.GlobalKey;
            }
        }

        return keys;
    }

    /// <summary>Pass 3: the global references still standing after pass 2.</summary>
    private static HashSet<(Type, string)> CollectGlobalReferences(IRosettaModelObject root)
    {
        var referenced = new HashSet<(Type, string)>();
        foreach (GlobalKeyNode node in GlobalKeyWalk.Enumerate(root).Skip(1))
        {
            if (node.Instance is IReferenceWithMetaView { GlobalReferenceValue: { } global } view)
            {
                referenced.Add((view.ValueType, global));
            }
        }

        return referenced;
    }

    /// <summary>Pass 2: redundant references, then the value behind whatever survives.</summary>
    private static IRosettaModelObject PruneReferences(
        IRosettaModelObject rebuilt, RunePropertyTable.Entry? entry, KeyLookup keys)
    {
        if (entry == null || rebuilt is not IReferenceWithMetaView view)
        {
            return rebuilt;
        }

        string? address = view.ScopedReferenceObject?.ScopedReference;
        string? external = view.ExternalReferenceValue;
        string? global = view.GlobalReferenceValue;

        bool externalRedundant = keys.AddressAndExternalShareObject(view.ValueType, address, external);
        // Order matters, as in Java: each check sees what the previous one dropped.
        bool globalRedundant = keys.AddressAndGlobalShareObject(view.ValueType, address, global)
            || (!externalRedundant && keys.ExternalAndGlobalShareObject(view.ValueType, external, global));

        bool hasReference = (!globalRedundant && global != null)
            || (!externalRedundant && external != null)
            || address != null;
        bool dropValue = hasReference && view.ValueObject != null;

        if (!externalRedundant && !globalRedundant && !dropValue)
        {
            return rebuilt;
        }

        RunePropertyTable table = RunePropertyTable.For(rebuilt.GetType());
        IRosettaModelObjectBuilder? builder = null;
        if (externalRedundant)
        {
            Set(ref builder, rebuilt, table, WireNames.ExternalReference, null);
        }
        if (globalRedundant)
        {
            Set(ref builder, rebuilt, table, WireNames.Reference, null);
        }
        if (dropValue)
        {
            Set(ref builder, rebuilt, table, WireNames.Data, null);
        }

        return (IRosettaModelObject)builder!.Build();
    }

    /// <summary>Pass 4: a global key nothing points at is not written.</summary>
    private static IRosettaModelObject PruneGlobalKeys(
        IRosettaModelObject rebuilt, RunePropertyTable.Entry? entry, HashSet<(Type, string)> referenced)
    {
        if (entry == null || TypeOf(rebuilt) is not { } type)
        {
            return rebuilt;
        }

        MetaFields? meta = rebuilt switch
        {
            IGlobalKey keyed => keyed.Meta,
            IFieldWithMetaView wrapper => wrapper.MetaObject,
            _ => null,
        };
        if (meta?.GlobalKey is not { } global || referenced.Contains((type, global)))
        {
            return rebuilt;
        }

        RunePropertyTable table = RunePropertyTable.For(rebuilt.GetType());
        IRosettaModelObjectBuilder? builder = null;
        Set(
            ref builder, rebuilt, table, WireNames.Meta,
            ((MetaFields.Builder)meta.ToBuilder()).SetGlobalKey(null).Build());
        return (IRosettaModelObject)builder!.Build();
    }

    /// <summary>
    /// The type a node is indexed under: what a wrapper wraps, else the node's own type. Null when the node
    /// is not a key carrier at all.
    /// </summary>
    private static Type? TypeOf(IRosettaModelObject instance) => instance switch
    {
        IFieldWithMetaView wrapper => wrapper.ValueType,
        IGlobalKey => instance.GetType(),
        _ => null,
    };

    private static void Set(
        ref IRosettaModelObjectBuilder? builder,
        IRosettaModelObject instance,
        RunePropertyTable table,
        string wireName,
        object? value)
    {
        RunePropertyTable.Entry attribute = table.FindByWireName(wireName)
            ?? throw new InvalidOperationException(
                $"{instance.GetType().Name} has no `{wireName}` attribute to clear.");
        RuneTreeRewriter.Set(ref builder, instance, attribute, value);
    }
}
