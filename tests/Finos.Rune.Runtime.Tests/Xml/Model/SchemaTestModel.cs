using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Xml;

namespace Finos.Rune.Runtime.Tests.Xml.Model;

// Mirrors the rune-common XML test schema, so the fixtures match the Java implementation's.

public enum UnitEnum
{
    Meter,
    Kilogram,
}

[RuneDataType("Foo", "com.rosetta.test", "0.0.0")]
public sealed class Foo : RosettaModelObject
{
    private Foo(Builder builder)
    {
        XmlValue = builder.XmlValue;
        Attr1 = builder.Attr1.Count == 0 ? null : builder.Attr1.ToArray();
    }

    [RuneAttribute("xmlValue")]
    public string? XmlValue { get; }

    [RuneAttribute("attr1")]
    public IReadOnlyList<string>? Attr1 { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder().SetXmlValue(XmlValue);
        foreach (string value in Attr1 ?? Array.Empty<string>())
        {
            builder.AddAttr1(value);
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? XmlValue { get; private set; }

        public List<string> Attr1 { get; } = new();

        public Builder SetXmlValue(string? value)
        {
            XmlValue = value;
            return this;
        }

        public Builder AddAttr1(string value)
        {
            Attr1.Add(value);
            return this;
        }

        public override Foo Build() => new Foo(this);

        public override Builder Prune() => this;

        public override bool HasData() => XmlValue != null || Attr1.Count > 0;
    }
}

[RuneDataType("Measure", "com.rosetta.test", "0.0.0")]
public sealed class Measure : RosettaModelObject
{
    private Measure(Builder builder)
    {
        Value = builder.Value;
        Unit = builder.Unit;
    }

    [RuneAttribute("value")]
    public decimal? Value { get; }

    [RuneAttribute("unit")]
    public UnitEnum? Unit { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetValue(Value).SetUnit(Unit);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public decimal? Value { get; private set; }

        public UnitEnum? Unit { get; private set; }

        public Builder SetValue(decimal? value)
        {
            Value = value;
            return this;
        }

        public Builder SetUnit(UnitEnum? unit)
        {
            Unit = unit;
            return this;
        }

        public override Measure Build() => new Measure(this);

        public override Builder Prune() => this;

        public override bool HasData() => Value != null || Unit != null;
    }
}

[RuneDataType("TopLevel", "com.rosetta.test", "0.0.0")]
public sealed class TopLevel : RosettaModelObject
{
    private TopLevel(Builder builder)
    {
        Attr = builder.Attr?.Build();
        Value = builder.Value?.Build();
    }

    [RuneAttribute("attr")]
    public Foo? Attr { get; }

    [RuneAttribute("value")]
    public Measure? Value { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetAttr(Attr?.ToBuilder()).SetValue(Value?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public Foo.Builder? Attr { get; private set; }

        public Measure.Builder? Value { get; private set; }

        public Builder SetAttr(Foo.Builder? attr)
        {
            Attr = attr;
            return this;
        }

        public Builder SetValue(Measure.Builder? value)
        {
            Value = value;
            return this;
        }

        public override TopLevel Build() => new TopLevel(this);

        public override Builder Prune()
        {
            if (Attr != null && !Attr.Prune().HasData())
            {
                Attr = null;
            }
            if (Value != null && !Value.Prune().HasData())
            {
                Value = null;
            }
            return this;
        }

        public override bool HasData() => Attr?.HasData() == true || Value?.HasData() == true;
    }
}

[RuneDataType("TopLevelExtension", "com.rosetta.test", "0.0.0")]
public sealed class TopLevelExtension : RosettaModelObject
{
    private TopLevelExtension(Builder builder)
    {
        Attr = builder.Attr?.Build();
        Value = builder.Value?.Build();
        DocumentExtensionAttr = builder.DocumentExtensionAttr;
    }

    [RuneAttribute("attr")]
    public Foo? Attr { get; }

    [RuneAttribute("value")]
    public Measure? Value { get; }

