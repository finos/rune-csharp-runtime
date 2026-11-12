using Finos.Rune.Runtime.Hashing;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.PostProcess;
using Xunit;

namespace Finos.Rune.Runtime.Tests.PostProcess;

/// <summary>
/// <see cref="ReferenceResolverProcessStep"/>'s behaviour on a reference whose <em>target</em> carries a
/// reference of its own, and the global-key consequence of getting it wrong.
/// </summary>
/// <remarks>
/// <para>
/// Java resolves inside a value it has just populated, and it is the walk that makes it do so, not any
/// explicit recursion. <c>RosettaModelObjectBuilder.processRosetta</c> is
/// <c>processFurther = processor.processRosetta(path, clazz, child, this, metas); if (child != null &amp;&amp;
/// processFurther) child.process(path, processor);</c> — so the resolver first sets
/// <c>referenceWithMeta.value</c> and then descends into the wrapper, whose generated <c>process</c> reads
/// <c>getValue()</c> <em>afterwards</em> and walks into the value it now has.
/// </para>
/// <para>
/// The consequence is not cosmetic. A resolved value and the object it was copied from are the same object
/// in every respect that matters to hashing, so <c>GlobalKeyProcessStep</c> must give them the same key —
/// and <c>ReKeyProcessStep</c> <em>throws</em> when two objects sharing an external key disagree on their
/// global key. That is exactly what CDM's compression, novation, allocation, clearing and exercise
/// function samples hit: one <c>party1</c> object, reachable both directly and as the value of a resolved
/// <c>partyReference</c>, with a nested <c>personReference</c> that only one of the two copies had
/// resolved.
/// </para>
/// </remarks>
public class NestedReferenceResolutionTests
{
    private static readonly ReferenceConfig Config = ReferenceConfig.NoScopeOrExcludedPaths;

    /// <summary>
    /// <c>referrer</c> points at <c>target</c>, which points at <c>leaf</c>.
    /// </summary>
    private static RefNode Graph(string? targetExternalKey = null)
    {
        MetaFields.Builder targetMeta = MetaFields.CreateBuilder().SetGlobalKey("target-key");
        if (targetExternalKey != null)
        {
            targetMeta.SetExternalKey(targetExternalKey);
        }

        RefNode leaf = RefNode.CreateBuilder()
            .SetName("leaf")
            .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("leaf-key"))
            .Build();
        RefNode target = RefNode.CreateBuilder()
            .SetName("target")
            .SetMeta(targetMeta)
            .SetLink(ReferenceWithMeta<RefNode>.CreateBuilder().SetGlobalReference("leaf-key"))
            .Build();
        RefNode referrer = RefNode.CreateBuilder()
            .SetName("referrer")
            .SetLink(ReferenceWithMeta<RefNode>.CreateBuilder().SetGlobalReference("target-key"))
            .Build();

