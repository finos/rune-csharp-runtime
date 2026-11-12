using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Turns an external reference into a global one, by looking up the object that owns the external key —
/// Java's <c>ReKeyProcessStep</c>.
/// </summary>
/// <remarks>
/// <para>
/// External keys are the names a source document uses — <c>Party1</c>, <c>EXERCISE</c>, <c>TRADEDATE</c> —
/// carried through FpML ingestion. They are not resolvable on their own, because reference resolution
/// matches on global keys and scoped keys only. This step bridges the two: for every wrapper carrying an
/// <c>externalReference</c>, it finds the object whose <c>externalKey</c> matches and copies that object's
/// computed <c>globalKey</c> into the wrapper's <c>globalReference</c>. The external reference itself is
/// left in place; the pack shows 577 sites where both survive.
/// </para>
/// <para>
/// It runs first in the pipeline, and it assigns global keys as part of its own work, because the map it
/// needs is exactly the set of keys that step produces.
/// </para>
/// <para>
/// <b>A duplicate external key is an error</b>, unlike an unresolvable one. Two distinct objects claiming
/// the same external name makes every reference to that name ambiguous, so Java throws rather than pick;
/// an external reference with no matching key, by contrast, is left alone in silence, like every other
/// unresolvable reference in this tier.
/// </para>
/// <para>
/// <b>Divergence, deliberate.</b> Java takes <c>toBuilder()</c> twice — once for its own rewrite and once
/// inside the nested key step — so on an immutable input the two copies are independent and the returned
/// tree carries the rewritten references but none of the assigned keys. That is a bug, harmless in Java
/// only because callers pass builders, where <c>toBuilder()</c> returns <c>this</c>. Reproducing it in an
/// immutable model would mean returning a tree with references pointing at keys that are not there.
/// </para>
/// </remarks>
public static class ReKeyProcessStep
{
    /// <summary>
    /// <paramref name="instance"/> with global keys assigned and every resolvable external reference
    /// promoted to a global one.
    /// </summary>
    public static T Rekey<T>(T instance)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);

        T keyed = GlobalKeyProcessStep.Assign(instance);
        Dictionary<string, string> externalToGlobal = CollectExternalKeys(keyed);
        if (externalToGlobal.Count == 0)
        {
            return keyed;
        }

        return (T)RuneTreeRewriter.Rewrite(
            keyed, entry: null, (rebuilt, _, _) => Promote(rebuilt, externalToGlobal));
    }

    /// <summary>
    /// Maps each external key to the global key of the object carrying it.
    /// </summary>
    /// <remarks>
    /// The root is skipped, consistently with the other steps in this tier: Java never passes it through the
    /// collecting processor, so a root's external key is not a reference target.
    /// </remarks>
    private static Dictionary<string, string> CollectExternalKeys(IRosettaModelObject root)
    {
        var externalToGlobal = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (GlobalKeyNode node in GlobalKeyWalk.Enumerate(root).Skip(1))
        {
            MetaFields? meta = node.Meta;
            if (meta?.ExternalKey == null || meta.GlobalKey == null)
            {
                continue;
            }

            if (externalToGlobal.TryGetValue(meta.ExternalKey, out string? existing)
                && !string.Equals(existing, meta.GlobalKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Two distinct objects share the external key '{meta.ExternalKey}' "
                    + $"({existing} and {meta.GlobalKey}), so references to it are ambiguous.");
            }

            externalToGlobal[meta.ExternalKey] = meta.GlobalKey;
        }

        return externalToGlobal;
    }

    private static IRosettaModelObject Promote(
        IRosettaModelObject rebuilt, Dictionary<string, string> externalToGlobal)
    {
        if (rebuilt is not IReferenceWithMetaView view
            || view.ExternalReferenceValue == null
            || !externalToGlobal.TryGetValue(view.ExternalReferenceValue, out string? global))
        {
            return rebuilt;
        }

        RunePropertyTable.Entry reference =
            RunePropertyTable.For(rebuilt.GetType()).FindByWireName(WireNames.Reference)
            ?? throw new InvalidOperationException(
                $"{rebuilt.GetType().Name} carries an external reference with no `@ref` attribute to write.");

        IRosettaModelObjectBuilder? builder = null;
        RuneTreeRewriter.Set(ref builder, rebuilt, reference, global);
        return (IRosettaModelObject)builder!.Build();
    }
}
