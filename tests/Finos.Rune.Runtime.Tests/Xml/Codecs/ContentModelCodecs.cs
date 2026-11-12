using System.Xml;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;

namespace Finos.Rune.Runtime.Tests.Xml.Codecs;

public sealed class FpmlTextValueCodec : XmlTypeCodec<FpmlTextValue, FpmlTextValue.Builder>
{
    public static readonly FpmlTextValueCodec Instance = new FpmlTextValueCodec();

    private FpmlTextValueCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.FpmlTextValue",
            new[]
            {
                new XmlPropertyDescriptor("value", new XmlQName("value"), XmlPropertyRepresentation.Value),
                new XmlPropertyDescriptor("scheme", new XmlQName("scheme"), XmlPropertyRepresentation.Attribute),
            }))
    {
    }

    public override FpmlTextValue.Builder CreateTypedBuilder() => FpmlTextValue.CreateBuilder();

    protected override bool TryReadAttribute(
        FpmlTextValue.Builder builder, XmlQName name, string text, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "scheme", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetScheme(text);
        return true;
    }

    protected override bool TryReadText(FpmlTextValue.Builder builder, string text, XmlReadContext context)
    {
        builder.SetValue(text);
        return true;
    }

    protected override void WriteAttributes(FpmlTextValue value, XmlObjectWriter writer, XmlWriteContext context) =>
        writer.WriteAttribute("scheme", value.Scheme);

    protected override string? TextValue(FpmlTextValue value, XmlWriteContext context) => value.Value;
}

public sealed class FpmlReferenceCodec : XmlTypeCodec<FpmlReference, FpmlReference.Builder>
{
    public static readonly FpmlReferenceCodec Instance = new FpmlReferenceCodec();

    private FpmlReferenceCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.FpmlReference",
            new[]
            {
                new XmlPropertyDescriptor("href", new XmlQName("href"), XmlPropertyRepresentation.Attribute),
            }))
    {
    }

    public override FpmlReference.Builder CreateTypedBuilder() => FpmlReference.CreateBuilder();

    protected override bool TryReadAttribute(
        FpmlReference.Builder builder, XmlQName name, string text, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "href", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetHref(text);
        return true;
    }

    protected override void WriteAttributes(FpmlReference value, XmlObjectWriter writer, XmlWriteContext context) =>
        writer.WriteAttribute("href", value.Href);
}

