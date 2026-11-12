using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Populates each reference wrapper's value from the object it points at elsewhere in the same document —
/// Java's <c>ReferenceResolverProcessStep</c>.
/// </summary>
/// <remarks>
/// <para>
/// A Rune document names a thing once and refers to it thereafter, so <c>@ref</c> and <c>@ref:scoped</c>
/// are how a trade's parties, quantities and observables are shared rather than duplicated. Resolution
/// turns those names back into object graph so an expression can navigate through them. It is purely an
/// in-memory affordance: <see cref="ReferencePruning"/> removes the value again on the way to the wire,
/// so a referring site still serializes as a bare reference.
/// </para>
/// <para>
/// <b>Two passes, collection complete before resolution</b>, which is what lets a reference resolve
/// against a target defined later in the document as well as earlier.
/// </para>
/// <para>
/// <b>The two lookups are scoped differently, and conflating them breaks resolution.</b> A global key is
/// document-wide: one table keyed by the key string. A scoped key is only visible within the region its
/// enclosing scope type opens, so those live in per-scope tables chosen by model path. Both are keyed by
/// string and hold candidates by type, because the match is on assignability rather than equality.
/// </para>
/// <para>
/// <b>Unresolved is a silent no-op</b> — no exception, no diagnostic, the value simply stays null. That is
/// deliberate rather than lax: CDM's own expectations contain dangling references (a
/// <c>@ref:scoped</c> naming a key nothing defines), so failing on them would reject valid documents.
/// </para>
/// </remarks>
public static class ReferenceResolverProcessStep
{
    private sealed class Candidates
    {
        internal Dictionary<string, List<(Type Type, object Target)>> ByKey { get; } =
            new(StringComparer.Ordinal);

        internal void Add(string key, Type type, object target)
        {
            if (!ByKey.TryGetValue(key, out List<(Type, object)>? list))
            {
                list = new List<(Type, object)>();
                ByKey[key] = list;
            }

            list.Add((type, target));
        }

        internal object? Resolve(string key, Type valueType)
        {
            if (!ByKey.TryGetValue(key, out List<(Type Type, object Target)>? list))
            {
                return null;
            }

            // Java's candidate order is unspecified; with several matches the last one wins.
            object? resolved = null;
            foreach ((Type type, object target) in list)
            {
                if (valueType.IsAssignableFrom(type))
                {
                    resolved = target;
                }
            }

            return resolved;
        }
    }

    /// <summary>
    /// <paramref name="instance"/> with every resolvable reference's value populated.
    /// </summary>
    public static T Resolve<T>(T instance, ReferenceConfig config)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(config);

        var global = new Candidates();
        var scoped = new ScopeBuckets<Candidates>();
        Collect(instance, config, global, scoped);
        var state = new ResolutionState();

