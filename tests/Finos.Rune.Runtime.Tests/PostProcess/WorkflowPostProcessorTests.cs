using Finos.Rune.Runtime.Hashing;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.PostProcess;
using Finos.Rune.Runtime.Qualification;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Xunit;

namespace Finos.Rune.Runtime.Tests.PostProcess;

/// <summary>
/// <see cref="WorkflowPostProcessor"/> — the model-agnostic composition of prune, re-key, temporary-key
/// renumbering, reference resolution and qualification, run in that exact hard-coded order.
/// </summary>
/// <remarks>
/// <para>
/// Built on <c>TestPojo</c>/<c>TestChild</c> (the same miniature the serialization tier tests against)
/// rather than a stub for every step, because what is under test here is not any one step's own logic —
/// each already has its own focused tests — but whether <em>this method calls them in the order it must</em>.
/// That needs a real, keyed, referencing pair of nodes: <c>TestChild</c> implements <see cref="IGlobalKey"/>
/// directly, so a fresh instance can be minted a key by step 2 and then found by step 4's reference
/// resolution, exactly as a CDM function's freshly built result would be.
/// </para>
/// <para>
/// Every test here proves a dependency, not a preference: for each one, the "wrong" order is executed
/// directly, alongside the correct one, and shown to produce a different, worse answer. That is what makes
/// the order load-bearing rather than incidental.
/// </para>
/// </remarks>
public class WorkflowPostProcessorTests
{
    /// <summary>Qualifies the carrier itself, succeeding only once its reference has been resolved.</summary>
    /// <remarks>
    /// This is what makes the Resolve-before-Qualify dependency observable without a real CDM qualify
    /// function: the stub's own verdict is defined in terms of whether resolution has already run.
    /// </remarks>
    private sealed class QualifiesOnlyWhenReferenceResolvedHandler : IQualificationHandler
    {
        public Type CarrierType => typeof(TestPojo);

        public IRosettaModelObject? GetQualifiableObject(IRosettaModelObject carrier) => carrier;

        public IReadOnlyList<QualifyResult> Qualify(IRosettaModelObject qualifiable) =>
            new[]
            {
                new QualifyResult(
                    "ReferenceIsResolved", ((TestPojo)qualifiable).RefAttr?.Value != null),
            };

        public IRosettaModelObject WithQualifier(IRosettaModelObject carrier, string qualifier) =>
            ((TestPojo)carrier).ToBuilder().SetStringAttr(qualifier).Build();
    }

    /// <summary>
    /// A fresh <c>TestPojo</c> whose <c>child</c> carries no key yet (as a function's output never does)
    /// and whose <c>refAttr</c> already names that child's <em>eventual</em> global key — computed the same
    /// way <see cref="GlobalKeyProcessStep"/> will, so the fixture does not have to guess a hash.
    /// </summary>
    private static TestPojo FreshPojoWithAGlobalReferenceToItsOwnChild(out string expectedGlobalKey)
    {
        TestChild child = TestChild.CreateBuilder().SetName("Fund A").Build();
        expectedGlobalKey = GlobalKeyHasher.Hash(child);

        return TestPojo.CreateBuilder()
            .SetChild(child)
            .SetRefAttr(ReferenceWithMeta<TestChild>.CreateBuilder().SetGlobalReference(expectedGlobalKey).Build())
            .Build();
    }

    /// <summary>
    /// A child object with no data — the shape an ordinary <c>Build()</c> leaves behind — is gone from the
    /// correct pipeline's output.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_ForAFreshlyBuiltGraphWithADataLessChild_PrunesItBeforeAnyStepRuns()
    {
        TestPojo fresh = TestPojo.CreateBuilder()
            .SetStringAttr("keep-me")
            .SetChild(TestChild.CreateBuilder().Build())
            .Build();

        Assert.NotNull(fresh.Child);

        WorkflowPostProcessResult<TestPojo> result = WorkflowPostProcessor.Run(
            fresh, ReferenceConfig.NoScopeOrExcludedPaths, Array.Empty<IQualificationHandler>());

        Assert.Null(result.Object.Child);
        Assert.Equal("keep-me", result.Object.StringAttr);
    }

