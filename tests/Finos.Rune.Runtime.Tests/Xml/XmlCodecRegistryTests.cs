using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// The dispatch table: qualified-name parsing, root resolution, substitution-group resolution and
/// the descriptor contract generated codecs are built from.
/// </summary>
public class XmlCodecRegistryTests
{
    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Fact]
    public void Parse_ForAFullyQualifiedName_SplitsOnTheLastSlash()
    {
        XmlQName name = XmlQName.Parse("urn:my.schema/animal");

        Assert.Equal("urn:my.schema", name.Namespace);
        Assert.Equal("animal", name.LocalName);
        Assert.Equal("urn:my.schema/animal", name.ToString());
    }

    [Fact]
    public void Parse_ForANameWithNoSlash_IsABareLocalName()
    {
        XmlQName name = XmlQName.Parse("animal");

        Assert.Null(name.Namespace);
        Assert.False(name.HasNamespace);
        Assert.Equal("animal", name.LocalName);
    }

    [Fact]
    public void Equals_ForNamesDifferingOnlyByNamespace_IsFalse()
    {
        var baseName = new XmlQName("urn:my.schema", "llama");
        var extensionName = new XmlQName("urn:my.extension", "llama");

        Assert.NotEqual(baseName, extensionName);
        Assert.True(baseName.MatchesLoosely(extensionName.WithoutNamespace()));
        Assert.False(baseName.MatchesLoosely(extensionName));
    }

    [Fact]
    public void FindRoot_ForAnExactlyQualifiedRootElement_ResolvesIt() =>
        Assert.Same(
            TopLevelCodec.Instance, _registry.FindRoot(new XmlQName("urn:my.schema", "TopLevel")));

    [Fact]
    public void FindRoot_ForARootElementWithNoNamespace_FallsBackToTheLocalName() =>
        Assert.Same(TopLevelCodec.Instance, _registry.FindRoot(new XmlQName("TopLevel")));

    [Fact]
    public void FindRoot_ForAnUnregisteredElement_ReturnsNull() =>
        Assert.Null(_registry.FindRoot(new XmlQName("NoSuchElement")));

    [Fact]
    public void FindSubstitute_ForAnExactlyQualifiedElement_PrefersTheMatchingNamespace()
    {
        IXmlTypeCodec? baseLlama = _registry.FindSubstitute(
            TestCodecs.AnimalGroup, new XmlQName("urn:my.schema", "llama"));
        IXmlTypeCodec? extensionLlama = _registry.FindSubstitute(
            TestCodecs.AnimalGroup, new XmlQName("urn:my.extension", "llama"));

        Assert.Same(LlamaCodec.Instance, baseLlama);
        Assert.Same(ExtensionLlamaCodec.Instance, extensionLlama);
    }

    [Fact]
    public void FindSubstitute_ForAnUnqualifiedElement_FallsBackToTheFirstRegisteredLocalName() =>
        Assert.Same(LlamaCodec.Instance, _registry.FindSubstitute(TestCodecs.AnimalGroup, new XmlQName("llama")));

    [Fact]
    public void FindSubstitute_ForAnElementOutsideTheGroup_ReturnsNull() =>
        Assert.Null(_registry.FindSubstitute(TestCodecs.AnimalGroup, new XmlQName("penguin")));

    [Fact]
    public void FindSubstitute_ThroughAnAbstractNestedHead_ResolvesTheConcreteDescendant()
    {
        XmlCodecRegistry registry = TransitiveRegistry();

        IXmlTypeCodec? result = registry.FindSubstitute(
            TestCodecs.AnimalGroup, new XmlQName("urn:my.schema", "salmon"));

        Assert.Same(TransitiveSalmonCodec.Instance, result);
    }

    [Fact]
    public void FindSubstitute_ForTheAbstractNestedHead_DoesNotReturnAnInstantiableCodec()
    {
        XmlCodecRegistry registry = TransitiveRegistry();

        IXmlTypeCodec? result = registry.FindSubstitute(
            TestCodecs.AnimalGroup, new XmlQName("urn:my.schema", "fish"));

        Assert.Null(result);
    }

    [Fact]
    public void FindElementReference_ForADirectDeclaredElement_ResolvesItsCodec()
    {
        XmlCodecRegistry registry = TransitiveRegistry();

        IXmlTypeCodec? result = registry.FindElementReference(
            new XmlQName("urn:my.schema", "goat"), new XmlQName("urn:my.schema", "goat"));

        Assert.Same(GoatCodec.Instance, result);
    }

    [Fact]
    public void FindElementReference_ForAHeadWithTransitiveMembers_ResolvesTheConcreteDescendant()
    {
        XmlCodecRegistry registry = TransitiveRegistry();

        IXmlTypeCodec? result = registry.FindElementReference(
            TestCodecs.AnimalGroup, new XmlQName("urn:my.schema", "salmon"));

        Assert.Same(TransitiveSalmonCodec.Instance, result);
    }

    [Fact]
    public void SubstitutionMembers_ForTheAnimalGroup_ListsEveryRegisteredMemberInOrder()
    {
        IReadOnlyList<IXmlTypeCodec> members = _registry.SubstitutionMembers(TestCodecs.AnimalGroup);

        Assert.Equal(
            new[]
            {
                "com.rosetta.test.Goat", "com.rosetta.test.Cow", "com.rosetta.test.Shark",
                "com.rosetta.test.Salmon", "com.rosetta.test.Llama", "com.rosetta.extension.test.Llama",
            },
            members.Select(m => m.Descriptor.TypeName));
    }

    [Fact]
    public void SubstitutedElementName_ForAConcreteValue_IsTheLocalNameOnly()
    {
        XmlQName? name = _registry.SubstitutedElementName(Goat.CreateBuilder().SetName("Goatee").Build());

        Assert.Equal(new XmlQName("goat"), name);
    }

    [Fact]
    public void FindByTypeName_ForARegisteredCodec_ResolvesIt() =>
        Assert.Same(FooCodec.Instance, _registry.FindByTypeName("com.rosetta.test.Foo"));

    [Fact]
    public void Descriptor_ForATypeWithAValueProperty_ExposesIt()
    {
        XmlTypeDescriptor descriptor = MeasureCodec.Instance.Descriptor;

        Assert.Equal("value", descriptor.ValueProperty!.PropertyName);
        Assert.Equal(XmlPropertyRepresentation.Attribute, descriptor.FindProperty("unit")!.Representation);
        Assert.Null(descriptor.FindProperty("nothing"));
    }

    [Fact]
    public void Descriptor_ForASubstitutionMember_CarriesItsGroupAndElementName()
    {
        XmlSubstitutionEntry substitution = GoatCodec.Instance.Descriptor.Substitution!;

        Assert.Equal(TestCodecs.AnimalGroup, substitution.Group);
        Assert.Equal(new XmlQName("urn:my.schema", "goat"), substitution.ElementName);
        Assert.False(substitution.IsAbstract);
    }

    [Fact]
    public void Descriptor_ForARootType_CarriesItsConstantAttributesInOrder() =>
        Assert.Equal(
            new[] { "xmlns", "xmlns:xsi" },
            TopLevelCodec.Instance.Descriptor.ConstantAttributes.Select(a => a.Key));

    [Fact]
    public void Format_ForAnEnumWithAnAlias_WritesTheAlias()
    {
        var codec = new XmlEnumCodec<UnitEnum>(new Dictionary<UnitEnum, string>
        {
            [UnitEnum.Meter] = "Meter",
            [UnitEnum.Kilogram] = "Kilogram",
        });

        Assert.Equal("Kilogram", codec.Format(UnitEnum.Kilogram));
        Assert.Equal(UnitEnum.Meter, codec.Parse("Meter"));
        Assert.Null(codec.Format((UnitEnum?)null));
        Assert.False(codec.TryParse("Furlong", out _));
        Assert.Throws<FormatException>(() => codec.Parse("Furlong"));
    }

    private static XmlCodecRegistry TransitiveRegistry() => new XmlCodecRegistry().RegisterAll(
        GoatCodec.Instance,
        AbstractFishCodec.Instance,
        TransitiveSalmonCodec.Instance);

    private sealed class AbstractFishCodec : XmlTypeCodec<Shark, Shark.Builder>
    {
        private static readonly XmlQName Element = new("urn:my.schema", "fish");

        public static readonly AbstractFishCodec Instance = new();

        private AbstractFishCodec()
            : base(new XmlTypeDescriptor(
                "com.rosetta.test.Fish",
                elementName: Element,
                isAbstract: true,
                substitution: new XmlSubstitutionEntry(TestCodecs.AnimalGroup, Element, isAbstract: true)))
        {
        }

        public override Shark.Builder CreateTypedBuilder() => Shark.CreateBuilder();
    }

    private sealed class TransitiveSalmonCodec : XmlTypeCodec<Salmon, Salmon.Builder>
    {
        private static readonly XmlQName FishGroup = new("urn:my.schema", "fish");
        private static readonly XmlQName Element = new("urn:my.schema", "salmon");

        public static readonly TransitiveSalmonCodec Instance = new();

        private TransitiveSalmonCodec()
            : base(new XmlTypeDescriptor(
                "com.rosetta.test.Salmon",
                elementName: Element,
                substitution: new XmlSubstitutionEntry(FishGroup, Element)))
        {
        }

        public override Salmon.Builder CreateTypedBuilder() => Salmon.CreateBuilder();
    }
}
