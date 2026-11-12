using Finos.Rune.Runtime.Qualification;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Runs a freshly built object through the whole post-processing pipeline in the one order the reference
/// always uses — the composition of Java's <c>FunctionRunner.postProcess</c> (which leads with a structural
/// <c>prune()</c>) and <c>WorkflowPostProcessor</c> (which then runs its four steps in a hard-coded order:
/// <c>ReKeyProcessStep</c>, <c>UpdateTemporaryKeyProcessStep</c>, <c>ReferenceResolverProcessStep</c>,
/// <c>QualifyProcessorStep</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is not a preference, it is a dependency chain, and it is not sorted at runtime.</b> Four of
/// Java's five steps report the same priority, so nothing about their declared priorities could reconstruct
/// this order — <c>PostProcessorRunner</c>'s sort is stable and simply preserves the caller's list, which is
/// this exact sequence, hard-coded. Each step needs what the one before it produced:
/// </para>
/// <list type="number">
/// <item>
/// <b>Prune first.</b> A function builds its result through ordinary builders, which leave a
/// data-less child object in place rather than dropping it — only <c>Prune()</c> removes one. Downstream,
/// <see cref="ReferenceResolverProcessStep"/>'s walk and the wire writer's own list handling both change
/// behaviour on a list whose elements do or do not carry data, so running the four steps over an unpruned
/// tree does not merely leave harmless clutter behind.
/// </item>
/// <item>
/// <b><see cref="ReKeyProcessStep"/> assigns global keys as part of its own work</b> (Java's
/// <c>GlobalKeyProcessStep</c>), and then promotes every resolvable external reference to a global one. A
/// freshly built graph carries no keys at all until this runs — nothing for a global reference to resolve
/// against exists before it.
/// </item>
/// <item>
/// <b><see cref="UpdateTemporaryKeyProcessStep"/> renumbers before resolution</b> matches on scoped-key
/// names, so that two runs over the same ingest produce the same stable names.
/// </item>
/// <item>
/// <b><see cref="ReferenceResolverProcessStep"/> populates every resolvable reference's value</b>, which
/// depends on step 2's global keys and is itself a precondition of step 5: a qualify function can navigate
/// through a reference pointing outside the subtree it is qualifying, and an unresolved one reads as
/// nothing there.
/// </item>
/// <item>
/// <b><see cref="QualifyProcessorStep"/> runs last</b>, over the fully resolved graph, and writes each
/// unique qualification back.
/// </item>
/// </list>
/// <para>
/// <b>Model-agnostic by construction.</b> Everything this method needs beyond the runtime's own types —
/// which type opens a reference scope, which paths resolution never enters, which node types carry a
/// qualification and how — is supplied by the caller as a <see cref="ReferenceConfig"/> and a list of
/// <see cref="IQualificationHandler"/>. CDM's own values for both live beside the generated model (see
/// <c>Cdm.Reference.CdmReferenceConfig</c> and <c>Cdm.Qualification.CdmQualification</c>), exactly as Java
/// splits the runtime-side <c>ReferenceConfig</c>/<c>WorkflowPostProcessor</c> from the CDM-side
/// <c>CdmReferenceConfig</c>/<c>CdmQualificationHandlerProvider</c>.
/// </para>
/// </remarks>
public static class WorkflowPostProcessor
{
    /// <summary>
    /// Runs <paramref name="instance"/> through prune, re-key, temporary-key renumbering, reference
    /// resolution and qualification, in that order, and reports what qualification decided.
    /// </summary>
    public static WorkflowPostProcessResult<T> Run<T>(
        T instance, ReferenceConfig referenceConfig, IReadOnlyList<IQualificationHandler> qualificationHandlers)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(referenceConfig);
        ArgumentNullException.ThrowIfNull(qualificationHandlers);

        T pruned = Prune(instance);
        T rekeyed = ReKeyProcessStep.Rekey(pruned);
        T renumbered = UpdateTemporaryKeyProcessStep.Renumber(rekeyed, referenceConfig);
        T resolved = ReferenceResolverProcessStep.Resolve(renumbered, referenceConfig);
        QualificationReport report = QualifyProcessorStep.Run(resolved, qualificationHandlers);

        return new WorkflowPostProcessResult<T>((T)report.Object, report.Results);
    }

    /// <summary>
    /// The structural prune <c>FunctionRunner</c> applies before any process step runs — builder-level, not
    /// <see cref="ReferencePruning"/>'s wire-focused reduction, which stays a separate, later, serialization
    /// concern.
    /// </summary>
    private static T Prune<T>(T instance)
        where T : class, IRosettaModelObject
    {
        IRosettaModelObjectBuilder builder = instance.ToBuilder().Prune();
        return (T)builder.Build();
    }
}

/// <summary>The outcome of running <see cref="WorkflowPostProcessor.Run{T}"/> over one object.</summary>
/// <param name="Object">The graph after all four steps, with each unique qualification written into it.</param>
/// <param name="QualificationResults">One entry per qualifiable object found, whether or not it qualified.</param>
public sealed record WorkflowPostProcessResult<T>(
    T Object, IReadOnlyList<QualificationResult> QualificationResults)
    where T : class, IRosettaModelObject
{
    /// <summary>How many qualifiable objects were found, qualified or not.</summary>
    public int QualifiableObjectsCount => QualificationResults.Count;

    /// <summary>How many resolved to exactly one qualification.</summary>
    public int UniquelyQualifiedObjectsCount => QualificationResults.Count(r => r.IsSuccess);
}