    /// <summary>
    /// The canary: running the same four steps with no leading prune leaves the data-less child in place.
    /// Omitting the first step changes the result.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_ComparedToSkippingTheLeadingPrune_RemovesAChildTheSkippedPipelineKeeps()
    {
        TestPojo fresh = TestPojo.CreateBuilder().SetChild(TestChild.CreateBuilder().Build()).Build();

        WorkflowPostProcessResult<TestPojo> withLeadingPrune = WorkflowPostProcessor.Run(
            fresh, ReferenceConfig.NoScopeOrExcludedPaths, Array.Empty<IQualificationHandler>());
        Assert.Null(withLeadingPrune.Object.Child);

        TestPojo rekeyed = ReKeyProcessStep.Rekey(fresh);
        TestPojo renumbered = UpdateTemporaryKeyProcessStep.Renumber(rekeyed, ReferenceConfig.NoScopeOrExcludedPaths);
        TestPojo resolved =
            ReferenceResolverProcessStep.Resolve(renumbered, ReferenceConfig.NoScopeOrExcludedPaths);
        QualificationReport withoutLeadingPrune =
            QualifyProcessorStep.Run(resolved, Array.Empty<IQualificationHandler>());

        Assert.NotNull(((TestPojo)withoutLeadingPrune.Object).Child);
    }

    /// <summary>
    /// A freshly built graph carries no keys at all: re-keying has to mint one before reference resolution
    /// can find anything to resolve a global reference against.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_ForAFreshlyBuiltGlobalReference_AssignsTheKeyThenResolvesAgainstIt()
    {
        TestPojo pojo = FreshPojoWithAGlobalReferenceToItsOwnChild(out string expectedGlobalKey);

        WorkflowPostProcessResult<TestPojo> result = WorkflowPostProcessor.Run(
            pojo, ReferenceConfig.NoScopeOrExcludedPaths, Array.Empty<IQualificationHandler>());

        Assert.NotNull(result.Object.RefAttr!.Value);
        Assert.Equal("Fund A", result.Object.RefAttr.Value!.Name);
        Assert.Equal(expectedGlobalKey, result.Object.Child!.Meta!.GlobalKey);
    }

    /// <summary>
    /// The canary: resolving before re-keying sees no global key yet, because none has been minted —
    /// swapping the two steps leaves the reference unresolved where the correct order resolves it.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_WhenResolutionRunsBeforeReKeying_LeavesTheGlobalReferenceUnresolved()
    {
        TestPojo pojo = FreshPojoWithAGlobalReferenceToItsOwnChild(out _);

        TestPojo resolvedFirst = ReferenceResolverProcessStep.Resolve(pojo, ReferenceConfig.NoScopeOrExcludedPaths);
        TestPojo rekeyedAfter = ReKeyProcessStep.Rekey(resolvedFirst);
        Assert.Null(rekeyedAfter.RefAttr!.Value);

        WorkflowPostProcessResult<TestPojo> correctOrder = WorkflowPostProcessor.Run(
            pojo, ReferenceConfig.NoScopeOrExcludedPaths, Array.Empty<IQualificationHandler>());
        Assert.NotNull(correctOrder.Object.RefAttr!.Value);
    }

    /// <summary>
    /// A qualify candidate that itself depends on resolution having already run succeeds under the correct
    /// order and fails under the swapped one — the same dependency <c>QualifyProcessorStep</c>'s own docs
    /// describe for CDM's bond-option underlier, made observable without a CDM fixture.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_ForAQualifyCandidateThatNeedsTheResolvedReference_QualifiesOnlyInTheCorrectOrder()
    {
        TestPojo pojo = FreshPojoWithAGlobalReferenceToItsOwnChild(out _);
        IReadOnlyList<IQualificationHandler> handlers =
            new IQualificationHandler[] { new QualifiesOnlyWhenReferenceResolvedHandler() };

        WorkflowPostProcessResult<TestPojo> correctOrder =
            WorkflowPostProcessor.Run(pojo, ReferenceConfig.NoScopeOrExcludedPaths, handlers);
        Assert.Equal(1, correctOrder.UniquelyQualifiedObjectsCount);
        Assert.Equal("ReferenceIsResolved", correctOrder.Object.StringAttr);

        QualificationReport swappedOrder = QualifyProcessorStep.Run(pojo, handlers);
        Assert.Equal(0, swappedOrder.UniquelyQualifiedObjectsCount);
    }

