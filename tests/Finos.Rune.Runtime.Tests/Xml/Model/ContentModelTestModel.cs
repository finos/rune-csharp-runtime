using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Tests.Xml.Model;

[RuneDataType("FpmlTextValue", "com.rosetta.test", "0.0.0")]
public sealed class FpmlTextValue : RosettaModelObject
{
    private FpmlTextValue(Builder builder)
    {
        Value = builder.Value;
        Scheme = builder.Scheme;
    }

    [RuneAttribute("value")]
    public string? Value { get; }

    [RuneAttribute("scheme")]
    public string? Scheme { get; }

    public static Builder CreateBuilder() => new Builder();

    public static FpmlTextValue Of(string value, string? scheme = null) =>
        new Builder().SetValue(value).SetScheme(scheme).Build();

    public override Builder ToBuilder() => new Builder().SetValue(Value).SetScheme(Scheme);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? Value { get; private set; }

        public string? Scheme { get; private set; }

        public Builder SetValue(string? value)
        {
            Value = value;
            return this;
        }

        public Builder SetScheme(string? scheme)
        {
            Scheme = scheme;
            return this;
        }

        public override FpmlTextValue Build() => new FpmlTextValue(this);

        public override Builder Prune() => this;

        public override bool HasData() => Value != null || Scheme != null;
    }
}

[RuneDataType("FpmlReference", "com.rosetta.test", "0.0.0")]
public sealed class FpmlReference : RosettaModelObject
{
    private FpmlReference(Builder builder)
    {
        Href = builder.Href;
    }

    [RuneAttribute("href")]
    public string? Href { get; }

    public static Builder CreateBuilder() => new Builder();

    public static FpmlReference Of(string href) => new Builder().SetHref(href).Build();

    public override Builder ToBuilder() => new Builder().SetHref(Href);

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? Href { get; private set; }

        public Builder SetHref(string? href)
        {
            Href = href;
            return this;
        }

        public override FpmlReference Build() => new FpmlReference(this);

        public override Builder Prune() => this;

        public override bool HasData() => Href != null;
    }
}

[RuneDataType("FpmlTradeIdentifierChoice", "com.rosetta.test", "0.0.0")]
public sealed class FpmlTradeIdentifierChoice : RosettaModelObject
{
    private FpmlTradeIdentifierChoice(Builder builder)
    {
        TradeId = builder.TradeId?.Build();
        VersionedTradeId = builder.VersionedTradeId?.Build();
    }

    [RuneAttribute("tradeId")]
    public FpmlTextValue? TradeId { get; }

    [RuneAttribute("versionedTradeId")]
    public FpmlTextValue? VersionedTradeId { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder()
        .SetTradeId(TradeId?.ToBuilder())
        .SetVersionedTradeId(VersionedTradeId?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public FpmlTextValue.Builder? TradeId { get; private set; }

        public FpmlTextValue.Builder? VersionedTradeId { get; private set; }

        public Builder SetTradeId(FpmlTextValue.Builder? value)
        {
            TradeId = value;
            return this;
        }

        public Builder SetVersionedTradeId(FpmlTextValue.Builder? value)
        {
            VersionedTradeId = value;
            return this;
        }

        public override FpmlTradeIdentifierChoice Build() => new FpmlTradeIdentifierChoice(this);

        public override Builder Prune()
        {
            if (TradeId != null && !TradeId.Prune().HasData())
            {
                TradeId = null;
            }
            if (VersionedTradeId != null && !VersionedTradeId.Prune().HasData())
            {
                VersionedTradeId = null;
            }
            return this;
        }

        public override bool HasData() => TradeId?.HasData() == true || VersionedTradeId?.HasData() == true;
    }
}

[RuneDataType("FpmlTradeIdentifier", "com.rosetta.test", "0.0.0")]
public sealed class FpmlTradeIdentifier : RosettaModelObject
{
    private FpmlTradeIdentifier(Builder builder)
    {
        Id = builder.Id;
        Issuer = builder.Issuer?.Build();
        TradeId = builder.TradeId?.Build();
        PartyReference = builder.PartyReference?.Build();
        AccountReference = builder.AccountReference?.Build();
        TradeIdentifierChoice = builder.TradeIdentifierChoice.Count == 0
            ? null
            : builder.TradeIdentifierChoice.Select(c => c.Build()).ToArray();
    }

