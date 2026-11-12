using System.Xml;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;

namespace Finos.Rune.Runtime.Tests.Xml.Codecs;

public sealed class FooCodec : XmlTypeCodec<Foo, Foo.Builder>
{
    public static readonly FooCodec Instance = new FooCodec();

    private static readonly XmlQName XmlValueName = new XmlQName("XMLValue");
    private static readonly XmlQName Attr1Name = new XmlQName("Attr1");

    private FooCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.Foo",
            new[]
            {
                new XmlPropertyDescriptor("xmlValue", XmlValueName),
                new XmlPropertyDescriptor("attr1", Attr1Name, isMulti: true),
            }))
    {
    }

    public override Foo.Builder CreateTypedBuilder() => Foo.CreateBuilder();

    protected override bool TryReadElement(
        Foo.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "XMLValue":
                builder.SetXmlValue(context.ReadText(reader));
                return true;
            case "Attr1":
                builder.AddAttr1(context.ReadText(reader));
                return true;
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(Foo value)
    {
        var present = new List<string>();
        if (value.XmlValue != null)
        {
            present.Add("xmlValue");
        }
        if (value.Attr1 is { Count: > 0 })
        {
            present.Add("attr1");
        }
        return present;
    }

    protected override void WriteProperty(
        Foo value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "xmlValue":
                writer.WriteTextElement(XmlValueName, value.XmlValue);
                break;
            case "attr1":
                foreach (string entry in value.Attr1 ?? Array.Empty<string>())
                {
                    writer.WriteTextElement(Attr1Name, entry);
                }
                break;
        }
    }
}

public sealed class MeasureCodec : XmlTypeCodec<Measure, Measure.Builder>
{
    public static readonly MeasureCodec Instance = new MeasureCodec();

    private static readonly XmlEnumCodec<UnitEnum> Units = new XmlEnumCodec<UnitEnum>(
        new Dictionary<UnitEnum, string>
        {
            [UnitEnum.Meter] = "Meter",
            [UnitEnum.Kilogram] = "Kilogram",
        });

    private MeasureCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.Measure",
            new[]
            {
                new XmlPropertyDescriptor("value", new XmlQName("value"), XmlPropertyRepresentation.Value),
                new XmlPropertyDescriptor("unit", new XmlQName("Unit"), XmlPropertyRepresentation.Attribute),
            }))
    {
    }

    public override Measure.Builder CreateTypedBuilder() => Measure.CreateBuilder();

    protected override bool TryReadAttribute(
        Measure.Builder builder, XmlQName name, string text, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "Unit", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetUnit(Units.Parse(text));
        return true;
    }

    protected override bool TryReadText(Measure.Builder builder, string text, XmlReadContext context)
    {
        builder.SetValue(XmlLexicalValue.ParseDecimal(text));
        return true;
    }

    protected override void WriteAttributes(Measure value, XmlObjectWriter writer, XmlWriteContext context) =>
        writer.WriteAttribute("Unit", Units.Format(value.Unit));

    protected override string? TextValue(Measure value, XmlWriteContext context) =>
        value.Value == null ? null : XmlLexicalValue.FormatDecimal(value.Value.Value);
}

public sealed class TopLevelCodec : XmlTypeCodec<TopLevel, TopLevel.Builder>
{
    public static readonly TopLevelCodec Instance = new TopLevelCodec();

    private static readonly XmlQName AttrName = new XmlQName("Attr");
    private static readonly XmlQName ValueName = new XmlQName("Value");

