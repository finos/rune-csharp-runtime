using System.Xml;
using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// The reader and writer policies the generated codecs rely on: absent values are omitted,
/// attributes precede child elements, constant attributes are written where they are declared,
/// XML infrastructure attributes are never fed back to a codec, and the empty-element form is
/// deterministic.
/// </summary>
public class XmlReadWriteBehaviourTests
{
    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Fact]
    public void Write_ForAnAbsentProperty_OmitsItsElementEntirely()
    {
        TopLevel value = TopLevel.CreateBuilder()
            .SetValue(Measure.CreateBuilder().SetValue(2m).SetUnit(UnitEnum.Kilogram))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry, new XmlWriteOptions { Indent = false });

        Assert.DoesNotContain("<Attr", xml, StringComparison.Ordinal);
        Assert.Contains("<Value Unit=\"Kilogram\">2</Value>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ForAnEmptyList_OmitsEveryElementAndAnyWrapper()
    {
        MulticardinalityContainer value = MulticardinalityContainer.CreateBuilder().Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal("<MulticardinalityContainer/>", xml);
    }

    [Fact]
    public void Write_ForAnAbsentAttribute_OmitsIt()
    {
        Zoo value = Zoo.CreateBuilder().AddAnimal(Goat.CreateBuilder()).Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal("<Zoo><goat/></Zoo>", xml);
    }

    [Fact]
    public void Write_ForAValueCarryingBothAnAttributeAndText_WritesTheAttributeFirst()
    {
        Measure value = Measure.CreateBuilder().SetValue(1.50m).SetUnit(UnitEnum.Meter).Build();
        TopLevel document = TopLevel.CreateBuilder().SetValue(value.ToBuilder()).Build();

        string xml = XmlDocumentWriter.WriteToString(document, _registry, new XmlWriteOptions { Indent = false });

        Assert.Contains("<Value Unit=\"Meter\">1.50</Value>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WithCompactEmptyElementsDisabled_KeepsTheDotNetSpacedForm()
    {
        Zoo value = Zoo.CreateBuilder().AddAnimal(Goat.CreateBuilder().SetName("Goatee")).Build();

        string xml = XmlDocumentWriter.WriteToString(
            value, _registry, new XmlWriteOptions { Indent = false, CompactEmptyElements = false });

        Assert.Equal("<Zoo><goat name=\"Goatee\" /></Zoo>", xml);
    }

    [Fact]
    public void Write_ForAnAttributeValueContainingMarkup_EscapesItSoTheCompactionStaysUnambiguous()
    {
        Zoo value = Zoo.CreateBuilder().AddAnimal(Goat.CreateBuilder().SetName("a /> b")).Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal("<Zoo><goat name=\"a /&gt; b\"/></Zoo>", xml);
        Assert.Equal(value, XmlDocumentReader.Read<Zoo>(xml, _registry, out _));
    }

    [Fact]
    public void Write_WithACustomIndent_UsesItAndAlwaysNewlinesWithNewlineOnly()
    {
        MulticardinalityContainer value = MulticardinalityContainer.CreateBuilder()
            .Also(b => b.AddFoo().SetXmlValue("v"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(
            value, _registry, new XmlWriteOptions { IndentChars = "\t" });

        Assert.Equal(
            "<MulticardinalityContainer>\n\t<foo>\n\t\t<XMLValue>v</XMLValue>\n\t</foo>\n</MulticardinalityContainer>",
            xml);
        Assert.DoesNotContain("\r", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ForADocumentWithADeclarationAndComments_IgnoresThem()
    {
        const string Xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- a leading comment -->
            <TypeWithTypeElement>
              <!-- an inner comment -->
              <firstElement>first</firstElement>
              <type>My type</type>
            </TypeWithTypeElement>
            """;

        TypeWithTypeElement actual = XmlDocumentReader.Read<TypeWithTypeElement>(Xml, _registry, out XmlReadContext context);

        Assert.Equal("first", actual.FirstElement);
        Assert.Equal("My type", actual.Type);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void Read_ForNamespaceAndSchemaLocationAttributes_NeverOffersThemToACodec()
    {
        const string Xml = """
            <TypeWithTypeElement xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="urn:x x.xsd">
              <firstElement>first</firstElement>
            </TypeWithTypeElement>
            """;

        XmlDocumentReader.Read<TypeWithTypeElement>(Xml, _registry, out XmlReadContext context);

        Assert.Equal(0, context.UnknownAttributeCount);
        Assert.True(context.IsClean);
    }

    [Theory]
    [InlineData("<FpmlTextValue/>")]
    [InlineData("<FpmlTextValue></FpmlTextValue>")]
    public void ReadAs_ForAnEmptyValueElement_PreservesTheEmptyString(string xml)
    {
        FpmlTextValue actual = XmlDocumentReader.ReadAs<FpmlTextValue>(
            xml, FpmlTextValueCodec.Instance, _registry, out XmlReadContext context);

        Assert.Equal(string.Empty, actual.Value);
        Assert.True(context.IsClean);
    }

    [Theory]
    [InlineData("<FpmlTextValue scheme=\"x\"/>")]
    [InlineData("<FpmlTextValue scheme=\"x\"></FpmlTextValue>")]
    public void ReadAs_ForAnEmptyValueElementWithAnAttribute_LeavesTheValueAbsent(string xml)
    {
        FpmlTextValue actual = XmlDocumentReader.ReadAs<FpmlTextValue>(
            xml, FpmlTextValueCodec.Instance, _registry, out XmlReadContext context);

        Assert.Equal("x", actual.Scheme);
        Assert.Null(actual.Value);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void ReadAs_ForAnExplicitRootCodec_IgnoresTheRegisteredRootName()
    {
        var context = new XmlReadContext(_registry);

        var actual = (Foo)XmlDocumentReader.ReadAs(
            "<AnyNameAtAll><XMLValue>v</XMLValue></AnyNameAtAll>", FooCodec.Instance, context);

        Assert.Equal("v", actual.XmlValue);
    }

    [Fact]
    public void ReadAs_ForARootWithASubstitutedChild_KeepsTheConcreteChildType()
    {
        const string Xml = """
            <AnimalContainer>
              <llama xmlns="urn:my.extension" name="Fluffy">
                <fluffiness>3</fluffiness>
              </llama>
            </AnimalContainer>
            """;

        AnimalContainer actual = XmlDocumentReader.ReadAs<AnimalContainer>(
            Xml, AnimalContainerCodec.Instance, _registry, out XmlReadContext context);

        ExtensionLlama llama = Assert.IsType<ExtensionLlama>(actual.Animal);
        Assert.Equal("Fluffy", llama.Name);
        Assert.Equal(3, llama.Fluffiness);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void ReadAs_WithARegistryHoldingOnlyTheRootCodec_DropsTheSubstitutedChild()
    {
        XmlCodecRegistry rootOnly = new XmlCodecRegistry().Register(AnimalContainerCodec.Instance);
        const string Xml = "<AnimalContainer><goat name=\"Goatee\"/></AnimalContainer>";

        AnimalContainer actual = XmlDocumentReader.ReadAs<AnimalContainer>(
            Xml, AnimalContainerCodec.Instance, rootOnly, out XmlReadContext context);

        Assert.Null(actual.Animal);
        Assert.Equal(1, context.UnknownElementCount);
        Assert.False(context.IsClean);
    }

    [Fact]
    public void FindSubstitute_ForAnUnqualifiedElement_FollowsRegistrationOrderNotHashOrder()
    {
        XmlCodecRegistry baseFirst = new XmlCodecRegistry().RegisterAll(
            AnimalContainerCodec.Instance, LlamaCodec.Instance, ExtensionLlamaCodec.Instance);
        XmlCodecRegistry extensionFirst = new XmlCodecRegistry().RegisterAll(
            AnimalContainerCodec.Instance, ExtensionLlamaCodec.Instance, LlamaCodec.Instance);
        var unqualified = new XmlQName("llama");

        Assert.Same(LlamaCodec.Instance, baseFirst.FindSubstitute(TestCodecs.AnimalGroup, unqualified));
        Assert.Same(ExtensionLlamaCodec.Instance, extensionFirst.FindSubstitute(TestCodecs.AnimalGroup, unqualified));

        Assert.Same(
            ExtensionLlamaCodec.Instance,
            baseFirst.FindSubstitute(TestCodecs.AnimalGroup, new XmlQName("urn:my.extension", "llama")));
        Assert.Same(
            LlamaCodec.Instance,
            extensionFirst.FindSubstitute(TestCodecs.AnimalGroup, new XmlQName("urn:my.schema", "llama")));
    }

    [Fact]
    public void Register_ForAnAlreadyRegisteredElementName_ReplacesInPlaceAndKeepsTheOrder()
    {
        XmlCodecRegistry registry = new XmlCodecRegistry().RegisterAll(
            GoatCodec.Instance, LlamaCodec.Instance, CowCodec.Instance);

        registry.Register(LlamaCodec.Instance);

        Assert.Equal(
            new[] { "com.rosetta.test.Goat", "com.rosetta.test.Llama", "com.rosetta.test.Cow" },
            registry.SubstitutionMembers(TestCodecs.AnimalGroup).Select(m => m.Descriptor.TypeName));
    }

    [Fact]
    public void Read_ForAnEmptyDocument_Throws() =>
        Assert.Throws<XmlException>(() => XmlDocumentReader.Read("<?xml version=\"1.0\"?>", _registry, out _));

    [Fact]
    public void Write_ForAnUnregisteredValueType_Throws()
    {
        var registry = new XmlCodecRegistry();

        Assert.Throws<InvalidOperationException>(
            () => XmlDocumentWriter.WriteToString(Goat.CreateBuilder().Build(), registry));
    }

    [Fact]
    public void Write_ForAConstantAttributeOnATypeAndOnAProperty_EmitsBothBeforeAnyContent()
    {
        XmlCodecRegistry registry = TestCodecs.CreateRegistry().Register(ConstantAttributeCodec.Instance);
        TypeWithTypeElement value = TypeWithTypeElement.CreateBuilder()
            .SetFirstElement("first")
            .SetType("My type")
            .Build();

        string xml = XmlDocumentWriter.WriteToString(
            value, ConstantAttributeCodec.Instance, registry, new XmlWriteOptions { Indent = false });

        Assert.Equal(
            "<ConstantAttributeContainer version=\"1-0\">"
            + "<firstElement>first</firstElement>"
            + "<type kind=\"literal\">My type</type>"
            + "</ConstantAttributeContainer>",
            xml);
    }

    /// <summary>
    /// A codec over the same model as <see cref="TypeWithTypeElementCodec"/>, but declaring a
    /// constant attribute on the type and another on one of its elements.
    /// </summary>
    private sealed class ConstantAttributeCodec : XmlTypeCodec<TypeWithTypeElement, TypeWithTypeElement.Builder>
    {
        internal static readonly ConstantAttributeCodec Instance = new ConstantAttributeCodec();

        private static readonly XmlQName FirstElementName = new XmlQName("firstElement");
        private static readonly XmlQName TypeName = new XmlQName("type");

        private static readonly IReadOnlyList<KeyValuePair<string, string>> TypeElementAttributes =
            new[] { new KeyValuePair<string, string>("kind", "literal") };

        private ConstantAttributeCodec()
            : base(new XmlTypeDescriptor(
                "com.rosetta.test.ConstantAttributeContainer",
                new[]
                {
                    new XmlPropertyDescriptor("firstElement", FirstElementName),
                    new XmlPropertyDescriptor(
                        "type", TypeName, constantAttributes: TypeElementAttributes),
                },
                elementName: new XmlQName("ConstantAttributeContainer"),
                constantAttributes: new[] { new KeyValuePair<string, string>("version", "1-0") }))
        {
        }

        public override TypeWithTypeElement.Builder CreateTypedBuilder() => TypeWithTypeElement.CreateBuilder();

        protected override IReadOnlyList<string> PresentProperties(TypeWithTypeElement value)
        {
            var present = new List<string>();
            if (value.FirstElement != null)
            {
                present.Add("firstElement");
            }
            if (value.Type != null)
            {
                present.Add("type");
            }
            return present;
        }

        protected override void WriteProperty(
            TypeWithTypeElement value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
        {
            switch (propertyName)
            {
                case "firstElement":
                    writer.WriteTextElement(FirstElementName, value.FirstElement);
                    break;
                case "type":
                    writer.WriteTextElement(TypeName, value.Type, TypeElementAttributes);
                    break;
            }
        }
    }
}