    [RuneAttribute("id")]
    public string? Id { get; }

    [RuneAttribute("issuer")]
    public FpmlTextValue? Issuer { get; }

    [RuneAttribute("tradeId")]
    public FpmlTextValue? TradeId { get; }

    [RuneAttribute("partyReference")]
    public FpmlReference? PartyReference { get; }

    [RuneAttribute("accountReference")]
    public FpmlReference? AccountReference { get; }

    [RuneAttribute("tradeIdentifierChoice")]
    public IReadOnlyList<FpmlTradeIdentifierChoice>? TradeIdentifierChoice { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder()
            .SetId(Id)
            .SetIssuer(Issuer?.ToBuilder())
            .SetTradeId(TradeId?.ToBuilder())
            .SetPartyReference(PartyReference?.ToBuilder())
            .SetAccountReference(AccountReference?.ToBuilder());
        foreach (FpmlTradeIdentifierChoice choice in TradeIdentifierChoice ?? Array.Empty<FpmlTradeIdentifierChoice>())
        {
            builder.TradeIdentifierChoice.Add(choice.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? Id { get; private set; }

        public FpmlTextValue.Builder? Issuer { get; private set; }

        public FpmlTextValue.Builder? TradeId { get; private set; }

        public FpmlReference.Builder? PartyReference { get; private set; }

        public FpmlReference.Builder? AccountReference { get; private set; }

        public List<FpmlTradeIdentifierChoice.Builder> TradeIdentifierChoice { get; } = new();

        public Builder SetId(string? id)
        {
            Id = id;
            return this;
        }

        public Builder SetIssuer(FpmlTextValue.Builder? value)
        {
            Issuer = value;
            return this;
        }

        public Builder SetTradeId(FpmlTextValue.Builder? value)
        {
            TradeId = value;
            return this;
        }

        public Builder SetPartyReference(FpmlReference.Builder? value)
        {
            PartyReference = value;
            return this;
        }

        public Builder SetAccountReference(FpmlReference.Builder? value)
        {
            AccountReference = value;
            return this;
        }

        public FpmlTradeIdentifierChoice.Builder AddTradeIdentifierChoice()
        {
            var added = Model.FpmlTradeIdentifierChoice.CreateBuilder();
            TradeIdentifierChoice.Add(added);
            return added;
        }

        public override FpmlTradeIdentifier Build() => new FpmlTradeIdentifier(this);

        public override Builder Prune()
        {
            if (Issuer != null && !Issuer.Prune().HasData())
            {
                Issuer = null;
            }
            if (TradeId != null && !TradeId.Prune().HasData())
            {
                TradeId = null;
            }
            if (PartyReference != null && !PartyReference.Prune().HasData())
            {
                PartyReference = null;
            }
            if (AccountReference != null && !AccountReference.Prune().HasData())
            {
                AccountReference = null;
            }
            PruneList(TradeIdentifierChoice);
            return this;
        }

        public override bool HasData() =>
            Id != null || Issuer?.HasData() == true || TradeId?.HasData() == true
            || PartyReference?.HasData() == true || AccountReference?.HasData() == true
            || TradeIdentifierChoice.Count > 0;
    }
}

[RuneDataType("SchemaLocationContainer", "com.rosetta.test", "0.0.0")]
public sealed class SchemaLocationContainer : RosettaModelObject
{
    private SchemaLocationContainer(Builder builder)
    {
        TradeIdentifier = builder.TradeIdentifier?.Build();
    }

    [RuneAttribute("tradeIdentifier")]
    public FpmlTradeIdentifier? TradeIdentifier { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetTradeIdentifier(TradeIdentifier?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public FpmlTradeIdentifier.Builder? TradeIdentifier { get; private set; }

        public Builder SetTradeIdentifier(FpmlTradeIdentifier.Builder? value)
        {
            TradeIdentifier = value;
            return this;
        }

        public override SchemaLocationContainer Build() => new SchemaLocationContainer(this);

        public override Builder Prune()
        {
            if (TradeIdentifier != null && !TradeIdentifier.Prune().HasData())
            {
                TradeIdentifier = null;
            }
            return this;
        }

        public override bool HasData() => TradeIdentifier?.HasData() == true;
    }
}

/// <summary>A two-leaf entry used by the multi-leaf, ALL and multi-layer virtual containers.</summary>
[RuneDataType("LeafEntry", "com.rosetta.test", "0.0.0")]
public sealed class LeafEntry : RosettaModelObject
{
    private LeafEntry(Builder builder)
    {
        FirstValue = builder.FirstValue?.Build();
        SecondValue = builder.SecondValue?.Build();
    }

    [RuneAttribute("firstValue")]
    public FpmlTextValue? FirstValue { get; }

    [RuneAttribute("secondValue")]
    public FpmlTextValue? SecondValue { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder()
        .SetFirstValue(FirstValue?.ToBuilder())
        .SetSecondValue(SecondValue?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public FpmlTextValue.Builder? FirstValue { get; private set; }

        public FpmlTextValue.Builder? SecondValue { get; private set; }

        public Builder SetFirstValue(FpmlTextValue.Builder? value)
        {
            FirstValue = value;
            return this;
        }

        public Builder SetSecondValue(FpmlTextValue.Builder? value)
        {
            SecondValue = value;
            return this;
        }

        public override LeafEntry Build() => new LeafEntry(this);

        public override Builder Prune()
        {
            if (FirstValue != null && !FirstValue.Prune().HasData())
            {
                FirstValue = null;
            }
            if (SecondValue != null && !SecondValue.Prune().HasData())
            {
                SecondValue = null;
            }
            return this;
        }

        public override bool HasData() => FirstValue?.HasData() == true || SecondValue?.HasData() == true;
    }
}

/// <summary>A container of repeated VIRTUAL <see cref="LeafEntry"/> groups.</summary>
[RuneDataType("LeafContainer", "com.rosetta.test", "0.0.0")]
public sealed class LeafContainer : RosettaModelObject
{
    private LeafContainer(Builder builder)
    {
        Entry = builder.Entry.Count == 0 ? null : builder.Entry.Select(e => e.Build()).ToArray();
    }

    [RuneAttribute("entry")]
    public IReadOnlyList<LeafEntry>? Entry { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (LeafEntry entry in Entry ?? Array.Empty<LeafEntry>())
        {
            builder.Entry.Add(entry.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public List<LeafEntry.Builder> Entry { get; } = new();

        public LeafEntry.Builder AddEntry()
        {
            var added = LeafEntry.CreateBuilder();
            Entry.Add(added);
            return added;
        }

        public override LeafContainer Build() => new LeafContainer(this);

        public override Builder Prune()
        {
            PruneList(Entry);
            return this;
        }

        public override bool HasData() => Entry.Count > 0;
    }
}

[RuneDataType("MultiLayerOuter", "com.rosetta.test", "0.0.0")]
public sealed class MultiLayerOuter : RosettaModelObject
{
    private MultiLayerOuter(Builder builder)
    {
        Inner = builder.Inner.Count == 0 ? null : builder.Inner.Select(i => i.Build()).ToArray();
    }

    [RuneAttribute("inner")]
    public IReadOnlyList<LeafEntry>? Inner { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (LeafEntry inner in Inner ?? Array.Empty<LeafEntry>())
        {
            builder.Inner.Add(inner.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public List<LeafEntry.Builder> Inner { get; } = new();

        public LeafEntry.Builder AddInner()
        {
            var added = LeafEntry.CreateBuilder();
            Inner.Add(added);
            return added;
        }

        public override MultiLayerOuter Build() => new MultiLayerOuter(this);

        public override Builder Prune()
        {
            PruneList(Inner);
            return this;
        }

        public override bool HasData() => Inner.Count > 0;
    }
}

[RuneDataType("MultiLayerContainer", "com.rosetta.test", "0.0.0")]
public sealed class MultiLayerContainer : RosettaModelObject
{
    private MultiLayerContainer(Builder builder)
    {
        Outer = builder.Outer.Count == 0 ? null : builder.Outer.Select(o => o.Build()).ToArray();
    }

    [RuneAttribute("outer")]
    public IReadOnlyList<MultiLayerOuter>? Outer { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        foreach (MultiLayerOuter outer in Outer ?? Array.Empty<MultiLayerOuter>())
        {
            builder.Outer.Add(outer.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public List<MultiLayerOuter.Builder> Outer { get; } = new();

        public MultiLayerOuter.Builder AddOuter()
        {
            var added = MultiLayerOuter.CreateBuilder();
            Outer.Add(added);
            return added;
        }

        public override MultiLayerContainer Build() => new MultiLayerContainer(this);

        public override Builder Prune()
        {
            PruneList(Outer);
            return this;
        }

        public override bool HasData() => Outer.Count > 0;
    }
}

[RuneDataType("AnyVirtualEntry", "com.rosetta.test", "0.0.0")]
public sealed class AnyVirtualEntry : RosettaModelObject
{
    private AnyVirtualEntry(Builder builder)
    {
        WildcardValue = builder.WildcardValue?.Build();
    }

    [RuneAttribute("wildcardValue")]
    public FpmlTextValue? WildcardValue { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder() => new Builder().SetWildcardValue(WildcardValue?.ToBuilder());

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public FpmlTextValue.Builder? WildcardValue { get; private set; }

        public Builder SetWildcardValue(FpmlTextValue.Builder? value)
        {
            WildcardValue = value;
            return this;
        }

        public override AnyVirtualEntry Build() => new AnyVirtualEntry(this);

        public override Builder Prune()
        {
            if (WildcardValue != null && !WildcardValue.Prune().HasData())
            {
                WildcardValue = null;
            }
            return this;
        }

        public override bool HasData() => WildcardValue?.HasData() == true;
    }
}

[RuneDataType("AnyVirtualContainer", "com.rosetta.test", "0.0.0")]
public sealed class AnyVirtualContainer : RosettaModelObject
{
    private AnyVirtualContainer(Builder builder)
    {
        KnownValue = builder.KnownValue?.Build();
        Entry = builder.Entry.Count == 0 ? null : builder.Entry.Select(e => e.Build()).ToArray();
    }

    [RuneAttribute("knownValue")]
    public FpmlTextValue? KnownValue { get; }

    [RuneAttribute("entry")]
    public IReadOnlyList<AnyVirtualEntry>? Entry { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder().SetKnownValue(KnownValue?.ToBuilder());
        foreach (AnyVirtualEntry entry in Entry ?? Array.Empty<AnyVirtualEntry>())
        {
            builder.Entry.Add(entry.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public FpmlTextValue.Builder? KnownValue { get; private set; }

        public List<AnyVirtualEntry.Builder> Entry { get; } = new();

        public Builder SetKnownValue(FpmlTextValue.Builder? value)
        {
            KnownValue = value;
            return this;
        }

        public AnyVirtualEntry.Builder AddEntry()
        {
            var added = AnyVirtualEntry.CreateBuilder();
            Entry.Add(added);
            return added;
        }

        public override AnyVirtualContainer Build() => new AnyVirtualContainer(this);

        public override Builder Prune()
        {
            if (KnownValue != null && !KnownValue.Prune().HasData())
            {
                KnownValue = null;
            }
            PruneList(Entry);
            return this;
        }

        public override bool HasData() => KnownValue?.HasData() == true || Entry.Count > 0;
    }
}