    private TopLevelCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.TopLevel",
            new[]
            {
                new XmlPropertyDescriptor("attr", AttrName),
                new XmlPropertyDescriptor("value", ValueName),
            },
            elementName: new XmlQName("urn:my.schema", "TopLevel"),
            constantAttributes: new[]
            {
                new KeyValuePair<string, string>("xmlns", "urn:my.schema"),
                new KeyValuePair<string, string>("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            }))
    {
    }

    public override TopLevel.Builder CreateTypedBuilder() => TopLevel.CreateBuilder();

    protected override bool TryReadElement(
        TopLevel.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "Attr":
                builder.SetAttr(context.ReadObject<Foo.Builder>(reader, FooCodec.Instance));
                return true;
            case "Value":
                builder.SetValue(context.ReadObject<Measure.Builder>(reader, MeasureCodec.Instance));
                return true;
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(TopLevel value)
    {
        var present = new List<string>();
        if (value.Attr != null)
        {
            present.Add("attr");
        }
        if (value.Value != null)
        {
            present.Add("value");
        }
        return present;
    }

    protected override void WriteProperty(
        TopLevel value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "attr":
                writer.WriteObjectElement(AttrName, value.Attr, FooCodec.Instance);
                break;
            case "value":
                writer.WriteObjectElement(ValueName, value.Value, MeasureCodec.Instance);
                break;
        }
    }
}

public sealed class TopLevelExtensionCodec : XmlTypeCodec<TopLevelExtension, TopLevelExtension.Builder>
{
    public static readonly TopLevelExtensionCodec Instance = new TopLevelExtensionCodec();

    private static readonly XmlQName AttrName = new XmlQName("Attr");
    private static readonly XmlQName ValueName = new XmlQName("Value");
    private static readonly XmlQName ExtensionName = new XmlQName("DocumentExtensionAttr");

    private TopLevelExtensionCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.TopLevelExtension",
            new[]
            {
                new XmlPropertyDescriptor("attr", AttrName),
                new XmlPropertyDescriptor("value", ValueName),
                new XmlPropertyDescriptor("documentExtensionAttr", ExtensionName),
            },
            elementName: new XmlQName("urn:my.schema", "TopLevelExtension"),
            constantAttributes: new[]
            {
                new KeyValuePair<string, string>("xmlns", "urn:my.schema"),
                new KeyValuePair<string, string>("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            }))
    {
    }

    public override TopLevelExtension.Builder CreateTypedBuilder() => TopLevelExtension.CreateBuilder();

    protected override bool TryReadElement(
        TopLevelExtension.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "Attr":
                builder.SetAttr(context.ReadObject<Foo.Builder>(reader, FooCodec.Instance));
                return true;
            case "Value":
                builder.SetValue(context.ReadObject<Measure.Builder>(reader, MeasureCodec.Instance));
                return true;
            case "DocumentExtensionAttr":
                builder.SetDocumentExtensionAttr(context.ReadText(reader));
                return true;
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(TopLevelExtension value)
    {
        var present = new List<string>();
        if (value.Attr != null)
        {
            present.Add("attr");
        }
        if (value.Value != null)
        {
            present.Add("value");
        }
        if (value.DocumentExtensionAttr != null)
        {
            present.Add("documentExtensionAttr");
        }
        return present;
    }

    protected override void WriteProperty(
        TopLevelExtension value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "attr":
                writer.WriteObjectElement(AttrName, value.Attr, FooCodec.Instance);
                break;
            case "value":
                writer.WriteObjectElement(ValueName, value.Value, MeasureCodec.Instance);
                break;
            case "documentExtensionAttr":
                writer.WriteTextElement(ExtensionName, value.DocumentExtensionAttr);
                break;
        }
    }
}

public sealed class MulticardinalityContainerCodec
    : XmlTypeCodec<MulticardinalityContainer, MulticardinalityContainer.Builder>
{
    public static readonly MulticardinalityContainerCodec Instance = new MulticardinalityContainerCodec();

    private static readonly XmlQName FooName = new XmlQName("foo");

    private MulticardinalityContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.MulticardinalityContainer",
            new[] { new XmlPropertyDescriptor("foo", FooName, isMulti: true) },
            elementName: new XmlQName("MulticardinalityContainer")))
    {
    }

    public override MulticardinalityContainer.Builder CreateTypedBuilder() =>
        MulticardinalityContainer.CreateBuilder();

    protected override bool TryReadElement(
        MulticardinalityContainer.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "foo", StringComparison.Ordinal))
        {
            return false;
        }
        builder.Foo.Add(context.ReadObject<Foo.Builder>(reader, FooCodec.Instance));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(MulticardinalityContainer value) =>
        value.Foo is { Count: > 0 } ? new[] { "foo" } : Array.Empty<string>();

    protected override void WriteProperty(
        MulticardinalityContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (!string.Equals(propertyName, "foo", StringComparison.Ordinal))
        {
            return;
        }
        foreach (Foo entry in value.Foo ?? Array.Empty<Foo>())
        {
            writer.WriteObjectElement(FooName, entry, FooCodec.Instance);
        }
    }
}

public sealed class PartyNameModelCodec : XmlTypeCodec<PartyNameModel, PartyNameModel.Builder>
{
    public static readonly PartyNameModelCodec Instance = new PartyNameModelCodec();

    private static readonly XmlQName PartyNameName = new XmlQName("partyName");