public sealed class FpmlTradeIdentifierChoiceCodec
    : XmlTypeCodec<FpmlTradeIdentifierChoice, FpmlTradeIdentifierChoice.Builder>
{
    public static readonly FpmlTradeIdentifierChoiceCodec Instance = new FpmlTradeIdentifierChoiceCodec();

    private static readonly XmlQName TradeIdName = new XmlQName("tradeId");
    private static readonly XmlQName VersionedTradeIdName = new XmlQName("versionedTradeId");

    private FpmlTradeIdentifierChoiceCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.FpmlTradeIdentifierChoice",
            new[]
            {
                new XmlPropertyDescriptor("tradeId", TradeIdName),
                new XmlPropertyDescriptor("versionedTradeId", VersionedTradeIdName),
            }))
    {
    }

    public override FpmlTradeIdentifierChoice.Builder CreateTypedBuilder() =>
        FpmlTradeIdentifierChoice.CreateBuilder();

    protected override bool TryReadElement(
        FpmlTradeIdentifierChoice.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "tradeId":
                builder.SetTradeId(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
                return true;
            case "versionedTradeId":
                builder.SetVersionedTradeId(
                    context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
                return true;
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(FpmlTradeIdentifierChoice value)
    {
        var present = new List<string>();
        if (value.TradeId != null)
        {
            present.Add("tradeId");
        }
        if (value.VersionedTradeId != null)
        {
            present.Add("versionedTradeId");
        }
        return present;
    }

    protected override void WriteProperty(
        FpmlTradeIdentifierChoice value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "tradeId":
                writer.WriteObjectElement(TradeIdName, value.TradeId, FpmlTextValueCodec.Instance);
                break;
            case "versionedTradeId":
                writer.WriteObjectElement(VersionedTradeIdName, value.VersionedTradeId, FpmlTextValueCodec.Instance);
                break;
        }
    }
}

public sealed class FpmlTradeIdentifierCodec : XmlTypeCodec<FpmlTradeIdentifier, FpmlTradeIdentifier.Builder>
{
    public static readonly FpmlTradeIdentifierCodec Instance = new FpmlTradeIdentifierCodec();

    private static readonly XmlQName IssuerName = new XmlQName("issuer");
    private static readonly XmlQName TradeIdName = new XmlQName("tradeId");
    private static readonly XmlQName PartyReferenceName = new XmlQName("partyReference");
    private static readonly XmlQName AccountReferenceName = new XmlQName("accountReference");

    private FpmlTradeIdentifierCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.FpmlTradeIdentifier",
            new[]
            {
                new XmlPropertyDescriptor("id", new XmlQName("id"), XmlPropertyRepresentation.Attribute),
                // Listed first on purpose, reproducing Java's bean order for the orderer to fix.
                new XmlPropertyDescriptor(
                    "tradeIdentifierChoice",
                    new XmlQName("tradeIdentifierChoice"),
                    XmlPropertyRepresentation.Virtual,
                    isMulti: true),
                new XmlPropertyDescriptor("issuer", IssuerName),
                new XmlPropertyDescriptor("tradeId", TradeIdName),
                new XmlPropertyDescriptor("partyReference", PartyReferenceName),
                new XmlPropertyDescriptor("accountReference", AccountReferenceName),
            },
            elementName: new XmlQName("FpmlTradeIdentifier"),
            contentModel: TestContentModels.TradeIdentifier))
    {
    }

    public override FpmlTradeIdentifier.Builder CreateTypedBuilder() => FpmlTradeIdentifier.CreateBuilder();

    public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
        string.Equals(propertyName, "tradeIdentifierChoice", StringComparison.Ordinal)
            ? FpmlTradeIdentifierChoiceCodec.Instance
            : null;

    protected override RosettaModelObjectBuilder? CreateVirtualSlot(
        FpmlTradeIdentifier.Builder builder, string propertyName, XmlReadContext context) =>
        string.Equals(propertyName, "tradeIdentifierChoice", StringComparison.Ordinal)
            ? builder.AddTradeIdentifierChoice()
            : null;

    protected override bool TryReadAttribute(
        FpmlTradeIdentifier.Builder builder, XmlQName name, string text, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "id", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetId(text);
        return true;
    }

    protected override bool TryReadElement(
        FpmlTradeIdentifier.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "issuer":
                builder.SetIssuer(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
                return true;
            case "tradeId":
                builder.SetTradeId(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
                return true;
            case "partyReference":
                builder.SetPartyReference(
                    context.ReadObject<FpmlReference.Builder>(reader, FpmlReferenceCodec.Instance));
                return true;
            case "accountReference":
                builder.SetAccountReference(
                    context.ReadObject<FpmlReference.Builder>(reader, FpmlReferenceCodec.Instance));
                return true;
            default:
                return false;
        }
    }

    protected override void WriteAttributes(
        FpmlTradeIdentifier value, XmlObjectWriter writer, XmlWriteContext context) =>
        writer.WriteAttribute("id", value.Id);

    protected override IReadOnlyList<string> PresentProperties(FpmlTradeIdentifier value)
    {
        var present = new List<string>();
        if (value.TradeIdentifierChoice is { Count: > 0 })
        {
            present.Add("tradeIdentifierChoice");
        }
        if (value.Issuer != null)
        {
            present.Add("issuer");
        }
        if (value.TradeId != null)
        {
            present.Add("tradeId");
        }
        if (value.PartyReference != null)
        {
            present.Add("partyReference");
        }
        if (value.AccountReference != null)
        {
            present.Add("accountReference");
        }
        return present;
    }

    protected override void WriteProperty(
        FpmlTradeIdentifier value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "issuer":
                writer.WriteObjectElement(IssuerName, value.Issuer, FpmlTextValueCodec.Instance);
                break;
            case "tradeId":
                writer.WriteObjectElement(TradeIdName, value.TradeId, FpmlTextValueCodec.Instance);
                break;
            case "partyReference":
                writer.WriteObjectElement(PartyReferenceName, value.PartyReference, FpmlReferenceCodec.Instance);
                break;
            case "accountReference":
                writer.WriteObjectElement(AccountReferenceName, value.AccountReference, FpmlReferenceCodec.Instance);
                break;
            case "tradeIdentifierChoice":
                foreach (FpmlTradeIdentifierChoice choice in value.TradeIdentifierChoice
                    ?? Array.Empty<FpmlTradeIdentifierChoice>())
                {
                    writer.WriteVirtual(choice, FpmlTradeIdentifierChoiceCodec.Instance);
                }
                break;
        }
    }
}

