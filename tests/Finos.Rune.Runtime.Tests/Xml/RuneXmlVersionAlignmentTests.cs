using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Pins the one behavioural difference between the two rune-common versions in play: the oracle the
/// pinned CDM pack was produced with (12.3.1, <c>cdm.pin</c>'s <c>RUNE_SERIALIZATION_VERSION</c>)
/// and the bundle this repository's generator depends on (12.5.1, <c>rosetta.bundle.version</c>).
/// </summary>
/// <remarks>
/// <para>Everything under <c>com.regnosys.rosetta.common.serialisation.xml</c> is identical between
/// the two tags apart from its licence-header year — matcher, orderer, disambiguating deserializer,
/// virtual path helper, substitution map, config model, mapper and module — as are the XML test
/// fixtures. So every semantic these tests assert elsewhere holds for both versions, and none of it
/// is version-conditional.</para>
/// <para>The difference is confined to pretty printing: 12.5.1 forces
/// <c>DefaultXmlPrettyPrinter.withCustomNewLine("\n")</c>, while 12.3.1 leaves Jackson's default,
/// whose separator is <c>System.getProperty("line.separator")</c>. Indentation, the empty-element
/// form and attribute order are unchanged. These tests assert exactly that shape of difference:
/// separator-only, pretty-print-only, and absent from compact output.</para>
/// </remarks>
public class RuneXmlVersionAlignmentTests
{
    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Fact]
    public void OracleVersion_AndGeneratorVersion_AreTheTwoPinnedRuneCommonBundles()
    {
        Assert.Equal("12.3.1", RuneXmlVersionAlignment.OracleVersion);
        Assert.Equal("12.5.1", RuneXmlVersionAlignment.GeneratorVersion);
        Assert.NotEqual(RuneXmlVersionAlignment.OracleVersion, RuneXmlVersionAlignment.GeneratorVersion);
    }

    [Fact]
    public void Default_ForAPrettyPrintedDocument_SeparatesLinesWithLineFeedOnly()
    {
        string xml = XmlDocumentWriter.WriteToString(Party(), _registry);

        Assert.Contains("\n", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", xml, StringComparison.Ordinal);
        Assert.Equal(xml, XmlDocumentWriter.WriteToString(Party(), _registry, RuneXmlVersionAlignment.LineFeedNewLines()));
    }

    [Fact]
    public void PlatformNewLines_ForAPrettyPrintedDocument_UsesTheHostSeparator()
    {
        string xml = XmlDocumentWriter.WriteToString(
            Party(), _registry, RuneXmlVersionAlignment.PlatformNewLines());

        Assert.Contains(Environment.NewLine, xml, StringComparison.Ordinal);
        Assert.Equal(
            XmlDocumentWriter.WriteToString(Party(), _registry).Replace("\n", Environment.NewLine, StringComparison.Ordinal),
            xml);
    }

    [Fact]
    public void BothPresets_DifferOnlyInTheSeparator_NeverInContent()
    {
        string lineFeed = XmlDocumentWriter.WriteToString(
            Party(), _registry, RuneXmlVersionAlignment.LineFeedNewLines());
        string platform = XmlDocumentWriter.WriteToString(
            Party(), _registry, RuneXmlVersionAlignment.PlatformNewLines());

        Assert.Equal(lineFeed, platform.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void BothPresets_ForCompactOutput_AreByteIdentical()
    {
        var compact = new XmlWriteOptions { Indent = false };

        string lineFeed = XmlDocumentWriter.WriteToString(
            Party(), _registry, RuneXmlVersionAlignment.LineFeedNewLines(compact));
        string platform = XmlDocumentWriter.WriteToString(
            Party(), _registry, RuneXmlVersionAlignment.PlatformNewLines(compact));

        Assert.Equal(lineFeed, platform);
        Assert.DoesNotContain("\n", lineFeed, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformNewLines_ForIndentationAndEmptyElementForm_ChangesNeither()
    {
        Zoo zoo = Zoo.CreateBuilder()
            .AddAnimal(Goat.CreateBuilder().SetName("Goatee"))
            .AddAnimal(Cow.CreateBuilder().SetName("Moomoo"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(zoo, _registry, RuneXmlVersionAlignment.PlatformNewLines());

        Assert.Equal(
            """
            <Zoo>
              <goat name="Goatee"/>
              <cow name="Moomoo"/>
            </Zoo>
            """.ReplaceLineEndings("\n"),
            xml.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WithNewLine_ForACustomSeparator_LeavesEveryOtherOptionAlone()
    {
        var original = new XmlWriteOptions
        {
            Indent = false,
            IndentChars = "\t",
            OmitXmlDeclaration = false,
            SchemaLocation = "urn:x x.xsd",
            CompactEmptyElements = false,
        };

        XmlWriteOptions copy = original.WithNewLine("\r\n");

        Assert.Equal("\r\n", copy.NewLine);
        Assert.False(copy.Indent);
        Assert.Equal("\t", copy.IndentChars);
        Assert.False(copy.OmitXmlDeclaration);
        Assert.Equal("urn:x x.xsd", copy.SchemaLocation);
        Assert.False(copy.CompactEmptyElements);
        Assert.Equal("\n", original.NewLine);
    }

    [Fact]
    public void Read_ForADocumentWithCarriageReturnNewLines_IsUnaffectedByTheSeparator()
    {
        string crlf = XmlDocumentWriter.WriteToString(
            Party(), _registry, RuneXmlVersionAlignment.PlatformNewLines(new XmlWriteOptions { NewLine = "\r\n" }));
        string lf = XmlDocumentWriter.WriteToString(Party(), _registry);

        Assert.Equal(
            XmlDocumentReader.Read<Party>(lf, _registry, out _),
            XmlDocumentReader.Read<Party>(crlf, _registry, out _));
    }

    private static Party Party() => Model.Party.CreateBuilder()
        .SetPartyNameModel(PartyNameModel.CreateBuilder().SetPartyName("my name"))
        .SetPartyModel(PartyModel.CreateBuilder().AddPartyId("myId1").AddPartyId("myId2"))
        .Build();
}