    private PartyNameModelCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.PartyNameModel",
            new[] { new XmlPropertyDescriptor("partyName", PartyNameName) }))
    {
    }

    public override PartyNameModel.Builder CreateTypedBuilder() => PartyNameModel.CreateBuilder();

    protected override bool TryReadElement(
        PartyNameModel.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "partyName", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetPartyName(context.ReadText(reader));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(PartyNameModel value) =>
        value.PartyName != null ? new[] { "partyName" } : Array.Empty<string>();

    protected override void WriteProperty(
        PartyNameModel value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (string.Equals(propertyName, "partyName", StringComparison.Ordinal))
        {
            writer.WriteTextElement(PartyNameName, value.PartyName);
        }
    }
}

public sealed class PartyModelCodec : XmlTypeCodec<PartyModel, PartyModel.Builder>
{
    public static readonly PartyModelCodec Instance = new PartyModelCodec();

    private static readonly XmlQName PartyIdName = new XmlQName("partyId");

    private PartyModelCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.PartyModel",
            new[] { new XmlPropertyDescriptor("partyId", PartyIdName, isMulti: true) }))
    {
    }

    public override PartyModel.Builder CreateTypedBuilder() => PartyModel.CreateBuilder();

    protected override bool TryReadElement(
        PartyModel.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "partyId", StringComparison.Ordinal))
        {
            return false;
        }
        builder.AddPartyId(context.ReadText(reader));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(PartyModel value) =>
        value.PartyId is { Count: > 0 } ? new[] { "partyId" } : Array.Empty<string>();

    protected override void WriteProperty(
        PartyModel value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (!string.Equals(propertyName, "partyId", StringComparison.Ordinal))
        {
            return;
        }
        foreach (string entry in value.PartyId ?? Array.Empty<string>())
        {
            writer.WriteTextElement(PartyIdName, entry);
        }
    }
}

/// <summary>
/// Party carries two VIRTUAL groups: their leaves are read and written flattened into the Party
/// element itself, with no wrapper of their own and no list wrapper around the repeated partyId.
/// </summary>
public sealed class PartyCodec : XmlTypeCodec<Party, Party.Builder>
{
    public static readonly PartyCodec Instance = new PartyCodec();

    private PartyCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.Party",
            new[]
            {
                new XmlPropertyDescriptor(
                    "partyNameModel", new XmlQName("partyNameModel"), XmlPropertyRepresentation.Virtual),
                new XmlPropertyDescriptor(
                    "partyModel", new XmlQName("partyModel"), XmlPropertyRepresentation.Virtual),
            },
            elementName: new XmlQName("Party")))
    {
    }

    public override Party.Builder CreateTypedBuilder() => Party.CreateBuilder();

    protected override bool TryReadElement(
        Party.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "partyName":
                builder.GetOrCreatePartyNameModel().SetPartyName(context.ReadText(reader));
                return true;
            case "partyId":
                builder.GetOrCreatePartyModel().AddPartyId(context.ReadText(reader));
                return true;
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(Party value)
    {
        var present = new List<string>();
        if (value.PartyNameModel != null)
        {
            present.Add("partyNameModel");
        }
        if (value.PartyModel != null)
        {
            present.Add("partyModel");
        }
        return present;
    }

    protected override void WriteProperty(
        Party value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "partyNameModel":
                writer.WriteVirtual(value.PartyNameModel, PartyNameModelCodec.Instance);
                break;
            case "partyModel":
                writer.WriteVirtual(value.PartyModel, PartyModelCodec.Instance);
                break;
        }
    }
}

public sealed class TypeWithTypeElementCodec : XmlTypeCodec<TypeWithTypeElement, TypeWithTypeElement.Builder>
{
    public static readonly TypeWithTypeElementCodec Instance = new TypeWithTypeElementCodec();

    private static readonly XmlQName FirstElementName = new XmlQName("firstElement");
    private static readonly XmlQName TypeName = new XmlQName("type");

    private TypeWithTypeElementCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.TypeWithTypeElement",
            new[]
            {
                new XmlPropertyDescriptor("firstElement", FirstElementName),
                new XmlPropertyDescriptor("type", TypeName),
            },
            elementName: new XmlQName("TypeWithTypeElement")))
    {
    }

    public override TypeWithTypeElement.Builder CreateTypedBuilder() => TypeWithTypeElement.CreateBuilder();

    protected override bool TryReadElement(
        TypeWithTypeElement.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "firstElement":
                builder.SetFirstElement(context.ReadText(reader));
                return true;
            case "type":
                builder.SetType(context.ReadText(reader));
                return true;
            default:
                return false;
        }
    }

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
                writer.WriteTextElement(TypeName, value.Type);
                break;
        }
    }
}