public sealed class FpmlTradeIdentifierNoContentCodec
    : XmlTypeCodec<FpmlTradeIdentifier, FpmlTradeIdentifier.Builder>
{
    public static readonly FpmlTradeIdentifierNoContentCodec Instance = new();

    private FpmlTradeIdentifierNoContentCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.FpmlTradeIdentifierNoContent",
            new[]
            {
                new XmlPropertyDescriptor(
                    "tradeIdentifierChoice",
                    new XmlQName("tradeIdentifierChoice"),
                    XmlPropertyRepresentation.Virtual,
                    isMulti: true),
            },
            elementName: new XmlQName("FpmlTradeIdentifierNoContent")))
    {
    }

    public override FpmlTradeIdentifier.Builder CreateTypedBuilder() =>
        FpmlTradeIdentifier.CreateBuilder();

    public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
        string.Equals(propertyName, "tradeIdentifierChoice", StringComparison.Ordinal)
            ? FpmlTradeIdentifierChoiceCodec.Instance
            : null;

    protected override bool TryReadElement(
        FpmlTradeIdentifier.Builder builder,
        XmlQName name,
        XmlReader reader,
        XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "tradeId", StringComparison.Ordinal)
            && !string.Equals(name.LocalName, "versionedTradeId", StringComparison.Ordinal))
        {
            return false;
        }

        bool append = builder.TradeIdentifierChoice.Count == 0;
        FpmlTradeIdentifierChoice.Builder slot = append
            ? FpmlTradeIdentifierChoice.CreateBuilder()
            : builder.TradeIdentifierChoice[builder.TradeIdentifierChoice.Count - 1];
        IXmlTypeCodec slotCodec = FpmlTradeIdentifierChoiceCodec.Instance;
        if (!slotCodec.TryReadElement(slot, name, reader, context))
        {
            return false;
        }
        if (append)
        {
            builder.TradeIdentifierChoice.Add(slot);
        }
        return true;
    }
}

public sealed class SchemaLocationContainerCodec
    : XmlTypeCodec<SchemaLocationContainer, SchemaLocationContainer.Builder>
{
    public static readonly SchemaLocationContainerCodec Instance = new SchemaLocationContainerCodec();

    private static readonly XmlQName TradeIdentifierName = new XmlQName("tradeIdentifier");

    private SchemaLocationContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.SchemaLocationContainer",
            new[] { new XmlPropertyDescriptor("tradeIdentifier", TradeIdentifierName) },
            elementName: new XmlQName("SchemaLocationContainer")))
    {
    }

    public override SchemaLocationContainer.Builder CreateTypedBuilder() => SchemaLocationContainer.CreateBuilder();

    protected override bool TryReadElement(
        SchemaLocationContainer.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "tradeIdentifier", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetTradeIdentifier(
            context.ReadObject<FpmlTradeIdentifier.Builder>(reader, FpmlTradeIdentifierCodec.Instance));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(SchemaLocationContainer value) =>
        value.TradeIdentifier != null ? new[] { "tradeIdentifier" } : Array.Empty<string>();

    protected override void WriteProperty(
        SchemaLocationContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (string.Equals(propertyName, "tradeIdentifier", StringComparison.Ordinal))
        {
            writer.WriteObjectElement(TradeIdentifierName, value.TradeIdentifier, FpmlTradeIdentifierCodec.Instance);
        }
    }
}

public sealed class LeafEntryCodec : XmlTypeCodec<LeafEntry, LeafEntry.Builder>
{
    public static readonly LeafEntryCodec Instance = new LeafEntryCodec();

    private static readonly XmlQName FirstValueName = new XmlQName("firstValue");
    private static readonly XmlQName SecondValueName = new XmlQName("secondValue");

