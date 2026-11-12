using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Port of the rune-common <c>XmlSerialisationTest</c> golden fixtures that the C# runtime is
/// responsible for: ATTRIBUTE + VALUE representation, VIRTUAL flattening of repeated lists,
/// substitution groups including mixed lists, root extension, prune-after-read, unknown leniency
/// and round-tripping. The expected XML is byte-for-byte the body of the Java fixture files (their
/// license header is a rune-common artifact and is not part of the serializer's output).
/// </summary>
public class XmlSerialisationFixtureTests
{
    private const string SchemaLocation = "urn:my.schema ../schema/schema.xsd";

    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Fact]
    public void Write_ForADocumentWithAttributeAndValueRepresentation_MatchesTheJavaFixture()
    {
        TopLevel document = Document();

        string xml = XmlDocumentWriter.WriteToString(
            document, _registry, new XmlWriteOptions { SchemaLocation = SchemaLocation });

        Assert.Equal(ExpectedDocument, xml);
    }

    [Fact]
    public void Read_ForADocumentWithAttributeAndValueRepresentation_RebuildsTheObject()
    {
        TopLevel actual = XmlDocumentReader.Read<TopLevel>(ExpectedDocument, _registry, out XmlReadContext context);

        Assert.Equal(Document(), actual);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void Write_ForARootTypeExtension_EmitsTheExtraElementLast()
    {
        TopLevelExtension document = TopLevelExtension.CreateBuilder()
            .SetAttr(FooBuilder("My value", "Foo", "Bar"))
            .SetValue(Measure.CreateBuilder().SetValue(1m).SetUnit(UnitEnum.Meter))
            .SetDocumentExtensionAttr("Document Extension Attribute Value")
            .Build();

        string xml = XmlDocumentWriter.WriteToString(
            document, _registry, new XmlWriteOptions { SchemaLocation = SchemaLocation });

        Assert.Equal(ExpectedExtendedDocument, xml);
        Assert.Equal(document, XmlDocumentReader.Read<TopLevelExtension>(xml, _registry, out _));
    }

    [Fact]
    public void Read_ForARootWithAnEmptyChildElement_PrunesTheEmptyChildAway()
    {
        const string Xml = """
            <TopLevel xmlns="urn:my.schema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="urn:my.schema ../schema/schema.xsd">
              <!-- this should get pruned -->
              <Attr/>
              <Value Unit="Meter">1</Value>
            </TopLevel>
            """;

        TopLevel actual = XmlDocumentReader.Read<TopLevel>(Xml, _registry, out _);

        Assert.Equal(1m, actual.Value!.Value);
        Assert.Equal(UnitEnum.Meter, actual.Value.Unit);
        Assert.Null(actual.Attr);
    }

    [Fact]
    public void Read_WithEmptyElementPreservation_KeepsTheEmptyChild()
    {
        const string Xml = """
            <TopLevel xmlns="urn:my.schema">
              <Attr/>
              <Value Unit="Meter">1</Value>
            </TopLevel>
            """;
        var context = new XmlReadContext(
            _registry, new XmlReadOptions { PreserveEmptyElements = true });

        TopLevel actual =
            XmlDocumentReader.ReadAs<TopLevel>(Xml, TopLevelCodec.Instance, context);

        Assert.NotNull(actual.Attr);
        Assert.Null(actual.Attr.XmlValue);
        Assert.Null(actual.Attr.Attr1);
    }

    [Fact]
    public void Write_ForATypeWithAnElementNamedType_MatchesTheJavaFixture()
    {
        TypeWithTypeElement value = TypeWithTypeElement.CreateBuilder()
            .SetFirstElement("first")
            .SetType("My type")
            .Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry);

        Assert.Equal(
            """
            <TypeWithTypeElement>
              <firstElement>first</firstElement>
              <type>My type</type>
            </TypeWithTypeElement>
            """.ReplaceLineEndings("\n"),
            xml);
        Assert.Equal(value, XmlDocumentReader.Read<TypeWithTypeElement>(xml, _registry, out _));
    }

    [Fact]
    public void Write_ForAMultiCardinalityProperty_EmitsRepeatedElementsWithNoListWrapper()
    {
        MulticardinalityContainer container = MulticardinalityContainer.CreateBuilder()
            .Also(b => b.Foo.Add(FooBuilder("foo1", "Foo", "Bar")))
            .Also(b => b.Foo.Add(FooBuilder("foo2", "Qux")))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(container, _registry);

        Assert.Equal(
            """
            <MulticardinalityContainer>
              <foo>
                <XMLValue>foo1</XMLValue>
                <Attr1>Foo</Attr1>
                <Attr1>Bar</Attr1>
              </foo>
              <foo>
                <XMLValue>foo2</XMLValue>
                <Attr1>Qux</Attr1>
              </foo>
            </MulticardinalityContainer>
            """.ReplaceLineEndings("\n"),
            xml);
        Assert.Equal(container, XmlDocumentReader.Read<MulticardinalityContainer>(xml, _registry, out _));
    }

    [Fact]
    public void Write_ForAMultiCardinalityAttributeInsideAVirtualGroup_FlattensItWithoutAWrapper()
    {
        Party party = Party.CreateBuilder()
            .SetPartyNameModel(PartyNameModel.CreateBuilder().SetPartyName("my name"))
            .SetPartyModel(PartyModel.CreateBuilder().AddPartyId("myId1").AddPartyId("myId2"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(party, _registry);

        Assert.Equal(
            """
            <Party>
              <partyName>my name</partyName>
              <partyId>myId1</partyId>
              <partyId>myId2</partyId>
            </Party>
            """.ReplaceLineEndings("\n"),
            xml);
        Assert.Equal(party, XmlDocumentReader.Read<Party>(xml, _registry, out _));
    }

    [Fact]
    public void Read_ForAMultiVirtualGroupWithoutAContentModel_AccumulatesDistinctChildren()
    {
        const string Xml = """
            <FpmlTradeIdentifierNoContent>
              <tradeId>trade-1</tradeId>
              <versionedTradeId>version-2</versionedTradeId>
            </FpmlTradeIdentifierNoContent>
            """;
        var context = new XmlReadContext(_registry);

        FpmlTradeIdentifier actual = XmlDocumentReader.ReadAs<FpmlTradeIdentifier>(
            Xml, FpmlTradeIdentifierNoContentCodec.Instance, context);

        FpmlTradeIdentifierChoice choice = Assert.Single(actual.TradeIdentifierChoice!);
        Assert.Equal("trade-1", choice.TradeId!.Value);
        Assert.Equal("version-2", choice.VersionedTradeId!.Value);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void Write_ForASubstitutionGroupMember_UsesTheMembersOwnElementName()
    {
        AnimalContainer container = AnimalContainer.CreateBuilder()
            .SetAnimal(Goat.CreateBuilder().SetName("Goatee"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(container, _registry);

        Assert.Equal(
            """
            <AnimalContainer>
              <goat name="Goatee"/>
            </AnimalContainer>
            """.ReplaceLineEndings("\n"),
            xml);
        Assert.Equal(container, XmlDocumentReader.Read<AnimalContainer>(xml, _registry, out _));
    }

    [Fact]
    public void Write_ForAMixedSubstitutionGroupList_UsesEachItemsOwnElementName()
    {
        Zoo zoo = Zoo.CreateBuilder()
            .AddAnimal(Goat.CreateBuilder().SetName("Goatee"))
            .AddAnimal(Cow.CreateBuilder().SetName("Moomoo"))
            .AddAnimal(Shark.CreateBuilder().SetName("Jaws"))
            .AddAnimal(Salmon.CreateBuilder().SetName("Sashimi"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(zoo, _registry);

        Assert.Equal(
            """
            <Zoo>
              <goat name="Goatee"/>
              <cow name="Moomoo"/>
              <shark name="Jaws"/>
              <salmon name="Sashimi"/>
            </Zoo>
            """.ReplaceLineEndings("\n"),
            xml);
        Assert.Equal(zoo, XmlDocumentReader.Read<Zoo>(xml, _registry, out _));
    }

    [Fact]
    public void Read_ForASubstitutedElementInTheBaseNamespace_ChoosesTheBaseType()
    {
        const string Xml = """
            <AnimalContainer xmlns="urn:my.schema" xmlns:ext="urn:my.extension">
              <llama name="Fluffy">
                <fluffiness>3</fluffiness>
              </llama>
            </AnimalContainer>
            """;

        AnimalContainer actual = XmlDocumentReader.Read<AnimalContainer>(Xml, _registry, out _);

        Llama llama = Assert.IsType<Llama>(actual.Animal);
        Assert.Equal("Fluffy", llama.Name);
        Assert.Equal(3, llama.Fluffiness);
    }

    [Fact]
    public void Read_ForASubstitutedElementInAnExtensionNamespace_ChoosesTheExtensionType()
    {
        const string Xml = """
            <AnimalContainer xmlns="urn:my.schema" xmlns:ext="urn:my.extension">
              <ext:llama name="Fluffy">
                <ext:fluffiness>3</ext:fluffiness>
              </ext:llama>
            </AnimalContainer>
            """;

        AnimalContainer actual = XmlDocumentReader.Read<AnimalContainer>(Xml, _registry, out _);

        ExtensionLlama llama = Assert.IsType<ExtensionLlama>(actual.Animal);
        Assert.Equal("Fluffy", llama.Name);
        Assert.Equal(3, llama.Fluffiness);
    }

    [Fact]
    public void Write_ForATimeValue_AppendsTheTrailingZ()
    {
        TimeContainer container = TimeContainer.CreateBuilder().SetTimeValue(new TimeOnly(1, 23, 45)).Build();

        string xml = XmlDocumentWriter.WriteToString(
            container, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal("<TimeContainer><timeValue>01:23:45Z</timeValue></TimeContainer>", xml);
        Assert.Equal(container, XmlDocumentReader.Read<TimeContainer>(xml, _registry, out _));
    }

    [Fact]
    public void Read_ForATimeValueWithoutATimezone_ReadsItUnchanged()
    {
        TimeContainer actual = XmlDocumentReader.Read<TimeContainer>(
            "<TimeContainer><timeValue>01:23:45</timeValue></TimeContainer>", _registry, out _);

        Assert.Equal(new TimeOnly(1, 23, 45), actual.TimeValue);
    }

    [Fact]
    public void Read_ForATimeValueWithAnOffset_NormalisesItToUtc()
    {
        TimeContainer actual = XmlDocumentReader.Read<TimeContainer>(
            "<TimeContainer><timeValue>03:23:45+02:00</timeValue></TimeContainer>", _registry, out _);

        Assert.Equal(new TimeOnly(1, 23, 45), actual.TimeValue);
    }

    [Fact]
    public void Read_ForUnknownElementsAndAttributes_SkipsThemAndCountsThem()
    {
        const string Xml = """
            <TypeWithTypeElement unexpectedAttribute="x">
              <firstElement>first</firstElement>
              <unexpectedElement><nested>ignored</nested></unexpectedElement>
              <type>My type</type>
            </TypeWithTypeElement>
            """;

        TypeWithTypeElement actual = XmlDocumentReader.Read<TypeWithTypeElement>(Xml, _registry, out XmlReadContext context);

        Assert.Equal("first", actual.FirstElement);
        Assert.Equal("My type", actual.Type);
        Assert.Equal(1, context.UnknownElementCount);
        Assert.Equal(1, context.UnknownAttributeCount);
        Assert.False(context.IsClean);
        Assert.Equal(2, context.Diagnostics.Count);
    }

    [Fact]
    public void Read_ForAnUnknownElementWhenNotLenient_Throws()
    {
        const string Xml = "<TypeWithTypeElement><unexpectedElement>x</unexpectedElement></TypeWithTypeElement>";
        var context = new XmlReadContext(_registry, new XmlReadOptions { Lenient = false });

        Assert.Throws<System.Xml.XmlException>(() => XmlDocumentReader.Read(Xml, context));
    }

    [Fact]
    public void Read_ForAnUnregisteredRootElement_Throws() =>
        Assert.Throws<System.Xml.XmlException>(
            () => XmlDocumentReader.Read("<NoSuchRoot/>", _registry, out _));

    private static TopLevel Document() => TopLevel.CreateBuilder()
        .SetAttr(FooBuilder("My value", "Foo", "Bar"))
        .SetValue(Measure.CreateBuilder().SetValue(1m).SetUnit(UnitEnum.Meter))
        .Build();

    private static Foo.Builder FooBuilder(string xmlValue, params string[] attrs)
    {
        Foo.Builder builder = Foo.CreateBuilder().SetXmlValue(xmlValue);
        foreach (string attr in attrs)
        {
            builder.AddAttr1(attr);
        }
        return builder;
    }

    private static readonly string ExpectedDocument =
        """
        <TopLevel xmlns="urn:my.schema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="urn:my.schema ../schema/schema.xsd">
          <Attr>
            <XMLValue>My value</XMLValue>
            <Attr1>Foo</Attr1>
            <Attr1>Bar</Attr1>
          </Attr>
          <Value Unit="Meter">1</Value>
        </TopLevel>
        """.ReplaceLineEndings("\n");

    private static readonly string ExpectedExtendedDocument =
        """
        <TopLevelExtension xmlns="urn:my.schema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="urn:my.schema ../schema/schema.xsd">
          <Attr>
            <XMLValue>My value</XMLValue>
            <Attr1>Foo</Attr1>
            <Attr1>Bar</Attr1>
          </Attr>
          <Value Unit="Meter">1</Value>
          <DocumentExtensionAttr>Document Extension Attribute Value</DocumentExtensionAttr>
        </TopLevelExtension>
        """.ReplaceLineEndings("\n");
}

internal static class BuilderExtensions
{
    /// <summary>Applies <paramref name="action"/> to a builder inline, keeping test setup fluent.</summary>
    internal static T Also<T>(this T builder, Action<T> action)
    {
        action(builder);
        return builder;
    }
}
