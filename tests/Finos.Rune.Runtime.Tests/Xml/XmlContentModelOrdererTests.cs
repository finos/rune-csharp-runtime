using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Case-for-case port of rune-common's <c>XMLContentModelOrdererTest</c>, plus the occurs-bound
/// cases that <c>OccursMax</c> carries.
/// </summary>
public class XmlContentModelOrdererTests
{
    [Fact]
    public void Order_ForABranchedChoice_PicksTheBranchOrderTheDataImplies()
    {
        XmlContentModelNode model = XmlContentModelNode.Choice(
            1,
            XmlOccursMax.Of(1),
            XmlContentModelNode.Sequence(
                1, XmlOccursMax.Of(1), Element("x", 1), Element("a", 1), Element("b", 1)),
            XmlContentModelNode.Sequence(
                1, XmlOccursMax.Of(1), Element("y", 1), Element("b", 1), Element("a", 1)));
        var orderer = new XmlContentModelOrderer(model);

        Assert.Equal(new[] { "x", "a", "b" }, orderer.Order(new[] { "x", "a", "b" }));
        Assert.Equal(new[] { "y", "b", "a" }, orderer.Order(new[] { "y", "a", "b" }));
    }

    [Fact]
    public void Order_ForTheTradeIdentifierShape_PutsPartyReferenceBeforeTheVirtualChoice()
    {
        XmlContentModelNode model = XmlContentModelNode.Choice(
            1,
            XmlOccursMax.Of(1),
            XmlContentModelNode.Sequence(1, XmlOccursMax.Of(1), Element("issuer", 1), Element("tradeId", 1)),
            XmlContentModelNode.Sequence(
                1,
                XmlOccursMax.Of(1),
                XmlContentModelNode.Sequence(
                    1, XmlOccursMax.Of(1), Element("partyReference", 1), Element("accountReference", 0)),
                XmlContentModelNode.Choice(
                    0,
                    XmlOccursMax.Unbounded,
                    Nested("tradeIdentifierChoice", "tradeId"),
                    Nested("tradeIdentifierChoice", "versionedTradeId"))));
        var orderer = new XmlContentModelOrderer(model);

        Assert.Equal(
            new[] { "issuer", "tradeId", "partyReference", "accountReference", "tradeIdentifierChoice" },
            orderer.ContentModelProperties);
        Assert.Equal(
            new[] { "partyReference", "tradeIdentifierChoice" },
            orderer.Order(new[] { "partyReference", "tradeIdentifierChoice" }));
        Assert.Equal(
            new[] { "partyReference", "accountReference", "tradeIdentifierChoice" },
            orderer.Order(new[] { "partyReference", "accountReference", "tradeIdentifierChoice" }));
        Assert.Equal(new[] { "issuer", "tradeId" }, orderer.Order(new[] { "issuer", "tradeId" }));
    }

    [Fact]
    public void Order_ForAPresentSetTheModelCannotConsume_ReturnsNull()
    {
        XmlContentModelNode model = XmlContentModelNode.Sequence(
            1, XmlOccursMax.Of(1), Element("a", 1), Element("b", 1));
        var orderer = new XmlContentModelOrderer(model);

        Assert.Null(orderer.Order(new[] { "a", "c" }));
    }

    [Fact]
    public void Order_ForAnEmptyPresentSet_ReturnsAnEmptyOrder()
    {
        XmlContentModelNode model = XmlContentModelNode.Sequence(1, XmlOccursMax.Of(1), Element("a", 0));
        var orderer = new XmlContentModelOrderer(model);

        Assert.Empty(orderer.Order(Array.Empty<string>())!);
    }

    [Fact]
    public void Order_ForARequiredElementThatIsAbsent_ReturnsNull()
    {
        XmlContentModelNode model = XmlContentModelNode.Sequence(
            1, XmlOccursMax.Of(1), Element("a", 1), Element("b", 1));
        var orderer = new XmlContentModelOrderer(model);

        Assert.Null(orderer.Order(new[] { "b" }));
    }

    [Fact]
    public void ContentModelProperties_ForAnAnyNodeWithoutAPath_ExcludesIt()
    {
        XmlContentModelNode model = XmlContentModelNode.Sequence(
            1,
            XmlOccursMax.Of(1),
            XmlContentModelNode.Any(null, 0, XmlOccursMax.Unbounded),
            Element("known", 1));
        var orderer = new XmlContentModelOrderer(model);

        Assert.Equal(new[] { "known" }, orderer.ContentModelProperties);
        Assert.Equal(new[] { "known" }, orderer.Order(new[] { "known" }));
    }

    [Fact]
    public void BoundedValue_ForAnUnboundedMax_IsTheRemainingInputLength()
    {
        Assert.True(XmlOccursMax.Unbounded.IsUnbounded);
        Assert.Null(XmlOccursMax.Unbounded.Value);
        Assert.Equal(7, XmlOccursMax.Unbounded.BoundedValue(7));
        Assert.Equal("unbounded", XmlOccursMax.Unbounded.ToString());
    }

    [Fact]
    public void BoundedValue_ForAFiniteMax_IgnoresTheRemainingInputLength()
    {
        XmlOccursMax max = XmlOccursMax.Of(3);

        Assert.False(max.IsUnbounded);
        Assert.Equal(3, max.Value);
        Assert.Equal(3, max.BoundedValue(7));
        Assert.Equal(XmlOccursMax.Of(3), max);
        Assert.NotEqual(XmlOccursMax.Of(2), max);
    }

    [Fact]
    public void Of_ForANegativeMax_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => XmlOccursMax.Of(-1));

    [Fact]
    public void MinOccursOrDefault_ForAnUnsetBound_IsOne()
    {
        var node = new XmlContentModelNode(XmlContentModelNodeType.Sequence);

        Assert.Equal(1, node.MinOccursOrDefault);
        Assert.Equal(1, node.MaxOccursOrDefault(9));
    }

    private static XmlContentModelNode Element(string name, int minOccurs) =>
        XmlContentModelNode.Element(name, new[] { name }, minOccurs, XmlOccursMax.Of(1));

    private static XmlContentModelNode Nested(string property, string name) =>
        XmlContentModelNode.Element(name, new[] { property, name }, 1, XmlOccursMax.Of(1));
}
