using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Qualification;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Qualification;

/// <summary>
/// <see cref="QualifyProcessorStep"/>'s selection rule and walk, driven by a stub handler.
/// </summary>
/// <remarks>
/// <para>
/// The rule under test is the one a reimplementation is most likely to get wrong, because the obvious
/// behaviour is wrong: several matching candidates produce <b>no</b> qualification, exactly as zero matches
/// do. There is no tie-break and no first-match preference. Both cases are real — CDM's corpus contains a
/// business event matching two functions and correctly carrying no qualifier — so "take the first" would
/// invent qualifications the reference declines to make.
/// </para>
/// <para>
/// A stub handler is used rather than CDM's, because what is being tested is the step's arithmetic over
/// verdicts, not any particular model's predicates. <c>MetaFields</c> stands in for a carrier: it is an
/// ordinary model object and this assembly has no model of its own.
/// </para>
/// </remarks>
public class QualifyProcessorStepTests
{
    /// <summary>Answers with fixed verdicts, and records the qualification by writing it to the scheme.</summary>
    private sealed class StubHandler : IQualificationHandler
    {
        private readonly QualifyResult[] _verdicts;
        private readonly bool _qualifiable;

        internal StubHandler(bool qualifiable, params QualifyResult[] verdicts)
        {
            _qualifiable = qualifiable;
            _verdicts = verdicts;
        }

        public Type CarrierType => typeof(MetaFields);

        public IRosettaModelObject? GetQualifiableObject(IRosettaModelObject carrier) =>
            _qualifiable ? carrier : null;

        public IReadOnlyList<QualifyResult> Qualify(IRosettaModelObject qualifiable) => _verdicts;

        public IRosettaModelObject WithQualifier(IRosettaModelObject carrier, string qualifier) =>
            ((MetaFields)carrier).ToBuilder() is MetaFields.Builder builder
                ? builder.SetScheme(qualifier).Build()
                : carrier;
    }

    private static MetaFields Carrier() => MetaFields.CreateBuilder().SetExternalKey("subject").Build();

    private static QualificationReport Run(StubHandler handler, IRosettaModelObject? subject = null) =>
        QualifyProcessorStep.Run(subject ?? Carrier(), new IQualificationHandler[] { handler });

    [Fact]
    public void Qualification_WhenExactlyOneCandidateMatches_WritesThatName()
    {
        QualificationReport report = Run(new StubHandler(
            true, new QualifyResult("Miss", false), new QualifyResult("InterestRate_Fra", true)));

        Assert.Equal("InterestRate_Fra", ((MetaFields)report.Object).Scheme);
        Assert.Equal(1, report.UniquelyQualifiedObjectsCount);
    }

    /// <summary>
    /// Several matches write nothing — the case that separates a faithful port from a plausible one.
    /// </summary>
    /// <remarks>
    /// A result is still recorded, so the object counts as found but not qualified. Java reports such an
    /// object as a <em>failure</em>, not as a success with an arbitrary winner.
    /// </remarks>
    [Fact]
    public void Qualification_WhenSeveralCandidatesMatch_WritesNothing()
    {
        QualificationReport report = Run(new StubHandler(
            true, new QualifyResult("CashTransfer", true), new QualifyResult("OnDemandPayment", true)));

        Assert.Null(((MetaFields)report.Object).Scheme);
        Assert.Equal(1, report.QualifiableObjectsCount);
        Assert.Equal(0, report.UniquelyQualifiedObjectsCount);
    }

    [Fact]
    public void Qualification_WhenNoCandidateMatches_WritesNothingAndStillRecordsTheObject()
    {
        QualificationReport report = Run(new StubHandler(true, new QualifyResult("Miss", false)));

        Assert.Null(((MetaFields)report.Object).Scheme);
        Assert.Equal(1, report.QualifiableObjectsCount);
        Assert.Equal(0, report.UniquelyQualifiedObjectsCount);
    }

    /// <summary>
    /// A node with nothing to qualify is not recorded at all.
    /// </summary>
    /// <remarks>
    /// The distinction from a failed qualification is observable in the report and is what makes an empty
    /// product yield an empty result set rather than one failing entry.
    /// </remarks>
    [Fact]
    public void Qualification_WhenTheNodeHasNothingToQualify_RecordsNoResult()
    {
        QualificationReport report = Run(new StubHandler(false, new QualifyResult("Anything", true)));

        Assert.Empty(report.Results);
    }

    /// <summary>
    /// The root itself is qualified.
    /// </summary>
    /// <remarks>
    /// This step is the exception in the post-processing tier: every other pass walks attributes only, but
    /// Java visits the root here explicitly before walking. The business-event expectations are documents
    /// whose root <em>is</em> the qualifiable object, so skipping it would qualify none of them.
    /// </remarks>
    [Fact]
    public void Qualification_WhenTheRootIsItselfQualifiable_QualifiesIt()
    {
        QualificationReport report = Run(new StubHandler(true, new QualifyResult("Execution", true)));

        Assert.Equal("Execution", ((MetaFields)report.Object).Scheme);
    }

    /// <summary>
    /// A nested carrier is qualified too, and independently of the root.
    /// </summary>
    /// <remarks>
    /// CDM relies on this: 56 of the pack's 727 qualifications sit on a product reached through an option's
    /// underlier, so a walk that stopped at the outermost product would miss the entire
    /// option-on-product class.
    /// </remarks>
    [Fact]
    public void Qualification_WhenACarrierIsNested_QualifiesItAsWell()
    {
        FieldWithMeta<MetaFields> nested = FieldWithMeta<MetaFields>.CreateBuilder()
            .SetValue(Carrier())
            .Build();

        QualificationReport report = QualifyProcessorStep.Run(
            nested,
            new IQualificationHandler[] { new StubHandler(true, new QualifyResult("Nested", true)) });

        Assert.Equal("Nested", ((FieldWithMeta<MetaFields>)report.Object).Value!.Scheme);
        Assert.Single(report.Results);
    }

    /// <summary>
    /// A carrier type is claimed by identity, not by assignability.
    /// </summary>
    /// <remarks>
    /// Java looks the node's class up in a map, so a subtype of a registered carrier is not claimed.
    /// Widening to an assignability test would qualify nodes the reference leaves alone.
    /// </remarks>
    [Fact]
    public void Qualification_WhenNoHandlerClaimsTheNodeType_LeavesItAlone()
    {
        Reference unclaimed = Reference.CreateBuilder().SetScopedReference("x").Build();

        QualificationReport report = QualifyProcessorStep.Run(
            unclaimed,
            new IQualificationHandler[] { new StubHandler(true, new QualifyResult("Anything", true)) });

        Assert.Empty(report.Results);
        Assert.Same(unclaimed, report.Object);
    }
}