    [RuneAttribute("documentExtensionAttr")]
    public string? DocumentExtensionAttr { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder()
        .SetAttr(Attr?.ToBuilder())
        .SetValue(Value?.ToBuilder())
        .SetDocumentExtensionAttr(DocumentExtensionAttr);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public Foo.Builder? Attr { get; private set; }

        public Measure.Builder? Value { get; private set; }

        public string? DocumentExtensionAttr { get; private set; }

        public Builder SetAttr(Foo.Builder? attr)
        {
            Attr = attr;
            return this;
        }

        public Builder SetValue(Measure.Builder? value)
        {
            Value = value;
            return this;
        }

        public Builder SetDocumentExtensionAttr(string? value)
        {
            DocumentExtensionAttr = value;
            return this;
        }

        public override TopLevelExtension Build() => new TopLevelExtension(this);

        public override Builder Prune()
        {
            if (Attr != null && !Attr.Prune().HasData())
            {
                Attr = null;
            }
            if (Value != null && !Value.Prune().HasData())
            {
                Value = null;
            }
            return this;
        }

        public override bool HasData() =>
            Attr?.HasData() == true || Value?.HasData() == true || DocumentExtensionAttr != null;
    }
}

[RuneDataType("MulticardinalityContainer", "com.rosetta.test", "0.0.0")]
public sealed class MulticardinalityContainer : RosettaModelObject
{
    private MulticardinalityContainer(Builder builder)
    {
        Foo = builder.Foo.Count == 0 ? null : builder.Foo.Select(f => f.Build()).ToArray();
    }