public sealed class TimeContainerCodec : XmlTypeCodec<TimeContainer, TimeContainer.Builder>
{
    public static readonly TimeContainerCodec Instance = new TimeContainerCodec();

    private static readonly XmlQName TimeValueName = new XmlQName("timeValue");

    private TimeContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.TimeContainer",
            new[] { new XmlPropertyDescriptor("timeValue", TimeValueName) },
            elementName: new XmlQName("TimeContainer")))
    {
    }

    public override TimeContainer.Builder CreateTypedBuilder() => TimeContainer.CreateBuilder();

    protected override bool TryReadElement(
        TimeContainer.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        if (!string.Equals(name.LocalName, "timeValue", StringComparison.Ordinal))
        {
            return false;
        }
        builder.SetTimeValue(XmlLexicalValue.ParseTime(context.ReadText(reader)));
        return true;
    }

    protected override IReadOnlyList<string> PresentProperties(TimeContainer value) =>
        value.TimeValue != null ? new[] { "timeValue" } : Array.Empty<string>();

    protected override void WriteProperty(
        TimeContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        if (string.Equals(propertyName, "timeValue", StringComparison.Ordinal) && value.TimeValue != null)
        {
            writer.WriteTextElement(TimeValueName, XmlLexicalValue.FormatTime(value.TimeValue.Value));
        }
    }
}

/// <summary>
/// Reads and writes a zoned dateTime whose public property is a <see cref="DateTimeOffset"/>,
/// preserving the lexical timezone that was written in XML-only metadata on the builder.
/// </summary>
public sealed class ZonedContainerCodec : XmlTypeCodec<ZonedContainer, ZonedContainer.Builder>
{
    public static readonly ZonedContainerCodec Instance = new ZonedContainerCodec();

    private static readonly XmlQName ZonedValueName = new XmlQName("zonedValue");
    private static readonly XmlQName SecondValueName = new XmlQName("secondValue");

    private ZonedContainerCodec()
        : base(new XmlTypeDescriptor(
            "com.rosetta.test.ZonedContainer",
            new[]
            {
                new XmlPropertyDescriptor("zonedValue", ZonedValueName),
                new XmlPropertyDescriptor("secondValue", SecondValueName),
            },
            elementName: new XmlQName("ZonedContainer")))
    {
    }

    public override ZonedContainer.Builder CreateTypedBuilder() => ZonedContainer.CreateBuilder();

    protected override bool TryReadElement(
        ZonedContainer.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
    {
        switch (name.LocalName)
        {
            case "zonedValue":
            {
                DateTimeOffset value = XmlLexicalValue.ParseZonedDateTime(
                    context.ReadText(reader), out XmlZoneMetadata zone);
                builder.SetZonedValue(value);
                builder.RecordXmlZone("zonedValue", zone);
                context.RecordTemporalZone(zone);
                return true;
            }
            case "secondValue":
            {
                DateTimeOffset value = XmlLexicalValue.ParseZonedDateTime(
                    context.ReadText(reader), out XmlZoneMetadata zone);
                builder.SetSecondValue(value);
                builder.RecordXmlZone("secondValue", zone);
                context.RecordTemporalZone(zone);
                return true;
            }
            default:
                return false;
        }
    }

    protected override IReadOnlyList<string> PresentProperties(ZonedContainer value)
    {
        var present = new List<string>();
        if (value.ZonedValue != null)
        {
            present.Add("zonedValue");
        }
        if (value.SecondValue != null)
        {
            present.Add("secondValue");
        }
        return present;
    }

    protected override void WriteProperty(
        ZonedContainer value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
        switch (propertyName)
        {
            case "zonedValue" when value.ZonedValue != null:
                WriteZoned(writer, ZonedValueName, value, "zonedValue", value.ZonedValue.Value);
                break;
            case "secondValue" when value.SecondValue != null:
                WriteZoned(writer, SecondValueName, value, "secondValue", value.SecondValue.Value);
                break;
        }
    }

    /// <summary>
    /// Resolves the lexical timezone through <see cref="XmlTemporalMetadataExtensions.ResolveZone"/>,
    /// which falls back to the value's own offset whenever the metadata map has no entry for this
    /// property — including when the map exists because a <em>different</em> property was read.
    /// </summary>
    private static void WriteZoned(
        XmlObjectWriter writer, XmlQName name, ZonedContainer holder, string propertyName, DateTimeOffset value) =>
        writer.WriteTextElement(
            name,
            XmlLexicalValue.FormatZonedDateTime(
                value, XmlTemporalMetadataExtensions.ResolveZone(holder, propertyName, value)));
}
