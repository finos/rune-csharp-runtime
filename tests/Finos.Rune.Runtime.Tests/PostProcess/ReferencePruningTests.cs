using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.PostProcess;
using Xunit;

namespace Finos.Rune.Runtime.Tests.PostProcess;

/// <summary>
/// <see cref="ReferencePruning"/> — dropping a resolved value on the way to the wire.
/// </summary>
/// <remarks>
/// <para>
/// These have to be unit tests, because the rule is <b>unobservable over the CDM corpus</b>: not one
/// object in the 1,246 checked-in resource files carries a reference alongside a value, so the round-trip
/// and cross-language sweeps pass whether this logic is right or wrong. They would only start exercising
/// it once reference resolution populates values — at which point a mistake here breaks 796 files at
/// once. So the discriminating cases are pinned in advance.
/// </para>
/// <para>
/// <c>MetaFields</c> stands in for a wrapped model object: the wrappers are generic over any
/// <c>IRosettaModelObject</c> and this assembly has no model of its own. The <em>traversal</em> is not
/// re-tested here — it is <see cref="RuneTreeRewriter"/>, shared with the global-key tier, whose
/// pack-wide test already drives it over 125 real documents.
/// </para>
/// </remarks>
public class ReferencePruningTests
{
    private static MetaFields Value() => MetaFields.CreateBuilder().SetScheme("a-value").Build();

    /// <summary>
    /// Prunes the wrapper under test, nested one level below the root.
    /// </summary>
    /// <remarks>
    /// The nesting is load-bearing, not incidental. None of the four passes visits the root — Java drives
    /// each through <c>process()</c>, which walks attributes only — so a wrapper handed in <em>as</em> the
    /// root is never pruned, and a test written that way would assert nothing while appearing to pass.
    /// Wrapping it in a <c>FieldWithMeta</c> puts it where a real document would have it: behind an
    /// attribute.
    /// </remarks>
    private static ReferenceWithMeta<MetaFields> Pruned(ReferenceWithMeta<MetaFields>.Builder builder)
    {
        FieldWithMeta<ReferenceWithMeta<MetaFields>> root = ReferencePruning.PruneForSerialization(
            FieldWithMeta<ReferenceWithMeta<MetaFields>>.CreateBuilder()
                .SetValue(builder.SetValue(Value()).Build())
                .Build());

        return root.Value!;
    }

    [Fact]
    public void Pruning_WithAGlobalReference_RemovesTheValue() =>
        Assert.Null(Pruned(ReferenceWithMeta<MetaFields>.CreateBuilder().SetGlobalReference("abc123")).Value);

    /// <summary>
    /// An external reference suppresses it too — the third disjunct.
    /// </summary>
    /// <remarks>
    /// This is the case that separates the two reference implementations. The legacy Jackson filter
    /// (<c>ReferenceFilter</c>) suppresses on a global or scoped reference only; the Rune-format writer
    /// that actually produced CDM's expectations also suppresses on an external reference. Copying the
    /// legacy condition would leave a value on the wire here.
    /// </remarks>
    [Fact]
    public void Pruning_WithAnExternalReference_RemovesTheValue() =>
        Assert.Null(Pruned(ReferenceWithMeta<MetaFields>.CreateBuilder().SetExternalReference("party3")).Value);

    [Fact]
    public void Pruning_WithAScopedReference_RemovesTheValue() =>
        Assert.Null(Pruned(ReferenceWithMeta<MetaFields>.CreateBuilder()
            .SetScopedReference(Reference.CreateBuilder().SetScopedReference("quantity-1").Build())).Value);

    /// <summary>
    /// A scoped-reference holder with no reference string does <b>not</b> suppress.
    /// </summary>
    /// <remarks>
    /// Java's condition tests the reference string, not merely the holder, and this shape is reachable:
    /// one post-processing entry point prunes before the steps run and one does not, so an empty holder
    /// survives on the un-pruned path. Testing the holder instead would silently drop a defining site's
    /// value.
    /// </remarks>
    [Fact]
    public void Pruning_WithAScopedHolderCarryingNoReference_KeepsTheValue() =>
        Assert.NotNull(Pruned(ReferenceWithMeta<MetaFields>.CreateBuilder()
            .SetScopedReference(Reference.CreateBuilder().SetScope("DOCUMENT").Build())).Value);

    /// <summary>A defining site — a value and no reference — keeps its value.</summary>
    [Fact]
    public void Pruning_AtADefiningSiteWithNoReference_KeepsTheValue() =>
        Assert.NotNull(Pruned(ReferenceWithMeta<MetaFields>.CreateBuilder()).Value);

    /// <summary>
    /// Pruning does not touch the caller's object.
    /// </summary>
    /// <remarks>
    /// The whole point of doing this as a pre-serialization rewrite rather than a write-time filter is
    /// that the in-memory graph keeps the resolved value for expressions to navigate, while the wire form
    /// does not. If this rewrote in place, serializing would destroy the resolution.
    /// </remarks>
    [Fact]
    public void Pruning_OverAResolvedReference_LeavesTheCallersObjectIntact()
    {
        ReferenceWithMeta<MetaFields> original = ReferenceWithMeta<MetaFields>.CreateBuilder()
            .SetGlobalReference("abc123")
            .SetValue(Value())
            .Build();
        FieldWithMeta<ReferenceWithMeta<MetaFields>> root =
            FieldWithMeta<ReferenceWithMeta<MetaFields>>.CreateBuilder().SetValue(original).Build();

        FieldWithMeta<ReferenceWithMeta<MetaFields>> pruned = ReferencePruning.PruneForSerialization(root);

        Assert.Null(pruned.Value!.Value);
        Assert.NotNull(original.Value);
    }

    /// <summary>Nothing to prune returns the very same instance, so the common case allocates nothing.</summary>
    [Fact]
    public void Pruning_WhenThereIsNothingToPrune_ReturnsTheSameInstance()
    {
        MetaFields untouched = Value();

        Assert.Same(untouched, ReferencePruning.PruneForSerialization(untouched));
    }
}