    /// <summary>
    /// A fresh <c>TestPojo</c> carrying a temporary scoped key — <c>quantity-$7</c>, the shape FpML
    /// ingestion mints, matching <see cref="UpdateTemporaryKeyProcessStep"/>'s own
    /// <c>^[a-zA-Z]*-\$[0-9]*$</c> pattern — on a <c>[metadata location]</c>-shaped wrapper
    /// (<see cref="TestPojo.LocationAttr"/>, a <see cref="FieldWithMeta{T}"/>, exactly like CDM's
    /// <c>PriceQuantity.quantity</c>), and a <c>[metadata address]</c>-shaped
    /// <see cref="ReferenceWithMeta{T}"/> elsewhere (<see cref="TestPojo.RefAttr"/>) already naming that
    /// same temporary value — exactly the pairing CDM's own ingest functions produce, and the one
    /// <see cref="UpdateTemporaryKeyProcessStep"/>'s own doc describes: "a temporary value that appears
    /// only as a reference still claims a number."
    /// </summary>
    /// <remarks>
    /// Deliberately built from a wrapper type, not <see cref="TestPojo.Child"/>'s bare
    /// <see cref="IGlobalKey"/>: <see cref="UpdateTemporaryKeyProcessStep"/> only inspects metadata
    /// wrappers (<see cref="IFieldWithMetaView"/>/<see cref="IReferenceWithMetaView"/>), by design — a
    /// temporary value sitting on a plain <c>[metadata key]</c> type is invisible to it, matching Java. A
    /// fixture built the other way would silently exercise nothing.
    /// </remarks>
    private static TestPojo FreshPojoWithATemporaryScopedKeyAndMatchingReference()
    {
        FieldWithMeta<TestChild> location = FieldWithMeta<TestChild>.CreateBuilder()
            .SetValue(TestChild.CreateBuilder().SetName("Fund A").Build())
            .SetMeta(MetaFields.CreateBuilder().SetScopedKey("quantity-$7").Build())
            .Build();

        ReferenceWithMeta<TestChild> reference = ReferenceWithMeta<TestChild>.CreateBuilder()
            .SetScopedReference(Reference.CreateBuilder().SetScopedReference("quantity-$7").Build())
            .Build();

        return TestPojo.CreateBuilder().SetLocationAttr(location).SetRefAttr(reference).Build();
    }

    /// <summary>
    /// The correct order renumbers the temporary key and its matching reference to the same stable name,
    /// and resolves the reference against it.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_ForATemporaryScopedKeyAndItsMatchingReference_RenumbersBothToTheSameStableNameAndResolves()
    {
        TestPojo pojo = FreshPojoWithATemporaryScopedKeyAndMatchingReference();

        WorkflowPostProcessResult<TestPojo> result = WorkflowPostProcessor.Run(
            pojo, ReferenceConfig.NoScopeOrExcludedPaths, Array.Empty<IQualificationHandler>());

        Assert.Equal("quantity-1", result.Object.LocationAttr!.Meta!.ScopedKey);
        Assert.Equal("quantity-1", result.Object.RefAttr!.ScopedReference!.ScopedReference);
        Assert.Equal("Fund A", result.Object.RefAttr.Value?.Name);
    }

    /// <summary>
    /// The canary for deleting <see cref="UpdateTemporaryKeyProcessStep"/> out of the pipeline: resolution
    /// alone, with no renumbering ever run, still resolves the reference — both sides carry the identical
    /// raw temporary string, and <see cref="ReferenceResolverProcessStep"/> matches on whatever string is
    /// currently present, temporary or not. What it does <em>not</em> do is produce the stable, canonical
    /// name every <see cref="WorkflowPostProcessor"/> output must carry: the key and its reference are left
    /// in their raw <c>quantity-$7</c> form, the exact shape <see cref="UpdateTemporaryKeyProcessStep"/>
    /// exists to remove. That is the concrete, observable difference deleting or skipping the step leaves
    /// behind, and what this canary catches — a temporary-pattern leak that the pack's own expectations
    /// (verified byte-for-byte in <c>TemporaryKeyTests</c>) never contain.
    /// </summary>
    [Fact]
    public void WorkflowPostProcessor_ComparedToSkippingTemporaryKeyRenumbering_LeavesTheRawTemporaryNameInPlaceOfAStableOne()
    {
        TestPojo pojo = FreshPojoWithATemporaryScopedKeyAndMatchingReference();

        TestPojo resolvedOnly = ReferenceResolverProcessStep.Resolve(pojo, ReferenceConfig.NoScopeOrExcludedPaths);
        Assert.Matches("^[a-zA-Z]*-\\$[0-9]*$", resolvedOnly.LocationAttr!.Meta!.ScopedKey!);
        Assert.Matches("^[a-zA-Z]*-\\$[0-9]*$", resolvedOnly.RefAttr!.ScopedReference!.ScopedReference!);
        Assert.Equal("Fund A", resolvedOnly.RefAttr.Value?.Name);

        WorkflowPostProcessResult<TestPojo> correctOrder = WorkflowPostProcessor.Run(
            pojo, ReferenceConfig.NoScopeOrExcludedPaths, Array.Empty<IQualificationHandler>());
        Assert.Equal("quantity-1", correctOrder.Object.LocationAttr!.Meta!.ScopedKey);
        Assert.Equal("quantity-1", correctOrder.Object.RefAttr!.ScopedReference!.ScopedReference);
    }
}