        return RefNode.CreateBuilder()
            .SetName("root")
            .SetNested(new[] { leaf, target, referrer })
            .Build();
    }

    private static RefNode NestedAt(RefNode root, string name) =>
        root.Nested!.Single(n => n.Name == name);

    [Fact]
    public void Resolve_WhenAResolvedValueItselfCarriesAReference_ResolvesThatReferenceToo()
    {
        RefNode resolved = ReferenceResolverProcessStep.Resolve(Graph(), Config);

        RefNode? referrerValue = NestedAt(resolved, "referrer").Link!.Value;
        Assert.NotNull(referrerValue);
        Assert.Equal("target", referrerValue!.Name);

        Assert.NotNull(referrerValue.Link!.Value);
        Assert.Equal("leaf", referrerValue.Link!.Value!.Name);
    }

    [Fact]
    public void Resolve_ForAnObjectReachableBothDirectlyAndThroughAReference_LeavesTheTwoCopiesEqual()
    {
        RefNode resolved = ReferenceResolverProcessStep.Resolve(Graph(), Config);

        RefNode direct = NestedAt(resolved, "target");
        RefNode throughReference = NestedAt(resolved, "referrer").Link!.Value!;

        Assert.Equal(direct, throughReference);
        Assert.Equal(GlobalKeyHasher.Hash(direct), GlobalKeyHasher.Hash(throughReference));
    }

    [Fact]
    public void Rekey_AfterResolvingAnExternallyKeyedObjectReachedTwoWays_DoesNotReportAmbiguity()
    {
        RefNode resolved = ReferenceResolverProcessStep.Resolve(Graph(targetExternalKey: "target-ext"), Config);

        RefNode rekeyed = ReKeyProcessStep.Rekey(resolved);

        Assert.Equal(
            NestedAt(rekeyed, "target").Meta!.GlobalKey,
            NestedAt(rekeyed, "referrer").Link!.Value!.Meta!.GlobalKey);
    }

    /// <summary>
    /// A reference to a <em>basic</em> value — Java's <c>BasicReferenceWithMetaX</c> — resolves against
    /// the keyed <c>FieldWithMeta</c> that holds it.
    /// </summary>
    /// <remarks>
    /// <c>ReferenceCollector.processRosetta</c> indexes <c>getValue()</c> under <c>getValueType()</c>, and
    /// for a <c>FieldWithMeta</c> those are the <em>wrapped</em> value and its type — a <c>String</c>, a
    /// <c>Date</c> — not the wrapper. <c>ReferenceResolver.doTest</c> then matches
    /// <c>BasicReferenceWithMetaDate.getValueType() == Date.class</c> against it. Indexing the wrapper
    /// instead leaves every basic reference in CDM unresolved: <c>dateRelativeTo</c> pointing at a keyed
    /// <c>tradeDate</c> is the shape, and it is common enough that the un-populated value changes the
    /// global key of every ancestor.
    /// </remarks>
    [Fact]
    public void Resolve_ForAReferenceToAKeyedBasicValue_PopulatesItFromTheFieldWithMeta()
    {
        RefNode holder = RefNode.CreateBuilder()
            .SetName("holder")
            .SetLabel(FieldWithMeta<string>.CreateBuilder()
                .SetValue("hello")
                .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("label-key")))
            .Build();
        RefNode referrer = RefNode.CreateBuilder()
            .SetName("referrer")
            .SetLabelRef(ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("label-key"))
            .Build();
        RefNode root = RefNode.CreateBuilder()
            .SetName("root")
            .SetNested(new[] { holder, referrer })
            .Build();

        RefNode resolved = ReferenceResolverProcessStep.Resolve(root, Config);

        Assert.Equal("hello", NestedAt(resolved, "referrer").LabelRef!.Value);
    }

    [Fact]
    public void Resolve_ForAReferenceToAnAbsentBasicValue_LeavesItUnresolved()
    {
        RefNode holder = RefNode.CreateBuilder()
            .SetName("holder")
            .SetLabel(FieldWithMeta<string>.CreateBuilder()
                .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("label-key")))
            .Build();
        RefNode referrer = RefNode.CreateBuilder()
            .SetName("referrer")
            .SetLabelRef(ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("label-key"))
            .Build();
        RefNode root = RefNode.CreateBuilder()
            .SetName("root")
            .SetNested(new[] { holder, referrer })
            .Build();

        RefNode resolved = ReferenceResolverProcessStep.Resolve(root, Config);

        Assert.Null(NestedAt(resolved, "referrer").LabelRef!.Value);
    }

    [Fact]
    public void Resolve_ForABranchingReferenceCycle_ThrowsADiagnosticBeforeExpansionRunsAway()
    {
        static RefNode Referrer(string name, string key) => RefNode.CreateBuilder()
            .SetName(name)
            .SetLink(ReferenceWithMeta<RefNode>.CreateBuilder().SetGlobalReference(key))
            .Build();

        RefNode a = RefNode.CreateBuilder()
            .SetName("a")
            .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("a-key"))
            .SetNested([Referrer("a-to-b-1", "b-key"), Referrer("a-to-b-2", "b-key")])
            .Build();
        RefNode b = RefNode.CreateBuilder()
            .SetName("b")
            .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("b-key"))
            .SetNested([Referrer("b-to-a-1", "a-key"), Referrer("b-to-a-2", "a-key")])
            .Build();
        RefNode root = RefNode.CreateBuilder().SetName("root").SetNested([a, b]).Build();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => ReferenceResolverProcessStep.Resolve(root, Config));

        Assert.Contains("reference cycle", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }
}
