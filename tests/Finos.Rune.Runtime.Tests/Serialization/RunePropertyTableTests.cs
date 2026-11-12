using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Serialization;

/// <summary>
/// The declaration-order view is the traversal order of Java's generated
/// process(RosettaPath, Processor), so these are parity tests, not implementation details:
/// a reorder here silently reorders every validation result over the whole test pack.
/// </summary>
public class RunePropertyTableTests
{
    [Fact]
    public void DeclarationOrder_ForASubtype_ListsTheBaseAttributesBeforeTheDerivedOnes()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(Derived));

        Assert.Equal(new[] { "attr", "other", "extra" }, WireNames(table.DeclarationOrderEntries));
    }

    [Fact]
    public void DeclarationOrder_ForAShadowedAttribute_KeepsTheBaseSlotAndTheDerivedDeclaration()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(Derived));

        RunePropertyTable.Entry attr = table.DeclarationOrderEntries[0];
        Assert.Equal("attr", attr.WireName);
        Assert.Equal(typeof(Derived), attr.Property.DeclaringType);
    }

    [Fact]
    public void DeclarationOrder_WhenShadowingTwoLevelsDeep_KeepsTheOriginalPositions()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(GrandDerived));

        Assert.Equal(new[] { "attr", "other", "extra" }, WireNames(table.DeclarationOrderEntries));
        Assert.Equal(typeof(GrandDerived), table.DeclarationOrderEntries[1].Property.DeclaringType);
    }

    [Fact]
    public void SerializationOrder_ForAKeyedType_HoistsMetaAndLeavesTheRestInDeclarationOrder()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(Keyed));

        Assert.Equal(new[] { "first", "second", "meta" }, WireNames(table.DeclarationOrderEntries));
        Assert.Equal(new[] { "meta", "first", "second" }, WireNames(table.Entries));
    }

    [Fact]
    public void DeclarationOrder_OnTheTypeThatDeclaresTheKey_PlacesMetaLast()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(Keyed));

        Assert.Equal("meta", table.DeclarationOrderEntries[^1].WireName);
    }

    [Fact]
    public void PathName_ForAGeneratedDataType_FallsBackToTheWireName()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(Derived));

        Assert.All(table.DeclarationOrderEntries, e => Assert.Equal(e.WireName, e.PathName));
    }

    [Fact]
    public void PathName_ForTheMetadataWrappers_UsesTheJavaMemberNamesNotTheWireNames()
    {
        RunePropertyTable field = RunePropertyTable.For(typeof(FieldWithMeta<string>));
        Assert.Equal(new[] { "@data", "meta" }, WireNames(field.DeclarationOrderEntries));
        Assert.Equal(new[] { "value", "meta" }, PathNames(field.DeclarationOrderEntries));

        RunePropertyTable reference = RunePropertyTable.For(typeof(ReferenceWithMeta<string>));
        Assert.Equal(
            new[] { "@data", "@ref", "@ref:external", "@ref:scoped" },
            WireNames(reference.DeclarationOrderEntries));
        Assert.Equal(
            new[] { "value", "globalReference", "externalReference", "reference" },
            PathNames(reference.DeclarationOrderEntries));
    }

    [Fact]
    public void PathName_ForTheScopedKeyOnMetaFields_IsLocation()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(MetaFields));

        RunePropertyTable.Entry scopedKey = Assert.Single(
            table.DeclarationOrderEntries, e => e.WireName == "@key:scoped");
        Assert.Equal("location", scopedKey.PathName);
    }

    [Fact]
    public void BuilderType_ForAClosedGenericWrapper_IsClosedAndResolvesEverySetter()
    {
        RunePropertyTable table = RunePropertyTable.For(typeof(FieldWithMeta<string>));

        Assert.False(table.BuilderType.ContainsGenericParameters);
        Assert.Equal(typeof(FieldWithMeta<string>.Builder), table.BuilderType);
        Assert.All(table.DeclarationOrderEntries, e => Assert.NotNull(e.Setter));
    }

    private static string[] WireNames(IReadOnlyList<RunePropertyTable.Entry> entries) =>
        entries.Select(e => e.WireName).ToArray();

    private static string[] PathNames(IReadOnlyList<RunePropertyTable.Entry> entries) =>
        entries.Select(e => e.PathName).ToArray();

    [RuneDataType("BaseT", "test.table", "0.0.0")]
    private class BaseT : RosettaModelObject
    {
        [RuneAttribute("attr")]
        public string? Attr { get; init; }

        [RuneAttribute("other")]
        public string? Other { get; init; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => new();

        public class Builder : RosettaModelObjectBuilder
        {
            public override BaseT Build() => new();

            public override Builder Prune() => this;

            public override bool HasData() => false;
        }
    }

    [RuneDataType("Derived", "test.table", "0.0.0")]
    private class Derived : BaseT
    {
        [RuneAttribute("attr")]
        public new string? Attr { get; init; }

        [RuneAttribute("extra")]
        public string? Extra { get; init; }
    }

    [RuneDataType("GrandDerived", "test.table", "0.0.0")]
    private sealed class GrandDerived : Derived
    {
        [RuneAttribute("other")]
        public new string? Other { get; init; }
    }

    [RuneDataType("Keyed", "test.table", "0.0.0")]
    private sealed class Keyed : RosettaModelObject
    {
        [RuneAttribute("first")]
        public string? First { get; init; }

        [RuneAttribute("second")]
        public string? Second { get; init; }

        [RuneAttribute("meta")]
        [RuneMetaType]
        public MetaFields? Meta { get; init; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => new();

        public sealed class Builder : RosettaModelObjectBuilder
        {
            public override Keyed Build() => new();

            public override Builder Prune() => this;

            public override bool HasData() => false;
        }
    }
}
