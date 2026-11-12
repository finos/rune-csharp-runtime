using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Rebuilds an immutable model graph bottom-up, applying a visitor to each node.
/// </summary>
/// <remarks>
/// <para>
/// Java's post-processing steps mutate a builder graph in place — <c>toBuilder()</c> on a builder returns
/// <c>this</c>, so a step rewrites the caller's tree. The generated C# model is immutable, so every step
/// that changes anything has to rebuild instead, and this is the one place that knows how.
/// </para>
/// <para>
/// It is shared rather than copied because the rebuild encodes two constraints that are not obvious and
/// must not drift between tiers: a setter is matched on an <em>exact</em> parameter type, so the value
/// handed to it has to be the built form; and a list setter takes
/// <c>IEnumerable&lt;TElement&gt;</c> of the built element type, so an interface-typed list will not
/// bind to it.
/// </para>
/// <para>
/// A subtree the visitor does not change is returned by reference and allocates no builder, which is
/// what keeps a whole-graph rewrite affordable on every serialize.
/// </para>
/// </remarks>
internal static class RuneTreeRewriter
{
    /// <summary>
    /// Transforms one node after its children have been rebuilt.
    /// </summary>
    /// <param name="rebuilt">The node, with any changed children already in place.</param>
    /// <param name="entry">
    /// The attribute this node hangs off, or null for the root. Carried because some rules depend on the
    /// attribute rather than the object — <c>[metadata id]</c> being the one that matters — and a root is
    /// visited with no attribute metadata at all.
    /// </param>
    /// <param name="path">Absolute path to this node, for rules that depend on where it sits.</param>
    internal delegate IRosettaModelObject Visitor(
        IRosettaModelObject rebuilt, RunePropertyTable.Entry? entry, RosettaPath path);

    /// <summary>
    /// Whether to enter a node at all. Returning false leaves the node and its whole subtree untouched —
    /// Java's <c>processRosetta</c> returning false for an excluded path.
    /// </summary>
    internal delegate bool Gate(RosettaPath path);

    /// <summary>Rewrites <paramref name="instance"/> and everything beneath it, deepest first.</summary>
    internal static IRosettaModelObject Rewrite(
        IRosettaModelObject instance, RunePropertyTable.Entry? entry, Visitor visit, Gate? gate = null) =>
        Rewrite(instance, entry, RosettaPath.ValueOf(RuneTypeName.Simple(instance.GetType())), visit, gate);

    /// <summary>
    /// Rewrites a subtree that is being spliced in at <paramref name="path"/> rather than walked into from
    /// the root — the case Java reaches when a processor sets a child and its walk then descends into what
    /// it just set, carrying the position that child occupies.
    /// </summary>
    internal static IRosettaModelObject RewriteAt(
        IRosettaModelObject instance,
        RunePropertyTable.Entry? entry,
        RosettaPath path,
        Visitor visit,
        Gate? gate = null) =>
        Rewrite(instance, entry, path, visit, gate);

    private static IRosettaModelObject Rewrite(
        IRosettaModelObject instance,
        RunePropertyTable.Entry? entry,
        RosettaPath path,
        Visitor visit,
        Gate? gate)
    {
        if (gate != null && !gate(path))
        {
            return instance;
        }

        return visit(RewriteChildren(instance, path, visit, gate), entry, path);
    }

    private static IRosettaModelObject RewriteChildren(
        IRosettaModelObject instance, RosettaPath path, Visitor visit, Gate? gate)
    {
        RunePropertyTable table = RunePropertyTable.For(instance.GetType());
        IRosettaModelObjectBuilder? builder = null;

        foreach (RunePropertyTable.Entry entry in table.DeclarationOrderEntries)
        {
            if (entry.IsMetaType || !typeof(IRosettaModelObject).IsAssignableFrom(entry.DeclaredType))
            {
                continue;
            }

            object? value = entry.Property.GetValue(instance);
            if (value == null)
            {
                continue;
            }

            RosettaPath sub = path.NewSubPath(entry.PathName);
            if (entry.ListElementType == null)
            {
                var child = (IRosettaModelObject)value;
                IRosettaModelObject rewritten = Rewrite(child, entry, sub, visit, gate);
                if (!ReferenceEquals(rewritten, child))
                {
                    Set(ref builder, instance, entry, rewritten);
                }

                continue;
            }

            var items = ((System.Collections.IEnumerable)value).Cast<IRosettaModelObject?>().ToList();
            List<IRosettaModelObject?>? replacement = null;
            int index = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }
                IRosettaModelObject rewritten = Rewrite(items[i]!, entry, sub.WithIndex(index++), visit, gate);
                if (!ReferenceEquals(rewritten, items[i]))
                {
                    replacement ??= new List<IRosettaModelObject?>(items);
                    replacement[i] = rewritten;
                }
            }

            if (replacement != null)
            {
                Set(ref builder, instance, entry, ToTypedList(replacement, entry.ListElementType!));
            }
        }

        return builder == null ? instance : (IRosettaModelObject)builder.Build();
    }

    /// <summary>Writes one attribute through the builder, materializing the builder on first write.</summary>
    internal static void Set(
        ref IRosettaModelObjectBuilder? builder,
        IRosettaModelObject instance,
        RunePropertyTable.Entry entry,
        object? value)
    {
        builder ??= instance.ToBuilder();
        if (entry.Setter == null)
        {
            throw new InvalidOperationException(
                $"No builder setter for {instance.GetType().Name}.{entry.Property.Name}, so it cannot be "
                + "rewritten.");
        }

        entry.Setter.Invoke(builder, new[] { value });
    }

    /// <summary>
    /// A <c>List&lt;TElement&gt;</c> of the built element type, which is what a list setter binds to —
    /// an <c>IRosettaModelObject</c>-typed list does not match.
    /// </summary>
    private static object ToTypedList(List<IRosettaModelObject?> items, Type elementType)
    {
        var typed = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(elementType), items.Count)!;
        foreach (IRosettaModelObject? item in items)
        {
            typed.Add(item);
        }

        return typed;
    }
}
