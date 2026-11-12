using System.Xml;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;

namespace Finos.Rune.Runtime.Tests.Xml.Codecs;

/// <summary>Base for the animals, which differ only in their element name.</summary>
public abstract class AnimalCodecBase<TValue, TBuilder> : XmlTypeCodec<TValue, TBuilder>
    where TValue : Animal
    where TBuilder : AnimalBuilder
{
    protected AnimalCodecBase(string typeName, XmlQName elementName, XmlQName group)
        : base(new XmlTypeDescriptor(
            typeName,
            new[]
            {
                new XmlPropertyDescriptor("name", new XmlQName("name"), XmlPropertyRepresentation.Attribute),
            },
            elementName: elementName,
            substitution: new XmlSubstitutionEntry(group, elementName)))
    {
    }

    protected override bool TryReadAttribute(TBuilder builder, XmlQName name, string text, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "name", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetName(text);
        return true;
    }

    protected override void WriteAttributes(TValue value, XmlObjectWriter writer, XmlWriteContext context) =>
        writer.WriteAttribute("name", value.Name);
}

public sealed class GoatCodec : AnimalCodecBase<Goat, Goat.Builder>
{
    public static readonly GoatCodec Instance = new GoatCodec();

    private GoatCodec()
        : base("com.rosetta.test.Goat", new XmlQName("urn:my.schema", "goat"), TestCodecs.AnimalGroup)
    {
    }

    public override Goat.Builder CreateTypedBuilder() => Goat.CreateBuilder();
}

public sealed class CowCodec : AnimalCodecBase<Cow, Cow.Builder>
{
    public static readonly CowCodec Instance = new CowCodec();

    private CowCodec()
        : base("com.rosetta.test.Cow", new XmlQName("urn:my.schema", "cow"), TestCodecs.AnimalGroup)
    {
    }

    public override Cow.Builder CreateTypedBuilder() => Cow.CreateBuilder();
}

public sealed class SharkCodec : AnimalCodecBase<Shark, Shark.Builder>
{
    public static readonly SharkCodec Instance = new SharkCodec();

    private SharkCodec()
        : base("com.rosetta.test.Shark", new XmlQName("urn:my.schema", "shark"), TestCodecs.AnimalGroup)
    {
    }

    public override Shark.Builder CreateTypedBuilder() => Shark.CreateBuilder();
}

public sealed class SalmonCodec : AnimalCodecBase<Salmon, Salmon.Builder>
{
    public static readonly SalmonCodec Instance = new SalmonCodec();

    private SalmonCodec()
        : base("com.rosetta.test.Salmon", new XmlQName("urn:my.schema", "salmon"), TestCodecs.AnimalGroup)
    {
    }

    public override Salmon.Builder CreateTypedBuilder() => Salmon.CreateBuilder();
}

public sealed class LlamaCodec : AnimalCodecBase<Llama, Llama.Builder>
{
    public static readonly LlamaCodec Instance = new LlamaCodec();

    private static readonly XmlQName FluffinessName = new XmlQName("fluffiness");

    private LlamaCodec()
        : base("com.rosetta.test.Llama", new XmlQName("urn:my.schema", "llama"), TestCodecs.AnimalGroup)
    {
    }

    public override Llama.Builder CreateTypedBuilder() => Llama.CreateBuilder();

    protected override bool TryReadElement(
        Llama.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "fluffiness", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetFluffiness(XmlLexicalValue.ParseInt(context.ReadText(reader)));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(Llama value) =>
        value.Fluffiness != null ? new[] { "fluffiness" } : Array.Empty<string>();

    protected override void WriteProperty(
        Llama value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (string.Equals(propertyName, "fluffiness", StringComparison.Ordinal) && value.Fluffiness != null)
        {
            writer.WriteTextElement(FluffinessName, XmlLexicalValue.FormatInt(value.Fluffiness.Value));
        }
    }
}

public sealed class ExtensionLlamaCodec : AnimalCodecBase<ExtensionLlama, ExtensionLlama.Builder>
{
    public static readonly ExtensionLlamaCodec Instance = new ExtensionLlamaCodec();

    private static readonly XmlQName FluffinessName = new XmlQName("fluffiness");

    private ExtensionLlamaCodec()
        : base(
            "com.rosetta.extension.test.Llama",
            new XmlQName("urn:my.extension", "llama"),
            TestCodecs.AnimalGroup)
    {
    }

    public override ExtensionLlama.Builder CreateTypedBuilder() => ExtensionLlama.CreateBuilder();

    protected override bool TryReadElement(
        ExtensionLlama.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "fluffiness", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetFluffiness(XmlLexicalValue.ParseInt(context.ReadText(reader)));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(ExtensionLlama value) =>
        value.Fluffiness != null ? new[] { "fluffiness" } : Array.Empty<string>();

    protected override void WriteProperty(
        ExtensionLlama value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (string.Equals(propertyName, "fluffiness", StringComparison.Ordinal) && value.Fluffiness != null)
        {
            writer.WriteTextElement(FluffinessName, XmlLexicalValue.FormatInt(value.Fluffiness.Value));
        }
    }
}

public sealed class AnimalContainerCodec : XmlTypeCodec<AnimalContainer, AnimalContainer.Builder>
{
    public static readonly AnimalContainerCodec Instance = new AnimalContainerCodec();

    private AnimalContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.AnimalContainer",
            new[]
            {
                new XmlPropertyDescriptor(
                    "animal", new XmlQName("animal"), elementRef: TestCodecs.AnimalGroup),
            },
            elementName: new XmlQName("AnimalContainer")))
    {
    }

    public override AnimalContainer.Builder CreateTypedBuilder() => AnimalContainer.CreateBuilder();

    protected override bool TryReadElement(
        AnimalContainer.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        RosettaModelObjectBuilder? animal = context.ReadSubstituted(reader, TestCodecs.AnimalGroup, Descriptor.TypeName);
        if (animal is not AnimalBuilder typed)
        {
            return animal == null;
        }
        builder.SetAnimal(typed);
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(AnimalContainer value) =>
        value.Animal != null ? new[] { "animal" } : Array.Empty<string>();

    protected override void WriteProperty(
        AnimalContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (string.Equals(propertyName, "animal", StringComparison.Ordinal))
        {
            writer.WriteSubstitutedElement(value.Animal);
        }
    }
}

public sealed class ZooCodec : XmlTypeCodec<Zoo, Zoo.Builder>
{
    public static readonly ZooCodec Instance = new ZooCodec();

    private ZooCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.Zoo",
            new[]
            {
                new XmlPropertyDescriptor(
                    "animal", new XmlQName("animal"), isMulti: true, elementRef: TestCodecs.AnimalGroup),
            },
            elementName: new XmlQName("Zoo")))
    {
    }

    public override Zoo.Builder CreateTypedBuilder() => Zoo.CreateBuilder();

    protected override bool TryReadElement(
        Zoo.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        RosettaModelObjectBuilder? animal = context.ReadSubstituted(reader, TestCodecs.AnimalGroup, Descriptor.TypeName);
        if (animal is not AnimalBuilder typed)
        {
            return animal == null;
        }
        builder.AddAnimal(typed);
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(Zoo value) =>
        value.Animal is { Count: > 0 } ? new[] { "animal" } : Array.Empty<string>();

    protected override void WriteProperty(
        Zoo value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (!string.Equals(propertyName, "animal", StringComparison.Ordinal))
        {
            return;
        }
        foreach (Animal animal in value.Animal ?? Array.Empty<Animal>())
        {
            writer.WriteSubstitutedElement(animal);
        }
    }
}