    private LeafEntryCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.LeafEntry",
            new[]
            {
                new XmlPropertyDescriptor("firstValue", FirstValueName),
                new XmlPropertyDescriptor("secondValue", SecondValueName),
            }))
    {
    }

    public override LeafEntry.Builder CreateTypedBuilder() => LeafEntry.CreateBuilder();

    protected override bool TryReadElement(
        LeafEntry.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "firstValue":
                builder.SetFirstValue(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
                return true;
            case "secondValue":
                builder.SetSecondValue(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
                return true;
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(LeafEntry value)
    {
        var present = new List<string>();
        if (value.FirstValue != null)
        {
            present.Add("firstValue");
        }
        if (value.SecondValue != null)
        {
            present.Add("secondValue");
        }
        return present;
    }

    protected override void WriteProperty(
        LeafEntry value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "firstValue":
                writer.WriteObjectElement(FirstValueName, value.FirstValue, FpmlTextValueCodec.Instance);
                break;
            case "secondValue":
                writer.WriteObjectElement(SecondValueName, value.SecondValue, FpmlTextValueCodec.Instance);
                break;
        }
    }
}

/// <summary>
/// A container of VIRTUAL leaf entries. Two instances exist: one whose content model is a repeated
/// SEQUENCE and one whose content model is an ALL group, so the same builder shape exercises both
/// node kinds.
/// </summary>
public sealed class LeafContainerCodec : XmlTypeCodec<LeafContainer, LeafContainer.Builder>
{
    public static readonly LeafContainerCodec MultiLeaf = new LeafContainerCodec(
        "com.rosetta.test.MultiLeafContainer", "MultiLeafContainer", TestContentModels.MultiLeafSequence);

    public static readonly LeafContainerCodec AllVirtual = new LeafContainerCodec(
        "com.rosetta.test.AllVirtualContainer", "AllVirtualContainer", TestContentModels.AllVirtual);

    private LeafContainerCodec(string typeName, string elementName, XmlContentModelNode contentModel)
        : base(new XmlTypeDescriptor(
            typeName,
            new[]
            {
                new XmlPropertyDescriptor(
                    "entry", new XmlQName("entry"), XmlPropertyRepresentation.Virtual, isMulti: true),
            },
            elementName: new XmlQName(elementName),
            contentModel: contentModel))
    {
    }

    public override LeafContainer.Builder CreateTypedBuilder() => LeafContainer.CreateBuilder();

    public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
        string.Equals(propertyName, "entry", StringComparison.Ordinal) ? LeafEntryCodec.Instance : null;

    protected override RosettaModelObjectBuilder? CreateVirtualSlot(
        LeafContainer.Builder builder, string propertyName, XmlReadContext context) =>
        string.Equals(propertyName, "entry", StringComparison.Ordinal) ? builder.AddEntry() : null;

    protected override IReadOnlyList<string> PresentProperties(LeafContainer value) =>
        value.Entry is { Count: > 0 } ? new[] { "entry" } : Array.Empty<string>();

    protected override void WriteProperty(
        LeafContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (!string.Equals(propertyName, "entry", StringComparison.Ordinal))
        {
            return;
        }
        foreach (LeafEntry entry in value.Entry ?? Array.Empty<LeafEntry>())
        {
            writer.WriteVirtual(entry, LeafEntryCodec.Instance);
        }
    }
}

public sealed class MultiLayerOuterCodec : XmlTypeCodec<MultiLayerOuter, MultiLayerOuter.Builder>
{
    public static readonly MultiLayerOuterCodec Instance = new MultiLayerOuterCodec();

    private MultiLayerOuterCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.MultiLayerOuter",
            new[]
            {
                new XmlPropertyDescriptor(
                    "inner", new XmlQName("inner"), XmlPropertyRepresentation.Virtual, isMulti: true),
            }))
    {
    }

    public override MultiLayerOuter.Builder CreateTypedBuilder() => MultiLayerOuter.CreateBuilder();

    public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
        string.Equals(propertyName, "inner", StringComparison.Ordinal) ? LeafEntryCodec.Instance : null;

    protected override RosettaModelObjectBuilder? CreateVirtualSlot(
        MultiLayerOuter.Builder builder, string propertyName, XmlReadContext context) =>
        string.Equals(propertyName, "inner", StringComparison.Ordinal) ? builder.AddInner() : null;

    protected override IReadOnlyList<string> PresentProperties(MultiLayerOuter value) =>
        value.Inner is { Count: > 0 } ? new[] { "inner" } : Array.Empty<string>();

    protected override void WriteProperty(
        MultiLayerOuter value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (!string.Equals(propertyName, "inner", StringComparison.Ordinal))
        {
            return;
        }
        foreach (LeafEntry entry in value.Inner ?? Array.Empty<LeafEntry>())
        {
            writer.WriteVirtual(entry, LeafEntryCodec.Instance);
        }
    }
}

