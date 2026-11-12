using Finos.Rune.Runtime.PostProcess;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Qualification;

/// <summary>
/// Knows how to qualify one kind of node: where its qualifiable object lives, which candidates apply, and
/// where the answer is written — Java's <c>QualificationHandler</c>.
/// </summary>
/// <remarks>
/// The three roles are deliberately separate, because in CDM they are three different objects. The node the
/// walk trips on is a <c>NonTransferableProduct</c>; the object the functions consume is its
/// <c>EconomicTerms</c>; and the answer is written back onto the product as a taxonomy. Collapsing them
/// would work for business events, where all three are the same object, and fail for products.
/// </remarks>
public interface IQualificationHandler
{
    /// <summary>
    /// The node type this handler claims, matched by <b>exact type identity</b> rather than assignability.
    /// </summary>
    /// <remarks>
    /// Java looks the type up in a map keyed by <c>Class</c>, so a subtype of a registered carrier is not
    /// claimed. Widening this to an assignability test would qualify nodes the reference leaves alone.
    /// </remarks>
    Type CarrierType { get; }

    /// <summary>The object the candidates run against, or null when this node has nothing to qualify.</summary>
    IRosettaModelObject? GetQualifiableObject(IRosettaModelObject carrier);

    /// <summary>Every candidate's verdict, in registry order.</summary>
    IReadOnlyList<QualifyResult> Qualify(IRosettaModelObject qualifiable);

    /// <summary>The carrier with the qualification written into it.</summary>
    IRosettaModelObject WithQualifier(IRosettaModelObject carrier, string qualifier);
}

/// <summary>
/// Qualifies every qualifiable object in a graph and writes each unique answer back — Java's
/// <c>QualifyProcessorStep</c>.
/// </summary>
/// <remarks>
/// <para>
/// Qualification asks what a product or event <em>is</em>: an interest-rate swap, a credit default swaption,
/// an execution. The answer is computed by running every candidate predicate for that kind of object and
/// taking the single one that matches.
/// </para>
/// <para>
/// <b>This step visits the root, unlike every other step in the post-processing tier.</b> Java makes an
/// explicit <c>processor.processRosetta(path, topClass, builder, null, …)</c> call before walking, which the
/// hashing and pruning steps do not — so a document whose root <em>is</em> a qualifiable node gets
/// qualified. That matters in practice: the business-event expectations are files whose root is a
/// <c>BusinessEvent</c>.
/// </para>
/// <para>
/// <b>Reference resolution must have run first.</b> A qualify function can navigate through a reference that
/// points outside the subtree it is qualifying — <c>bond-option.json</c>'s option underlier is a scoped
/// reference whose target sits under <c>tradeLot</c>, and without it the product cannot be recognised as a
/// debt option at all. Java's own tests resolve references before qualifying for exactly this reason.
/// </para>
/// </remarks>
public static class QualifyProcessorStep
{
    /// <summary>
    /// Qualifies <paramref name="instance"/>, returning the rewritten graph and what was decided.
    /// </summary>
    public static QualificationReport Run(
        IRosettaModelObject instance, IReadOnlyList<IQualificationHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(handlers);

        var byCarrier = new Dictionary<Type, IQualificationHandler>();
        foreach (IQualificationHandler handler in handlers)
        {
            byCarrier[handler.CarrierType] = handler;
        }

        var results = new List<QualificationResult>();
        IRosettaModelObject qualified = RuneTreeRewriter.Rewrite(
            instance,
            entry: null,
            // No root guard on purpose: this is the one step that visits the root.
            (rebuilt, _, path) => QualifyNode(rebuilt, path.BuildPath(), byCarrier, results));

        return new QualificationReport(qualified, results);
    }

    private static IRosettaModelObject QualifyNode(
        IRosettaModelObject node,
        string path,
        Dictionary<Type, IQualificationHandler> byCarrier,
        List<QualificationResult> results)
    {
        if (!byCarrier.TryGetValue(node.GetType(), out IQualificationHandler? handler))
        {
            return node;
        }

        IRosettaModelObject? qualifiable = handler.GetQualifiableObject(node);
        if (qualifiable == null)
        {
            return node;
        }

        var result = new QualificationResult(qualifiable.GetType(), path, handler.Qualify(qualifiable));
        results.Add(result);

        return result.UniqueSuccess is { } winner ? handler.WithQualifier(node, winner.Name) : node;
    }
}
