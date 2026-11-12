using Finos.Rune.Runtime.Hashing;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Assigns a computed global key to every keyed object in a graph — Java's
/// <c>GlobalKeyProcessStep(NonNullHashCollector::new)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of the global-key tier that model consumers actually invoke: a function that builds
/// a <c>TradeState</c> produces one with no keys, and the keys have to be minted before it is serialized
/// or compared against a reference output.
/// </para>
/// <para>
/// <b>It returns a new object rather than mutating.</b> Java walks a builder graph and mutates in place
/// — <c>instance.toBuilder()</c> on a builder returns <c>this</c>, so its process step rewrites the
/// caller's graph. The generated C# model is immutable, so this rebuilds instead, reusing every subtree
/// that did not change (an object with no keyed descendants is returned by reference).
/// </para>
/// <para>
/// <b>It works bottom-up where Java works top-down</b>, and the two agree. Java keys a parent before its
/// children, so a parent's hash is taken over a subtree whose keys are not yet assigned; this keys
/// children first, so a parent's hash is taken over a subtree whose keys <em>are</em> assigned. Those
/// give the same numbers for one reason only: <c>meta</c> is excluded from hashing entirely, so a key
/// written into a descendant is invisible to every ancestor's hash. That same property is what makes
/// the step idempotent — necessary, since CDM's own helper runs it twice over one graph — and bottom-up
/// is the order an immutable rebuild wants, since a parent cannot be built until its children are final.
/// </para>
/// </remarks>
public static class GlobalKeyProcessStep
{
    /// <summary>
    /// <paramref name="instance"/> with a global key on every keyed object, itself included.
    /// </summary>
    /// <remarks>
    /// Any key already present is <b>overwritten</b>, matching Java: there is no fill-if-missing branch
    /// and no way to disable it, so a key deserialized from input is replaced by a recomputed one.
    /// </remarks>
    public static T Assign<T>(T instance)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);
        return (T)RuneTreeRewriter.Rewrite(instance, entry: null, Key);
    }

    private static IRosettaModelObject Key(
        IRosettaModelObject rebuilt, RunePropertyTable.Entry? entry, Path.RosettaPath path) =>
        GlobalKeyWalk.IsKeyed(rebuilt, entry?.IsGlobalKeyField ?? false)
            ? WithGlobalKey(rebuilt, GlobalKeyHasher.Hash(rebuilt))
            : rebuilt;

    /// <summary>Writes the key to <c>meta -&gt; globalKey</c>, preserving any other metadata.</summary>
    private static IRosettaModelObject WithGlobalKey(IRosettaModelObject instance, string key)
    {
        RunePropertyTable table = RunePropertyTable.For(instance.GetType());
        RunePropertyTable.Entry meta = table.FindByWireName(WireNames.Meta)
            ?? throw new InvalidOperationException(
                $"{instance.GetType().Name} is keyed but has no `meta` attribute to write to.");

        MetaFields? existing = instance switch
        {
            IGlobalKey globalKey => globalKey.Meta,
            IFieldWithMetaView wrapper => wrapper.MetaObject,
            _ => null,
        };
        MetaFields.Builder metaBuilder = existing == null
            ? MetaFields.CreateBuilder()
            : (MetaFields.Builder)existing.ToBuilder();

        IRosettaModelObjectBuilder? builder = null;
        RuneTreeRewriter.Set(ref builder, instance, meta, metaBuilder.SetGlobalKey(key).Build());
        return (IRosettaModelObject)builder!.Build();
    }
}