public sealed class MultiLayerContainerCodec : XmlTypeCodec<MultiLayerContainer, MultiLayerContainer.Builder>
{
    public static readonly MultiLayerContainerCodec Instance = new MultiLayerContainerCodec();

    private MultiLayerContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.MultiLayerContainer",
            new[]
            {
                new XmlPropertyDescriptor(
                    "outer", new XmlQName("outer"), XmlPropertyRepresentation.Virtual, isMulti: true),
            },
            elementName: new XmlQName("MultiLayerContainer"),
            contentModel: TestContentModels.MultiLayer))
    {
    }

    public override MultiLayerContainer.Builder CreateTypedBuilder() => MultiLayerContainer.CreateBuilder();

    public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
        string.Equals(propertyName, "outer", StringComparison.Ordinal) ? MultiLayerOuterCodec.Instance : null;

    protected override RosettaModelObjectBuilder? CreateVirtualSlot(
        MultiLayerContainer.Builder builder, string propertyName, XmlReadContext context) =>
        string.Equals(propertyName, "outer", StringComparison.Ordinal) ? builder.AddOuter() : null;

    protected override IReadOnlyList<string> PresentProperties(MultiLayerContainer value) =>
        value.Outer is { Count: > 0 } ? new[] { "outer" } : Array.Empty<string>();

    protected override void WriteProperty(
        MultiLayerContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (!string.Equals(propertyName, "outer", StringComparison.Ordinal))
        {
            return;
        }
        foreach (MultiLayerOuter outer in value.Outer ?? Array.Empty<MultiLayerOuter>())
        {
            writer.WriteVirtual(outer, MultiLayerOuterCodec.Instance);
        }
    }
}

public sealed class AnyVirtualEntryCodec : XmlTypeCodec<AnyVirtualEntry, AnyVirtualEntry.Builder>
{
    public static readonly AnyVirtualEntryCodec Instance = new AnyVirtualEntryCodec();

