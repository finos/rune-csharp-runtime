using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.PostProcess;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Xunit;

namespace Finos.Rune.Runtime.Tests.PostProcess;

/// <summary>Ports rune-common 12.3.1's PathCountProcessorTest cases.</summary>
public class PathCountProcessorTests
{
    private static readonly Measure Price = Measure.CreateBuilder().SetValue(1.234m).Build();

    [Fact]
    public void Count_ForASingleBasicValue_CollectsItsPath()
    {
        PathCountReport report = PathCountProcessor.Process(Price);

        Assert.Equal(1, report.Count);
        Assert.Equal(1.234m, report.CollectedPaths[RosettaPath.ValueOf("value")]);
    }

    [Fact]
    public void Count_ForResolvedGlobalAndExternalReference_CollectsOnlyExternalReference()
    {
        ResolvablePriceQuantity value = ResolvablePriceQuantity.CreateBuilder()
            .SetResolvedPrice(ReferenceWithMeta<Measure>.CreateBuilder()
                .SetValue(Price)
                .SetGlobalReference("global")
                .SetExternalReference("external")
                .Build())
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(1, report.Count);
        Assert.Equal(
            "external",
            report.CollectedPaths[RosettaPath.ValueOf("resolvedPrice.externalReference")]);
    }

    [Fact]
    public void Count_ForResolvedGlobalReferenceOnly_CollectsNoValuePath()
    {
        ResolvablePriceQuantity value = ResolvablePriceQuantity.CreateBuilder()
            .SetResolvedPrice(ReferenceWithMeta<Measure>.CreateBuilder()
                .SetValue(Price)
                .SetGlobalReference("global")
                .Build())
            .Build();

        Assert.Empty(PathCountProcessor.Process(value).CollectedPaths);
    }

    [Fact]
    public void Count_ForResolvedExternalReferenceOnly_CollectsOnlyExternalReference()
    {
        ResolvablePriceQuantity value = ResolvablePriceQuantity.CreateBuilder()
            .SetResolvedPrice(ReferenceWithMeta<Measure>.CreateBuilder()
                .SetValue(Price)
                .SetExternalReference("external")
                .Build())
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(1, report.Count);
        Assert.Equal(
            "external",
            report.CollectedPaths[RosettaPath.ValueOf("resolvedPrice.externalReference")]);
    }

    [Fact]
    public void Count_ForResolvedScopedReference_CollectsOnlyScopedReference()
    {
        ResolvablePriceQuantity value = ResolvablePriceQuantity.CreateBuilder()
            .SetResolvedPrice(ReferenceWithMeta<Measure>.CreateBuilder()
                .SetValue(Price)
                .SetScopedReference(Reference.CreateBuilder().SetScopedReference("address").Build())
                .Build())
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(1, report.Count);
        Assert.Equal(
            "address",
            report.CollectedPaths[RosettaPath.ValueOf("resolvedPrice.reference.address")]);
    }

    [Fact]
    public void Count_ForAValueWithoutReference_CollectsValuePaths()
    {
        ResolvablePriceQuantity value = ResolvablePriceQuantity.CreateBuilder()
            .SetResolvedPrice(ReferenceWithMeta<Measure>.CreateBuilder().SetValue(Price).Build())
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(1, report.Count);
        Assert.Equal(
            1.234m,
            report.CollectedPaths[RosettaPath.ValueOf("resolvedPrice.value.value")]);
    }

    [Fact]
    public void Count_ForFieldMetadataWithGlobalAndExternalKey_CollectsValueAndExternalKey()
    {
        FieldWithMeta<Measure> value = FieldWithMeta<Measure>.CreateBuilder()
            .SetValue(Price)
            .SetMeta(MetaFields.CreateBuilder()
                .SetGlobalKey("global")
                .SetExternalKey("external"))
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(2, report.Count);
        Assert.Equal(1.234m, report.CollectedPaths[RosettaPath.ValueOf("value.value")]);
        Assert.Equal("external", report.CollectedPaths[RosettaPath.ValueOf("meta.externalKey")]);
    }

    [Fact]
    public void Count_ForFieldMetadataWithScopedKey_CollectsValueAndScopedKey()
    {
        FieldWithMeta<Measure> value = FieldWithMeta<Measure>.CreateBuilder()
            .SetValue(Price)
            .SetMeta(MetaFields.CreateBuilder().SetScopedKey("location"))
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(2, report.Count);
        Assert.Equal(1.234m, report.CollectedPaths[RosettaPath.ValueOf("value.value")]);
        Assert.Equal("location", report.CollectedPaths[RosettaPath.ValueOf("meta.location")]);
    }

    [Fact]
    public void Count_ForAListOfBasicValues_IndexesEveryValue()
    {
        Foo value = Foo.CreateBuilder()
            .SetXmlValue("root")
            .AddAttr1("first")
            .AddAttr1("second")
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(3, report.Count);
        Assert.Equal("root", report.CollectedPaths[RosettaPath.ValueOf("xmlValue")]);
        Assert.Equal("first", report.CollectedPaths[RosettaPath.ValueOf("attr1(0)")]);
        Assert.Equal("second", report.CollectedPaths[RosettaPath.ValueOf("attr1(1)")]);
    }

    [Fact]
    public void Count_ForAListOfModelValues_IndexesEveryDescendant()
    {
        Zoo value = Zoo.CreateBuilder()
            .AddAnimal(Goat.CreateBuilder().SetName("goat"))
            .AddAnimal(Cow.CreateBuilder().SetName("cow"))
            .Build();

        PathCountReport report = PathCountProcessor.Process(value);

        Assert.Equal(2, report.Count);
        Assert.Equal("goat", report.CollectedPaths[RosettaPath.ValueOf("animal(0).name")]);
        Assert.Equal("cow", report.CollectedPaths[RosettaPath.ValueOf("animal(1).name")]);
    }

    [RuneDataType("ResolvablePriceQuantity", "com.rosetta.test", "0.0.0")]
    private sealed class ResolvablePriceQuantity : RosettaModelObject
    {
        private ResolvablePriceQuantity(Builder builder)
        {
            ResolvedPrice = builder.ResolvedPrice;
        }

        [RuneAttribute("resolvedPrice")]
        public ReferenceWithMeta<Measure>? ResolvedPrice { get; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => new Builder().SetResolvedPrice(ResolvedPrice);

        public sealed class Builder : RosettaModelObjectBuilder
        {
            public ReferenceWithMeta<Measure>? ResolvedPrice { get; private set; }

            public Builder SetResolvedPrice(ReferenceWithMeta<Measure>? resolvedPrice)
            {
                ResolvedPrice = resolvedPrice;
                return this;
            }

            public override ResolvablePriceQuantity Build() => new(this);

            public override Builder Prune() => this;

            public override bool HasData() => ResolvedPrice != null;
        }
    }
}