        return (T)RuneTreeRewriter.Rewrite(
            instance,
            entry: null,
            (rebuilt, _, path) => Populate(rebuilt, path, config, global, scoped, state),
            path => !IsExcluded(path, config));
    }

    /// <summary>
    /// Targets currently being expanded, used to diagnose a real reference cycle.
    /// </summary>
    /// <remarks>
    /// Java would recurse forever on a cycle because its collected targets are pre-resolution snapshots.
    /// Reference identity is the cycle oracle here; unlike a depth or whole-document work cap, it does not
    /// reject a large but acyclic document Java accepts.
    /// </remarks>
    private sealed class ResolutionState
    {
        private readonly HashSet<IRosettaModelObject> _active =
            new(ReferenceEqualityComparer.Instance);

        internal void Enter(IRosettaModelObject target, RosettaPath path)
        {
            if (!_active.Add(target))
            {
                throw new InvalidOperationException(
                    $"Reference resolution encountered a reference cycle at {path}; the target is already "
                    + "being resolved.");
            }
        }

        internal void Exit(IRosettaModelObject target) => _active.Remove(target);
    }

    /// <summary>Pass 1: index every keyed object by its global key and by its scoped key.</summary>
    /// <remarks>
    /// Note what is <em>not</em> here. There is no <c>HasData</c> test and no attribute-metadata test —
    /// the collector takes any key-carrying node with a value, unlike the global-key tier's much fussier
    /// inclusion rule. Reusing that rule here would silently drop resolution targets.
    /// </remarks>
    private static void Collect(
        IRosettaModelObject root,
        ReferenceConfig config,
        Candidates global,
        ScopeBuckets<Candidates> scoped)
    {
        // Skip(1) drops the root, which Java never collects.
        foreach (GlobalKeyNode node in GlobalKeyWalk.Enumerate(root).Skip(1))
        {
            if (config.ScopeType != null && config.ScopeType.IsAssignableFrom(node.Instance.GetType()))
            {
                scoped.Open(node.Path);
            }

            MetaFields? meta = node.Meta;
            if (meta == null)
            {
                continue;
            }

            object? target = node.Instance;
            Type? type = node.Instance.GetType();
            if (node.Instance is IFieldWithMetaView wrapper)
            {
                target = wrapper.ValueObject;
                type = wrapper.ValueType;
            }

            if (target == null || type == null)
            {
                continue;
            }

            if (meta.GlobalKey != null)
            {
                global.Add(meta.GlobalKey, type, target);
            }

            if (meta.ScopedKey != null)
            {
                scoped.For(node.Path).Add(meta.ScopedKey, type, target);
            }
        }
    }

    /// <summary>Pass 2: give one wrapper its value, if the reference it carries resolves.</summary>
    /// <remarks>
    /// <b>The populated value is itself resolved.</b> Java does not special-case this; its walk produces
    /// it. <c>RosettaModelObjectBuilder.processRosetta</c> calls the processor on a child, and <em>then</em>
    /// calls <c>child.process(path, processor)</c> — so the wrapper is descended into after its value was
    /// set, and the generated <c>process</c> reads <c>getValue()</c> at that later point and walks the
    /// value it now holds. Skipping it leaves the resolved copy of an object structurally different from
    /// the object it was copied from, which <see cref="GlobalKeyProcessStep"/> then hashes differently and
    /// <see cref="ReKeyProcessStep"/> reports as two objects sharing one external key.
    /// </remarks>
    private static IRosettaModelObject Populate(
        IRosettaModelObject rebuilt,
        RosettaPath path,
        ReferenceConfig config,
        Candidates global,
        ScopeBuckets<Candidates> scoped,
        ResolutionState state)
    {
        if (rebuilt is not IReferenceWithMetaView view)
        {
            return rebuilt;
        }

        // Global wins, and an empty scoped holder resolves to nothing rather than falling back.
        object? resolved;
        if (view.GlobalReferenceValue != null)
        {
            resolved = global.Resolve(view.GlobalReferenceValue, view.ValueType);
        }
        else if (view.ScopedReferenceObject != null)
        {
            string? key = view.ScopedReferenceObject.ScopedReference;
            resolved = key == null ? null : scoped.For(path).Resolve(key, view.ValueType);
        }
        else
        {
            return rebuilt;
        }

        if (resolved == null)
        {
            return rebuilt;
        }

        RunePropertyTable.Entry value = RunePropertyTable.For(rebuilt.GetType()).FindByWireName(WireNames.Data)
            ?? throw new InvalidOperationException(
                $"{rebuilt.GetType().Name} is a reference wrapper with no `@data` attribute to populate.");

        if (resolved is IRosettaModelObject model)
        {
            state.Enter(model, path);
            try
            {
                resolved = RuneTreeRewriter.RewriteAt(
                    model,
                    value,
                    path.NewSubPath(value.PathName),
                    (child, _, childPath) => Populate(child, childPath, config, global, scoped, state),
                    childPath => !IsExcluded(childPath, config));
            }
            finally
            {
                state.Exit(model);
            }
        }

        IRosettaModelObjectBuilder? builder = null;
        RuneTreeRewriter.Set(ref builder, rebuilt, value, resolved);
        return (IRosettaModelObject)builder!.Build();
    }

    /// <summary>
    /// Whether resolution refuses to enter this path.
    /// </summary>
    /// <remarks>
    /// Matched on the trailing element name only — indices are ignored, and <c>*</c> is a wildcard on the
    /// pattern side. Java's gate suppresses the descent only for a single-valued attribute, because its
    /// builder-side list traversal discards the processor's answer; ours suppresses it for both. CDM's one
    /// excluded path is single-valued, so the two agree today.
    /// </remarks>
    private static bool IsExcluded(RosettaPath path, ReferenceConfig config)
    {
        foreach (RosettaPath excluded in config.ExcludedPaths)
        {
            if (path.EndsWith(excluded))
            {
                return true;
            }
        }

        return false;
    }
}
