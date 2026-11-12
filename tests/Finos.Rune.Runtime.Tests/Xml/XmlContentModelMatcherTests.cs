using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Case-for-case port of rune-common's <c>XMLContentModelMatcherNamespaceTest</c> and
/// <c>XMLContentModelMatcherZeroWidthOptionalContentTest</c>, plus the ALL/ANY, occurrence and
/// unbounded behaviour the disambiguation tests exercise through the mapper.
/// </summary>
public class XmlContentModelMatcherTests
{
    private const string NsA = "urn:test:a";
    private const string NsB = "urn:test:b";

    [Fact]
    public void Route_ForRequiredNamespaceAndEqualPresentInputNamespace_Matches()
    {
        XmlContentModelNode model = Element("tradeId", NsA, "tradeId");

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("tradeId", XmlNamespaceState.Present(NsA)) });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "tradeId" }, result.PathAt(0));
    }

    [Fact]
    public void Route_ForRequiredNamespaceAndDifferentKnownInputNamespace_DoesNotMatch()
    {
        XmlContentModelNode model = Element("tradeId", NsA, "tradeId");

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("tradeId", XmlNamespaceState.Present(NsB)) });

        Assert.Equal(XmlRoutingStatus.NoMatch, result.Status);
    }

    [Fact]
    public void Route_ForRequiredNamespaceAndKnownAbsentInputNamespace_DoesNotMatch()
    {
        XmlContentModelNode model = Element("tradeId", NsA, "tradeId");

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("tradeId", XmlNamespaceState.Absent()) });

        Assert.Equal(XmlRoutingStatus.NoMatch, result.Status);
    }

    [Fact]
    public void Route_ForRequiredNamespaceAndUnknownInputNamespace_MatchesOnLocalName()
    {
        XmlContentModelNode model = Element("tradeId", NsA, "tradeId");

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("tradeId", XmlNamespaceState.Unknown()) });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "tradeId" }, result.PathAt(0));
    }

    [Fact]
    public void Route_ForUnknownInputNamespaceAcrossNamespaceQualifiedRoutes_ReportsAmbiguous()
    {
        XmlContentModelNode model = Choice(
            Element("tradeId", NsA, "aTradeId"),
            Element("tradeId", NsB, "bTradeId"));

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("tradeId", XmlNamespaceState.Unknown()) });

        Assert.Equal(XmlRoutingStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.CandidatePaths.Count);
        Assert.Contains(result.CandidatePaths, paths => paths.Any(p => p.SequenceEqual(new[] { "aTradeId" })));
        Assert.Contains(result.CandidatePaths, paths => paths.Any(p => p.SequenceEqual(new[] { "bTradeId" })));
    }

    [Fact]
    public void Route_ForSequenceWithSoleOptionalChoiceAndNoInput_Succeeds()
    {
        XmlContentModelNode model = SequenceOnce(
            OptionalChoice(PlainElement("fieldA"), PlainElement("fieldB")));

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, Array.Empty<XmlRoutingInput>());

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
    }

    [Fact]
    public void Route_ForSequenceWithSoleOptionalChoiceAndAPresentBranch_RoutesThatBranch()
    {
        XmlContentModelNode model = SequenceOnce(
            OptionalChoice(PlainElement("fieldA"), PlainElement("fieldB")));

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("fieldA", XmlNamespaceState.Unknown()) });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "fieldA" }, result.PathAt(0));
    }

    [Fact]
    public void Route_ForATradeIdentifierVirtualBranch_RoutesTradeIdThroughTheVirtualChoice()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            TradeIdentifierModel(),
            new[] { Input("partyReference"), Input("tradeId") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "partyReference" }, result.PathAt(0));
        Assert.Equal(new[] { "tradeIdentifierChoice", "tradeId" }, result.PathAt(1));
    }

    [Fact]
    public void Route_ForATradeIdentifierDirectBranch_RoutesTradeIdToTheDirectProperty()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            TradeIdentifierModel(),
            new[] { Input("issuer"), Input("tradeId") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "issuer" }, result.PathAt(0));
        Assert.Equal(new[] { "tradeId" }, result.PathAt(1));
    }

    [Fact]
    public void Route_ForMixedNestedChoices_GivesEachOneItsOwnOccurrence()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            TradeIdentifierModel(),
            new[] { Input("partyReference"), Input("tradeId"), Input("versionedTradeId") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "tradeIdentifierChoice", "tradeId" }, result.PathAt(1));
        Assert.Equal(new[] { "tradeIdentifierChoice", "versionedTradeId" }, result.PathAt(2));
        Assert.NotEqual(result.OccurrenceAt(1), result.OccurrenceAt(2));
    }

    [Fact]
    public void Route_ForATradeIdentifierMixingBothBranches_IsAmbiguous()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            TradeIdentifierModel(),
            new[] { Input("issuer"), Input("partyReference"), Input("tradeId") });

        Assert.Equal(XmlRoutingStatus.NoMatch, result.Status);
    }

    [Fact]
    public void Route_ForMisorderedInput_DoesNotMatchUntilItIsReordered()
    {
        XmlRoutingResult strict = XmlContentModelMatcher.Route(
            TradeIdentifierModel(),
            new[] { Input("versionedTradeId"), Input("partyReference") });
        XmlRoutingResult reordered = XmlContentModelMatcher.Route(
            TradeIdentifierModel(),
            new[] { Input("partyReference"), Input("versionedTradeId") });

        Assert.Equal(XmlRoutingStatus.NoMatch, strict.Status);
        Assert.Equal(XmlRoutingStatus.Success, reordered.Status);
    }

    [Fact]
    public void Route_ForUnboundedGreedyElementsFollowedByAChoice_FillsTheBaseElementsFirst()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            FxTargetKnockoutForwardModel(),
            new[]
            {
                Input("constantPayoffRegion"), Input("linearPayoffRegion"), Input("constantPayoffRegion"),
            });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "constantPayoffRegion" }, result.PathAt(0));
        Assert.Equal(new[] { "linearPayoffRegion" }, result.PathAt(1));
        Assert.Equal(new[] { "fxTargetKnockoutForwardChoice", "constantPayoffRegion" }, result.PathAt(2));
    }

    [Fact]
    public void Route_ForAMissingRequiredElement_DoesNotMatch()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            FxTargetKnockoutForwardModel(),
            new[] { Input("constantPayoffRegion") });

        Assert.Equal(XmlRoutingStatus.NoMatch, result.Status);
    }

    [Fact]
    public void Route_ForARepeatedSequenceOfTwoLeaves_GivesEachIterationItsOwnOccurrence()
    {
        XmlContentModelNode model = new XmlContentModelNode(
            XmlContentModelNodeType.Sequence,
            minOccurs: 0,
            maxOccurs: XmlOccursMax.Unbounded,
            children: new[]
            {
                XmlContentModelNode.Element("firstValue", new[] { "entry", "firstValue" }, 1, XmlOccursMax.Of(1)),
                XmlContentModelNode.Element("secondValue", new[] { "entry", "secondValue" }, 1, XmlOccursMax.Of(1)),
            });

        XmlRoutingResult result = XmlContentModelMatcher.Route(
            model,
            new[] { Input("firstValue"), Input("secondValue"), Input("firstValue"), Input("secondValue") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(result.OccurrenceAt(0), result.OccurrenceAt(1));
        Assert.Equal(result.OccurrenceAt(2), result.OccurrenceAt(3));
        Assert.NotEqual(result.OccurrenceAt(0), result.OccurrenceAt(2));
    }

    [Fact]
    public void Route_ForAnAllGroupWithUnorderedInput_RoutesBothIntoOneOccurrence()
    {
        XmlContentModelNode model = new XmlContentModelNode(
            XmlContentModelNodeType.All,
            children: new[]
            {
                XmlContentModelNode.Element("firstValue", new[] { "entry", "firstValue" }, 1, XmlOccursMax.Of(1)),
                XmlContentModelNode.Element("secondValue", new[] { "entry", "secondValue" }, 1, XmlOccursMax.Of(1)),
            });

        XmlRoutingResult result = XmlContentModelMatcher.Route(
            model, new[] { Input("secondValue"), Input("firstValue") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "entry", "secondValue" }, result.PathAt(0));
        Assert.Equal(new[] { "entry", "firstValue" }, result.PathAt(1));
        Assert.Equal(result.OccurrenceAt(0), result.OccurrenceAt(1));
    }

    [Fact]
    public void Route_ForAnAnyNode_RoutesAnUnknownNameToTheWildcardPath()
    {
        XmlContentModelNode model = XmlContentModelNode.Sequence(
            1,
            XmlOccursMax.Of(1),
            XmlContentModelNode.Any(new[] { "entry", "wildcardValue" }, 1, XmlOccursMax.Of(1)),
            XmlContentModelNode.Element("knownValue", new[] { "knownValue" }, 1, XmlOccursMax.Of(1)));

        XmlRoutingResult result = XmlContentModelMatcher.Route(
            model, new[] { Input("unexpectedValue"), Input("knownValue") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Equal(new[] { "entry", "wildcardValue" }, result.PathAt(0));
        Assert.Equal(new[] { "knownValue" }, result.PathAt(1));
    }

    [Fact]
    public void Route_ForAnElementNodeWithNoPath_ConsumesTheInputWithoutRoutingIt()
    {
        XmlContentModelNode model = new XmlContentModelNode(
            XmlContentModelNodeType.Element, "ignored", minOccurs: 1, maxOccurs: XmlOccursMax.Of(1));

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, new[] { Input("ignored") });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        Assert.Empty(result.Assignments);
    }

    [Fact]
    public void Route_ForInputTheModelCannotConsume_DoesNotMatch()
    {
        XmlContentModelNode model = SequenceOnce(PlainElement("fieldA"));

        XmlRoutingResult result = XmlContentModelMatcher.Route(
            model, new[] { Input("fieldA"), Input("unexpected") });

        Assert.Equal(XmlRoutingStatus.NoMatch, result.Status);
    }

    internal static XmlContentModelNode TradeIdentifierModel() => XmlContentModelNode.Choice(
        1,
        XmlOccursMax.Of(1),
        XmlContentModelNode.Sequence(
            1,
            XmlOccursMax.Of(1),
            XmlContentModelNode.Element("issuer", new[] { "issuer" }, 1, XmlOccursMax.Of(1)),
            XmlContentModelNode.Element("tradeId", new[] { "tradeId" }, 1, XmlOccursMax.Of(1))),
        XmlContentModelNode.Sequence(
            1,
            XmlOccursMax.Of(1),
            XmlContentModelNode.Element("partyReference", new[] { "partyReference" }, 1, XmlOccursMax.Of(1)),
            XmlContentModelNode.Element("accountReference", new[] { "accountReference" }, 0, XmlOccursMax.Of(1)),
            XmlContentModelNode.Choice(
                0,
                XmlOccursMax.Unbounded,
                XmlContentModelNode.Element(
                    "tradeId", new[] { "tradeIdentifierChoice", "tradeId" }, 1, XmlOccursMax.Of(1)),
                XmlContentModelNode.Element(
                    "versionedTradeId", new[] { "tradeIdentifierChoice", "versionedTradeId" }, 1, XmlOccursMax.Of(1)))));

    private static XmlContentModelNode FxTargetKnockoutForwardModel() => XmlContentModelNode.Sequence(
        1,
        XmlOccursMax.Of(1),
        XmlContentModelNode.Element("pivot", new[] { "pivot" }, 0, XmlOccursMax.Of(1)),
        XmlContentModelNode.Element("constantPayoffRegion", new[] { "constantPayoffRegion" }, 0, XmlOccursMax.Unbounded),
        XmlContentModelNode.Element("linearPayoffRegion", new[] { "linearPayoffRegion" }, 1, XmlOccursMax.Of(1)),
        XmlContentModelNode.Choice(
            0,
            XmlOccursMax.Unbounded,
            XmlContentModelNode.Element(
                "constantPayoffRegion",
                new[] { "fxTargetKnockoutForwardChoice", "constantPayoffRegion" },
                1,
                XmlOccursMax.Of(1)),
            XmlContentModelNode.Element(
                "linearPayoffRegion",
                new[] { "fxTargetKnockoutForwardChoice", "linearPayoffRegion" },
                1,
                XmlOccursMax.Of(1))));

    private static XmlRoutingInput Input(string xmlName) => new XmlRoutingInput(0, xmlName, XmlNamespaceState.Unknown());

    private static XmlRoutingInput Input(string xmlName, XmlNamespaceState ns) => new XmlRoutingInput(0, xmlName, ns);

    private static XmlContentModelNode Element(string xmlName, string ns, string path) =>
        XmlContentModelNode.Element(xmlName, new[] { path }, 1, XmlOccursMax.Of(1), ns);

    private static XmlContentModelNode PlainElement(string name) =>
        XmlContentModelNode.Element(name, new[] { name }, 1, XmlOccursMax.Of(1));

    private static XmlContentModelNode Choice(params XmlContentModelNode[] children) =>
        XmlContentModelNode.Choice(1, XmlOccursMax.Of(1), children);

    private static XmlContentModelNode OptionalChoice(params XmlContentModelNode[] children) =>
        XmlContentModelNode.Choice(0, XmlOccursMax.Of(1), children);

    private static XmlContentModelNode SequenceOnce(params XmlContentModelNode[] children) =>
        XmlContentModelNode.Sequence(1, XmlOccursMax.Of(1), children);
}