    private AnyVirtualEntryCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.AnyVirtualEntry",
            new[] { new XmlPropertyDescriptor("wildcardValue", new XmlQName("wildcardValue")) }))
    {
    }

    public override AnyVirtualEntry.Builder CreateTypedBuilder() => AnyVirtualEntry.CreateBuilder();

    protected override bool TryReadElement(
        AnyVirtualEntry.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        builder.SetWildcardValue(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(AnyVirtualEntry value) =>
        value.WildcardValue != null ? new[] { "wildcardValue" } : Array.Empty<string>();
}

public sealed class AnyVirtualContainerCodec : XmlTypeCodec<AnyVirtualContainer, AnyVirtualContainer.Builder>
{
    public static readonly AnyVirtualContainerCodec Instance = new AnyVirtualContainerCodec();

    private AnyVirtualContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.AnyVirtualContainer",
            new[]
            {
                new XmlPropertyDescriptor("knownValue", new XmlQName("knownValue")),
                new XmlPropertyDescriptor(
                    "entry", new XmlQName("entry"), XmlPropertyRepresentation.Virtual, isMulti: true),
            },
            elementName: new XmlQName("AnyVirtualContainer"),
            contentModel: TestContentModels.AnyVirtual))
    {
    }

    public override AnyVirtualContainer.Builder CreateTypedBuilder() => AnyVirtualContainer.CreateBuilder();

    public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
        string.Equals(propertyName, "entry", StringComparison.Ordinal) ? AnyVirtualEntryCodec.Instance : null;

    protected override RosettaModelObjectBuilder? CreateVirtualSlot(
        AnyVirtualContainer.Builder builder, string propertyName, XmlReadContext context) =>
        string.Equals(propertyName, "entry", StringComparison.Ordinal) ? builder.AddEntry() : null;

    protected override bool TryReadElement(
        AnyVirtualContainer.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "knownValue", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetKnownValue(context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(AnyVirtualContainer value)
    {
        var present = new List<string>();
        if (value.KnownValue != null)
        {
            present.Add("knownValue");
        }
        if (value.Entry is { Count: > 0 })
        {
            present.Add("entry");
        }
        return present;
    }
}

/// <summary>The XSD content models the codecs above declare, written once and shared.</summary>
public static class TestContentModels
{
    public static readonly XmlContentModelNode TradeIdentifier = XmlContentModelNode.Choice(
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
                    "versionedTradeId",
                    new[] { "tradeIdentifierChoice", "versionedTradeId" },
                    1,
                    XmlOccursMax.Of(1)))));

    public static readonly XmlContentModelNode MultiLeafSequence = new XmlContentModelNode(
        XmlContentModelNodeType.Sequence,
        minOccurs: 0,
        maxOccurs: XmlOccursMax.Unbounded,
        children: new[]
        {
            XmlContentModelNode.Element("firstValue", new[] { "entry", "firstValue" }, 1, XmlOccursMax.Of(1)),
            XmlContentModelNode.Element("secondValue", new[] { "entry", "secondValue" }, 1, XmlOccursMax.Of(1)),
        });

    public static readonly XmlContentModelNode AllVirtual = new XmlContentModelNode(
        XmlContentModelNodeType.All,
        children: new[]
        {
            XmlContentModelNode.Element("firstValue", new[] { "entry", "firstValue" }, 1, XmlOccursMax.Of(1)),
            XmlContentModelNode.Element("secondValue", new[] { "entry", "secondValue" }, 1, XmlOccursMax.Of(1)),
        });

    public static readonly XmlContentModelNode MultiLayer = new XmlContentModelNode(
        XmlContentModelNodeType.Sequence,
        minOccurs: 0,
        maxOccurs: XmlOccursMax.Unbounded,
        children: new[]
        {
            XmlContentModelNode.Element(
                "firstValue", new[] { "outer", "inner", "firstValue" }, 1, XmlOccursMax.Of(1)),
            XmlContentModelNode.Element(
                "secondValue", new[] { "outer", "inner", "secondValue" }, 1, XmlOccursMax.Of(1)),
        });

    public static readonly XmlContentModelNode AnyVirtual = XmlContentModelNode.Sequence(
        1,
        XmlOccursMax.Of(1),
        XmlContentModelNode.Any(new[] { "entry", "wildcardValue" }, 1, XmlOccursMax.Of(1)),
        XmlContentModelNode.Element("knownValue", new[] { "knownValue" }, 1, XmlOccursMax.Of(1)));
}

/// <summary>The registry every XML test reads and writes through.</summary>
public static class TestCodecs
{
    public static readonly XmlQName AnimalGroup = new XmlQName("urn:my.schema", "animal");

    public static XmlCodecRegistry CreateRegistry() => new XmlCodecRegistry().RegisterAll(
        FooCodec.Instance,
        MeasureCodec.Instance,
        TopLevelCodec.Instance,
        TopLevelExtensionCodec.Instance,
        MulticardinalityContainerCodec.Instance,
        PartyNameModelCodec.Instance,
        PartyModelCodec.Instance,
        PartyCodec.Instance,
        TypeWithTypeElementCodec.Instance,
        TimeContainerCodec.Instance,
        ZonedContainerCodec.Instance,
        GoatCodec.Instance,
        CowCodec.Instance,
        SharkCodec.Instance,
        SalmonCodec.Instance,
        LlamaCodec.Instance,
        ExtensionLlamaCodec.Instance,
        AnimalContainerCodec.Instance,
        ZooCodec.Instance,
        FpmlTextValueCodec.Instance,
        FpmlReferenceCodec.Instance,
        FpmlTradeIdentifierChoiceCodec.Instance,
        FpmlTradeIdentifierCodec.Instance,
        SchemaLocationContainerCodec.Instance,
        LeafEntryCodec.Instance,
        LeafContainerCodec.MultiLeaf,
        LeafContainerCodec.AllVirtual,
        MultiLayerOuterCodec.Instance,
        MultiLayerContainerCodec.Instance,
        AnyVirtualEntryCodec.Instance,
        AnyVirtualContainerCodec.Instance);
}
