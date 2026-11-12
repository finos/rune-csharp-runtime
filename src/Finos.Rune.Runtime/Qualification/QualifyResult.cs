namespace Finos.Rune.Runtime.Qualification;

/// <summary>One qualification function's verdict on one object.</summary>
/// <param name="Name">
/// The qualification name, which is the function's own name with its <c>Qualify_</c> prefix removed —
/// <c>Qualify_InterestRate_Fra</c> answers to <c>InterestRate_Fra</c>. The name is never read from the
/// function's <c>docReference</c>, which disagrees with it in at least one CDM function.
/// </param>
/// <param name="Success">Whether the function returned true.</param>
public readonly record struct QualifyResult(string Name, bool Success);

/// <summary>
/// Every candidate's verdict on one qualifiable object, and the single winner if there is one.
/// </summary>
/// <param name="QualifiableType">The type the candidates were run against.</param>
/// <param name="Path">Where the object sits, so a failure can name it.</param>
/// <param name="Results">One entry per candidate function, in registry order.</param>
public sealed record QualificationResult(
    Type QualifiableType, string Path, IReadOnlyList<QualifyResult> Results)
{
    /// <summary>
    /// The winner, and only when there is exactly one.
    /// </summary>
    /// <remarks>
    /// <b>Zero matches and several matches behave identically: no winner, and nothing is written.</b> There
    /// is no tie-break, no ordering preference, and no exception — a product matching two functions is left
    /// unqualified exactly as one matching none is. Both cases occur in CDM's own corpus: 76 of the 803
    /// qualifiable objects in the test pack match nothing, and
    /// <c>on-demand-interest-payment-output.json</c> matches both <c>CashTransfer</c> and
    /// <c>OnDemandPayment</c> and correctly carries no qualifier. Taking the first match would silently
    /// invent a qualification the reference implementation declines to make.
    /// </remarks>
    public QualifyResult? UniqueSuccess
    {
        get
        {
            QualifyResult? found = null;
            foreach (QualifyResult result in Results)
            {
                if (!result.Success)
                {
                    continue;
                }
                if (found != null)
                {
                    return null;
                }
                found = result;
            }

            return found;
        }
    }

    /// <summary>Whether this object qualified — i.e. exactly one candidate matched.</summary>
    public bool IsSuccess => UniqueSuccess != null;
}

/// <summary>The outcome of qualifying a document: the rewritten graph, and what was decided.</summary>
/// <param name="Object">The graph with each unique qualification written into it.</param>
/// <param name="Results">One entry per qualifiable object found, whether or not it qualified.</param>
public sealed record QualificationReport(IRosettaModelObject Object, IReadOnlyList<QualificationResult> Results)
{
    /// <summary>How many qualifiable objects were found, qualified or not.</summary>
    public int QualifiableObjectsCount => Results.Count;

    /// <summary>How many resolved to exactly one qualification.</summary>
    public int UniquelyQualifiedObjectsCount => Results.Count(r => r.IsSuccess);
}