    [RuneAttribute("foo")]
    public IReadOnlyList<Foo>? Foo { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (Foo value in Foo ?? Array.Empty<Foo>())
        {
            builder.Foo.Add(value.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public List<Foo.Builder> Foo { get; } = new();

        public Foo.Builder AddFoo()
        {
            var added = Model.Foo.CreateBuilder();
            Foo.Add(added);
            return added;
        }

        public override MulticardinalityContainer Build() => new MulticardinalityContainer(this);

        public override Builder Prune()
        {
            PruneList(Foo);
            return this;
        }

        public override bool HasData() => Foo.Count > 0;
    }
}

[RuneDataType("PartyNameModel", "com.rosetta.test", "0.0.0")]
public sealed class PartyNameModel : RosettaModelObject
{
    private PartyNameModel(Builder builder)
    {
        PartyName = builder.PartyName;
    }

    [RuneAttribute("partyName")]
    public string? PartyName { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetPartyName(PartyName);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? PartyName { get; private set; }

        public Builder SetPartyName(string? value)
        {
            PartyName = value;
            return this;
        }

        public override PartyNameModel Build() => new PartyNameModel(this);

        public override Builder Prune() => this;

        public override bool HasData() => PartyName != null;
    }
}

[RuneDataType("PartyModel", "com.rosetta.test", "0.0.0")]
public sealed class PartyModel : RosettaModelObject
{
    private PartyModel(Builder builder)
    {
        PartyId = builder.PartyId.Count == 0 ? null : builder.PartyId.ToArray();
    }

    [RuneAttribute("partyId")]
    public IReadOnlyList<string>? PartyId { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (string value in PartyId ?? Array.Empty<string>())
        {
            builder.AddPartyId(value);
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public List<string> PartyId { get; } = new();

        public Builder AddPartyId(string value)
        {
            PartyId.Add(value);
            return this;
        }

        public override PartyModel Build() => new PartyModel(this);

        public override Builder Prune() => this;

        public override bool HasData() => PartyId.Count > 0;
    }
}

[RuneDataType("Party", "com.rosetta.test", "0.0.0")]
public sealed class Party : RosettaModelObject
{
    private Party(Builder builder)
    {
        PartyNameModel = builder.PartyNameModel?.Build();
        PartyModel = builder.PartyModel?.Build();
    }

    [RuneAttribute("partyNameModel")]
    public PartyNameModel? PartyNameModel { get; }

    [RuneAttribute("partyModel")]
    public PartyModel? PartyModel { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder()
        .SetPartyNameModel(PartyNameModel?.ToBuilder())
        .SetPartyModel(PartyModel?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public PartyNameModel.Builder? PartyNameModel { get; private set; }

        public PartyModel.Builder? PartyModel { get; private set; }

        public Builder SetPartyNameModel(PartyNameModel.Builder? value)
        {
            PartyNameModel = value;
            return this;
        }

        public Builder SetPartyModel(PartyModel.Builder? value)
        {
            PartyModel = value;
            return this;
        }

        public PartyNameModel.Builder GetOrCreatePartyNameModel() =>
            PartyNameModel ??= Model.PartyNameModel.CreateBuilder();

        public PartyModel.Builder GetOrCreatePartyModel() => PartyModel ??= Model.PartyModel.CreateBuilder();

        public override Party Build() => new Party(this);

        public override Builder Prune()
        {
            if (PartyNameModel != null && !PartyNameModel.Prune().HasData())
            {
                PartyNameModel = null;
            }
            if (PartyModel != null && !PartyModel.Prune().HasData())
            {
                PartyModel = null;
            }
            return this;
        }

        public override bool HasData() => PartyNameModel?.HasData() == true || PartyModel?.HasData() == true;
    }
}

[RuneDataType("TypeWithTypeElement", "com.rosetta.test", "0.0.0")]
public sealed class TypeWithTypeElement : RosettaModelObject
{
    private TypeWithTypeElement(Builder builder)
    {
        FirstElement = builder.FirstElement;
        Type = builder.Type;
    }

    [RuneAttribute("firstElement")]
    public string? FirstElement { get; }

    [RuneAttribute("type")]
    public string? Type { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetFirstElement(FirstElement).SetType(Type);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? FirstElement { get; private set; }

        public string? Type { get; private set; }

        public Builder SetFirstElement(string? value)
        {
            FirstElement = value;
            return this;
        }

        public Builder SetType(string? value)
        {
            Type = value;
            return this;
        }

        public override TypeWithTypeElement Build() => new TypeWithTypeElement(this);

        public override Builder Prune() => this;

        public override bool HasData() => FirstElement != null || Type != null;
    }
}

[RuneDataType("TimeContainer", "com.rosetta.test", "0.0.0")]
public sealed class TimeContainer : RosettaModelObject
{
    private TimeContainer(Builder builder)
    {
        TimeValue = builder.TimeValue;
    }

    [RuneAttribute("timeValue")]
    public TimeOnly? TimeValue { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetTimeValue(TimeValue);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public TimeOnly? TimeValue { get; private set; }

        public Builder SetTimeValue(TimeOnly? value)
        {
            TimeValue = value;
            return this;
        }

        public override TimeContainer Build() => new TimeContainer(this);

        public override Builder Prune() => this;

        public override bool HasData() => TimeValue != null;
    }
}

/// <summary>
/// A container whose single <c>zonedDateTime</c> property stays a <see cref="DateTimeOffset"/>
/// while the lexical timezone it was written with rides along in XML-only metadata.
/// </summary>
[RuneDataType("ZonedContainer", "com.rosetta.test", "0.0.0")]
public sealed class ZonedContainer : RosettaModelObject, IXmlTemporalMetadataHolder
{
    private ZonedContainer(Builder builder)
    {
        ZonedValue = builder.ZonedValue;
        SecondValue = builder.SecondValue;
        XmlTemporalMetadata = builder.XmlTemporalMetadata;
    }

    [RuneAttribute("zonedValue")]
    public DateTimeOffset? ZonedValue { get; }

    /// <summary>A second temporal property, so partial metadata is representable.</summary>
    [RuneAttribute("secondValue")]
    public DateTimeOffset? SecondValue { get; }

    [RuneIgnore]
    public XmlTemporalMetadata? XmlTemporalMetadata { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder().SetZonedValue(ZonedValue).SetSecondValue(SecondValue);
        builder.SetXmlTemporalMetadata(XmlTemporalMetadata);
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder, IXmlTemporalMetadataBuilder
    {
        public DateTimeOffset? ZonedValue { get; private set; }

        public DateTimeOffset? SecondValue { get; private set; }

        public XmlTemporalMetadata? XmlTemporalMetadata { get; private set; }

        public Builder SetZonedValue(DateTimeOffset? value)
        {
            ZonedValue = value;
            return this;
        }

        public Builder SetSecondValue(DateTimeOffset? value)
        {
            SecondValue = value;
            return this;
        }

        public void SetXmlTemporalMetadata(XmlTemporalMetadata? metadata) => XmlTemporalMetadata = metadata;

        public override ZonedContainer Build() => new ZonedContainer(this);

        public override Builder Prune()
        {
            XmlTemporalMetadata = XmlTemporalMetadata?.Prune();
            return this;
        }

        public override bool HasData() => ZonedValue != null || SecondValue != null;
    }
}
