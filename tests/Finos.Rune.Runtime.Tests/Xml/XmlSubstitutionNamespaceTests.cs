using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// How a substitution-group member's element name is qualified on write, and the one place this
/// deliberately diverges from rune-common 12.3.1.
/// </summary>
/// <remarks>
/// <para><b>What Java does.</b> <c>SubstitutingBeanPropertyWriter.serializeAsField</c> writes
/// <c>SubstitutionMap.getSubstitutedName(value)</c> as the field name, and
/// <c>getSubstitutedName</c> returns <c>XMLFullyQualifiedName.getName()</c> — the local name, never
/// the namespace. Verified in the 12.3.1 source, and unchanged in 12.5.1.</para>
/// <para><b>Why that is lossy.</b> A group whose members span namespaces can hold two element names
/// with the same local name — <c>urn:my.schema/llama</c> and <c>urn:my.extension/llama</c> in
/// rune-common's own test schema. Java writes both as <c>&lt;llama&gt;</c>, and reading that back
/// can only guess. rune-common never exercises this: its extension cases are read-only, from
/// hand-authored input files, so no Java golden constrains the output.</para>
/// <para><b>The divergence.</b> Under the default
/// <see cref="XmlSubstitutionNamespacePolicy.WhenAmbiguous"/> the writer keeps the namespace only
/// when the local name does not identify exactly one member. Every Java golden fixture — goat, cow,
/// shark, salmon — has a unique local name and is therefore byte-identical; only the ambiguous case
/// changes, and only where Java is provably lossy.
/// <see cref="XmlSubstitutionNamespacePolicy.Never"/> restores exact Java behaviour, loss included.
/// </para>
/// </remarks>
public class XmlSubstitutionNamespaceTests
{
    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Fact]
    public void IsSubstitutionLocalNameAmbiguous_ForALocalNameSharedAcrossNamespaces_IsTrue()
    {
        Assert.True(_registry.IsSubstitutionLocalNameAmbiguous(TestCodecs.AnimalGroup, "llama"));
        Assert.False(_registry.IsSubstitutionLocalNameAmbiguous(TestCodecs.AnimalGroup, "goat"));
        Assert.False(_registry.IsSubstitutionLocalNameAmbiguous(TestCodecs.AnimalGroup, "nosuchname"));
    }

    [Fact]
    public void SubstitutedElementName_ForAnUnambiguousMember_StaysABareLocalNameUnderEveryPolicyButAlways()
    {
        var goat = (Goat)Goat.CreateBuilder().SetName("Goatee").Build();

        Assert.Equal(new XmlQName("goat"), _registry.SubstitutedElementName(goat));
        Assert.Equal(
            new XmlQName("goat"),
            _registry.SubstitutedElementName(goat, XmlSubstitutionNamespacePolicy.Never));
        Assert.Equal(
            new XmlQName("urn:my.schema", "goat"),
            _registry.SubstitutedElementName(goat, XmlSubstitutionNamespacePolicy.Always));
    }

    [Fact]
    public void SubstitutedElementName_ForAnAmbiguousMember_KeepsItsNamespaceByDefault()
    {
        ExtensionLlama llama = ExtensionLlama.CreateBuilder().SetFluffiness(1).Build();

        Assert.Equal(new XmlQName("urn:my.extension", "llama"), _registry.SubstitutedElementName(llama));
        Assert.Equal(
            new XmlQName("llama"),
            _registry.SubstitutedElementName(llama, XmlSubstitutionNamespacePolicy.Never));
    }

    [Fact]
    public void Write_ForAnExtensionNamespaceMember_RoundTripsBackToTheExtensionType()
    {
        AnimalContainer container = AnimalContainer.CreateBuilder()
            .SetAnimal(ExtensionLlama.CreateBuilder().SetFluffiness(3).SetName("Fluffy"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(container, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal(
            "<AnimalContainer>"
            + "<llama xmlns=\"urn:my.extension\" name=\"Fluffy\"><fluffiness>3</fluffiness></llama>"
            + "</AnimalContainer>",
            xml);
        AnimalContainer read = XmlDocumentReader.Read<AnimalContainer>(xml, _registry, out _);
        Assert.IsType<ExtensionLlama>(read.Animal);
        Assert.Equal(container, read);
    }

    [Fact]
    public void Write_ForABaseNamespaceMemberWithAnAmbiguousLocalName_RoundTripsBackToTheBaseType()
    {
        AnimalContainer container = AnimalContainer.CreateBuilder()
            .SetAnimal(Llama.CreateBuilder().SetFluffiness(3).SetName("Fluffy"))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(container, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal(
            "<AnimalContainer>"
            + "<llama xmlns=\"urn:my.schema\" name=\"Fluffy\"><fluffiness>3</fluffiness></llama>"
            + "</AnimalContainer>",
            xml);
        AnimalContainer read = XmlDocumentReader.Read<AnimalContainer>(xml, _registry, out _);
        Assert.IsType<Llama>(read.Animal);
        Assert.Equal(container, read);
    }

    [Fact]
    public void Write_ForAMixedNamespaceSubstitutionList_QualifiesOnlyTheAmbiguousItems()
    {
        Zoo zoo = MixedZoo();

        string xml = XmlDocumentWriter.WriteToString(zoo, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal(
            "<Zoo>"
            + "<goat name=\"Goatee\"/>"
            + "<llama xmlns=\"urn:my.extension\" name=\"Ext\"><fluffiness>1</fluffiness></llama>"
            + "<llama xmlns=\"urn:my.schema\" name=\"Base\"><fluffiness>2</fluffiness></llama>"
            + "<cow name=\"Moomoo\"/>"
            + "</Zoo>",
            xml);
    }

    [Fact]
    public void Read_ForAMixedNamespaceSubstitutionList_RestoresEveryConcreteType()
    {
        Zoo zoo = MixedZoo();

        string xml = XmlDocumentWriter.WriteToString(zoo, _registry);
        Zoo read = XmlDocumentReader.Read<Zoo>(xml, _registry, out XmlReadContext context);

        Assert.Equal(
            new[] { typeof(Goat), typeof(ExtensionLlama), typeof(Llama), typeof(Cow) },
            read.Animal!.Select(a => a.GetType()));
        Assert.Equal(zoo, read);
        Assert.True(context.IsClean);
    }

    [Fact]
    public void Read_ForAnIndentedMixedNamespaceList_IsUnaffectedByThePrettyPrinting()
    {
        string xml = XmlDocumentWriter.WriteToString(MixedZoo(), _registry);

        Assert.Equal(
            """
            <Zoo>
              <goat name="Goatee"/>
              <llama xmlns="urn:my.extension" name="Ext">
                <fluffiness>1</fluffiness>
              </llama>
              <llama xmlns="urn:my.schema" name="Base">
                <fluffiness>2</fluffiness>
              </llama>
              <cow name="Moomoo"/>
            </Zoo>
            """.ReplaceLineEndings("\n"),
            xml);
    }

    [Fact]
    public void Write_WithTheNeverPolicy_ReproducesJavaExactlyIncludingItsLoss()
    {
        Zoo zoo = MixedZoo();
        var javaExact = new XmlWriteOptions
        {
            Indent = false,
            SubstitutionNamespace = XmlSubstitutionNamespacePolicy.Never,
        };

        string xml = XmlDocumentWriter.WriteToString(zoo, _registry, javaExact);

        Assert.Equal(
            "<Zoo>"
            + "<goat name=\"Goatee\"/>"
            + "<llama name=\"Ext\"><fluffiness>1</fluffiness></llama>"
            + "<llama name=\"Base\"><fluffiness>2</fluffiness></llama>"
            + "<cow name=\"Moomoo\"/>"
            + "</Zoo>",
            xml);

        Zoo read = XmlDocumentReader.Read<Zoo>(xml, _registry, out _);
        Assert.Equal(typeof(Goat), read.Animal![0].GetType());
        Assert.Equal(typeof(Cow), read.Animal[3].GetType());
        Assert.Equal(read.Animal[1].GetType(), read.Animal[2].GetType());
        Assert.NotEqual(zoo, read);
    }

    [Fact]
    public void Write_ForTheJavaGoldenSubstitutionFixtures_IsUnchangedByTheDefaultPolicy()
    {
        Zoo zoo = Zoo.CreateBuilder()
            .AddAnimal(Goat.CreateBuilder().SetName("Goatee"))
            .AddAnimal(Cow.CreateBuilder().SetName("Moomoo"))
            .AddAnimal(Shark.CreateBuilder().SetName("Jaws"))
            .AddAnimal(Salmon.CreateBuilder().SetName("Sashimi"))
            .Build();

        string withDefault = XmlDocumentWriter.WriteToString(zoo, _registry);
        string javaExact = XmlDocumentWriter.WriteToString(
            zoo, _registry, new XmlWriteOptions { SubstitutionNamespace = XmlSubstitutionNamespacePolicy.Never });

        Assert.Equal(javaExact, withDefault);
        Assert.DoesNotContain("xmlns", withDefault, StringComparison.Ordinal);
    }

    private static Zoo MixedZoo() => Zoo.CreateBuilder()
        .AddAnimal(Goat.CreateBuilder().SetName("Goatee"))
        .AddAnimal(ExtensionLlama.CreateBuilder().SetFluffiness(1).SetName("Ext"))
        .AddAnimal(Llama.CreateBuilder().SetFluffiness(2).SetName("Base"))
        .AddAnimal(Cow.CreateBuilder().SetName("Moomoo"))
        .Build();
}
