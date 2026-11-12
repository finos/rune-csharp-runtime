using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Port of rune-common's <c>XmlContentModelDisambiguationTest</c> and
/// <c>XmlContentModelSerializationOrderTest</c>, driven through the C# codec contract instead of
/// Jackson: the same worked examples, the same lenient recovery, and the same content-model-ordered
/// write with a root-only schemaLocation.
/// </summary>
public class XmlContentModelDisambiguationTests
{
    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Fact]
    public void Read_ForATradeIdentifierVirtualBranch_RoutesTradeIdIntoTheVirtualChoice()
    {
        const string Xml = "<FpmlTradeIdentifier id=\"ti-1\">"
            + "<partyReference href=\"party-1\"/>"
            + "<tradeId scheme=\"urn:trade-id\">ABC-123</tradeId>"
            + "</FpmlTradeIdentifier>";

        FpmlTradeIdentifier actual = XmlDocumentReader.Read<FpmlTradeIdentifier>(Xml, _registry, out XmlReadContext context);

        Assert.Equal("ti-1", actual.Id);
        Assert.Equal("party-1", actual.PartyReference!.Href);
        Assert.Null(actual.TradeId);
        Assert.Single(actual.TradeIdentifierChoice!);
        Assert.Equal("ABC-123", actual.TradeIdentifierChoice![0].TradeId!.Value);
        Assert.Equal("urn:trade-id", actual.TradeIdentifierChoice[0].TradeId!.Scheme);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void Read_ForATradeIdentifierDirectBranch_RoutesTradeIdToTheDirectProperty()
    {
        const string Xml = "<FpmlTradeIdentifier id=\"ti-2\">"
            + "<issuer scheme=\"urn:issuer\">BANK-A</issuer>"
            + "<tradeId scheme=\"urn:trade-id\">ABC-123</tradeId>"
            + "</FpmlTradeIdentifier>";

        FpmlTradeIdentifier actual = XmlDocumentReader.Read<FpmlTradeIdentifier>(Xml, _registry, out _);

        Assert.Equal("ti-2", actual.Id);
        Assert.Equal("ABC-123", actual.TradeId!.Value);
        Assert.Equal("urn:trade-id", actual.TradeId.Scheme);
        Assert.Equal("BANK-A", actual.Issuer!.Value);
        Assert.Null(actual.TradeIdentifierChoice);
    }

    [Fact]
    public void Read_ForMixedNestedChoices_ProducesOneVirtualObjectPerOccurrence()
    {
        const string Xml = "<FpmlTradeIdentifier>"
            + "<partyReference href=\"party-1\"/>"
            + "<tradeId scheme=\"urn:trade-id\">T-1</tradeId>"
            + "<versionedTradeId scheme=\"urn:version\">V-2</versionedTradeId>"
            + "</FpmlTradeIdentifier>";

        FpmlTradeIdentifier actual = XmlDocumentReader.Read<FpmlTradeIdentifier>(Xml, _registry, out _);

        Assert.Equal(2, actual.TradeIdentifierChoice!.Count);
        Assert.Equal("T-1", actual.TradeIdentifierChoice[0].TradeId!.Value);
        Assert.Null(actual.TradeIdentifierChoice[0].VersionedTradeId);
        Assert.Null(actual.TradeIdentifierChoice[1].TradeId);
        Assert.Equal("V-2", actual.TradeIdentifierChoice[1].VersionedTradeId!.Value);
    }

    [Fact]
    public void Read_ForARepeatedVirtualSequence_GroupsEachIterationIntoItsOwnEntry()
    {
        const string Xml = "<MultiLeafContainer>"
            + "<firstValue>A1</firstValue><secondValue>A2</secondValue>"
            + "<firstValue>B1</firstValue><secondValue>B2</secondValue>"
            + "</MultiLeafContainer>";

        var actual = (LeafContainer)XmlDocumentReader.Read(Xml, _registry, out _);

        Assert.Equal(2, actual.Entry!.Count);
        Assert.Equal("A1", actual.Entry[0].FirstValue!.Value);
        Assert.Equal("A2", actual.Entry[0].SecondValue!.Value);
        Assert.Equal("B1", actual.Entry[1].FirstValue!.Value);
        Assert.Equal("B2", actual.Entry[1].SecondValue!.Value);
    }

    [Fact]
    public void Read_ForAnAllGroupWithUnorderedChildren_RoutesThemIntoOneVirtualOccurrence()
    {
        const string Xml = "<AllVirtualContainer>"
            + "<secondValue>A2</secondValue><firstValue>A1</firstValue>"
            + "</AllVirtualContainer>";

        var actual = (LeafContainer)XmlDocumentReader.Read(Xml, _registry, out _);

        Assert.Single(actual.Entry!);
        Assert.Equal("A1", actual.Entry![0].FirstValue!.Value);
        Assert.Equal("A2", actual.Entry[0].SecondValue!.Value);
    }

    [Fact]
    public void Read_ForAnAnyNode_RoutesTheWildcardElementToItsVirtualPath()
    {
        const string Xml = "<AnyVirtualContainer>"
            + "<unexpectedValue>WILD</unexpectedValue><knownValue>KNOWN</knownValue>"
            + "</AnyVirtualContainer>";

        AnyVirtualContainer actual = XmlDocumentReader.Read<AnyVirtualContainer>(Xml, _registry, out _);

        Assert.Equal("KNOWN", actual.KnownValue!.Value);
        Assert.Single(actual.Entry!);
        Assert.Equal("WILD", actual.Entry![0].WildcardValue!.Value);
    }

    [Fact]
    public void Read_ForAMultiSegmentVirtualPath_CreatesEveryIntermediateLayer()
    {
        const string Xml = "<MultiLayerContainer>"
            + "<firstValue>A1</firstValue><secondValue>A2</secondValue>"
            + "<firstValue>B1</firstValue><secondValue>B2</secondValue>"
            + "</MultiLayerContainer>";

        MultiLayerContainer actual = XmlDocumentReader.Read<MultiLayerContainer>(Xml, _registry, out _);

        Assert.Equal(2, actual.Outer!.Count);
        Assert.Single(actual.Outer[0].Inner!);
        Assert.Equal("A1", actual.Outer[0].Inner![0].FirstValue!.Value);
        Assert.Equal("A2", actual.Outer[0].Inner![0].SecondValue!.Value);
        Assert.Single(actual.Outer[1].Inner!);
        Assert.Equal("B1", actual.Outer[1].Inner![0].FirstValue!.Value);
        Assert.Equal("B2", actual.Outer[1].Inner![0].SecondValue!.Value);
    }

    [Fact]
    public void Read_ForMisorderedButValidContent_StablyReordersAndStillRoutesIt()
    {
        const string Xml = "<FpmlTradeIdentifier>"
            + "<versionedTradeId scheme=\"urn:version\">V-1</versionedTradeId>"
            + "<partyReference href=\"party-1\"/>"
            + "</FpmlTradeIdentifier>";

        FpmlTradeIdentifier actual = XmlDocumentReader.Read<FpmlTradeIdentifier>(Xml, _registry, out XmlReadContext context);

        Assert.Equal("party-1", actual.PartyReference!.Href);
        Assert.Single(actual.TradeIdentifierChoice!);
        Assert.Equal("V-1", actual.TradeIdentifierChoice![0].VersionedTradeId!.Value);
        Assert.Equal(1, context.ReorderedObjectCount);
    }

    [Fact]
    public void Read_ForGenuinelyAmbiguousContent_AbandonsRoutingAndKeepsWhatItCanPlace()
    {
        const string Xml = "<FpmlTradeIdentifier id=\"ti-ambiguous\">"
            + "<issuer scheme=\"urn:issuer\">BANK-A</issuer>"
            + "<partyReference href=\"party-1\"/>"
            + "<tradeId scheme=\"urn:trade-id\">ABC-123</tradeId>"
            + "</FpmlTradeIdentifier>";

        FpmlTradeIdentifier actual = XmlDocumentReader.Read<FpmlTradeIdentifier>(Xml, _registry, out XmlReadContext context);

        Assert.Equal("ti-ambiguous", actual.Id);
        Assert.Equal("BANK-A", actual.Issuer!.Value);
        Assert.Equal("party-1", actual.PartyReference!.Href);
        Assert.Equal(1, context.AbandonedRoutingCount);
        Assert.Contains(context.Diagnostics, d => d.Contains("Cannot route XML content", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_ForAVirtualChoice_FollowsTheContentModelOrderAndRoundTrips()
    {
        FpmlTradeIdentifier value = FpmlTradeIdentifier.CreateBuilder()
            .SetPartyReference(FpmlReference.CreateBuilder().SetHref("party-1"))
            .Also(b => b.AddTradeIdentifierChoice()
                .SetVersionedTradeId(FpmlTextValue.CreateBuilder().SetValue("V-1")))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry);

        Assert.Equal(
            """
            <FpmlTradeIdentifier>
              <partyReference href="party-1"/>
              <versionedTradeId>V-1</versionedTradeId>
            </FpmlTradeIdentifier>
            """.ReplaceLineEndings("\n"),
            xml);
        Assert.Equal(value, XmlDocumentReader.Read<FpmlTradeIdentifier>(xml, _registry, out _));
    }

    [Fact]
    public void Write_ForAPresentSetTheContentModelCannotConsume_KeepsDescriptorOrderWithoutThrowing()
    {
        FpmlTradeIdentifier value = FpmlTradeIdentifier.CreateBuilder()
            .SetIssuer(FpmlTextValue.CreateBuilder().SetValue("BANK-A"))
            .SetPartyReference(FpmlReference.CreateBuilder().SetHref("party-1"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry);

        Assert.Equal(
            """
            <FpmlTradeIdentifier>
              <issuer>BANK-A</issuer>
              <partyReference href="party-1"/>
            </FpmlTradeIdentifier>
            """.ReplaceLineEndings("\n"),
            xml);
    }

    [Fact]
    public void Write_ForANestedContentModelElement_EmitsSchemaLocationOnTheRootOnly()
    {
        SchemaLocationContainer container = SchemaLocationContainer.CreateBuilder()
            .SetTradeIdentifier(FpmlTradeIdentifier.CreateBuilder()
                .SetPartyReference(FpmlReference.CreateBuilder().SetHref("party-1"))
                .Also(b => b.AddTradeIdentifierChoice()
                    .SetVersionedTradeId(FpmlTextValue.CreateBuilder().SetValue("V-1"))))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(
            container, _registry, new XmlWriteOptions { SchemaLocation = "urn:my.schema ../schema/schema.xsd" });

        Assert.Equal(1, CountOccurrences(xml, "xsi:schemaLocation="));
        Assert.Contains("<SchemaLocationContainer", xml, StringComparison.Ordinal);
        Assert.Equal(container, XmlDocumentReader.Read<SchemaLocationContainer>(xml, _registry, out _));
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0;
        int index = text.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }
        return count;
    }
}
