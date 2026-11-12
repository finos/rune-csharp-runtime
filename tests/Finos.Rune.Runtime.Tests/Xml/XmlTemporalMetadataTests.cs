using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// The XML-only lexical timezone contract: a generated property stays a
/// <see cref="DateTimeOffset"/>, while the exact timezone spelling the document used survives a
/// read, a Build, a ToBuilder, a Prune and a write.
/// </summary>
public class XmlTemporalMetadataTests
{
    private readonly XmlCodecRegistry _registry = TestCodecs.CreateRegistry();

    [Theory]
    [InlineData("2006-04-02T15:38:00", XmlTimeZoneKind.Absent, "")]
    [InlineData("2006-04-02T15:38:00Z", XmlTimeZoneKind.Utc, "Z")]
    [InlineData("2006-04-02T15:38:00+01:00", XmlTimeZoneKind.Offset, "+01:00")]
    [InlineData("2006-04-02T15:38:00+0100", XmlTimeZoneKind.Offset, "+0100")]
    [InlineData("2006-04-02T15:38:00+01", XmlTimeZoneKind.Offset, "+01")]
    [InlineData("2006-04-02T15:38:00-05:00", XmlTimeZoneKind.Offset, "-05:00")]
    public void Read_ForEveryLexicalTimezoneForm_RecordsItAndWritesItBackUnchanged(
        string lexical, XmlTimeZoneKind expectedKind, string expectedLexical)
    {
        string xml = "<ZonedContainer><zonedValue>" + lexical + "</zonedValue></ZonedContainer>";

        ZonedContainer actual = XmlDocumentReader.Read<ZonedContainer>(xml, _registry, out _);

        XmlZoneMetadata zone = actual.XmlTemporalMetadata!.GetOrAbsent("zonedValue");
        Assert.Equal(expectedKind, zone.Kind);
        Assert.Equal(expectedLexical, zone.Lexical);
        Assert.Equal(
            xml,
            XmlDocumentWriter.WriteToString(actual, _registry, new XmlWriteOptions { Indent = false }));
    }

    [Theory]
    [InlineData("+00:00")]
    [InlineData("-00:00")]
    public void Read_ForANumericZeroOffset_NormalisesToJavaUtc(string suffix)
    {
        ZonedContainer actual = XmlDocumentReader.Read<ZonedContainer>(
            "<ZonedContainer><zonedValue>2006-04-02T15:38:00" + suffix + "</zonedValue></ZonedContainer>",
            _registry,
            out _);

        Assert.Equal(XmlTimeZoneKind.Utc, actual.XmlTemporalMetadata!.GetOrAbsent("zonedValue").Kind);
        Assert.Equal(
            "<ZonedContainer><zonedValue>2006-04-02T15:38:00Z</zonedValue></ZonedContainer>",
            XmlDocumentWriter.WriteToString(actual, _registry, new XmlWriteOptions { Indent = false }));
    }

    [Fact]
    public void Read_ForAZonelessValue_KeepsThePublicPropertyAtZeroOffset()
    {
        ZonedContainer actual = XmlDocumentReader.Read<ZonedContainer>(
            "<ZonedContainer><zonedValue>2006-04-02T15:38:00</zonedValue></ZonedContainer>",
            _registry,
            out XmlReadContext context);

        Assert.Equal(new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.Zero), actual.ZonedValue);
        Assert.True(actual.XmlTemporalMetadata!.GetOrAbsent("zonedValue").IsAbsent);
        Assert.Equal(1, context.TemporalValueCount);
        Assert.Equal(1, context.ZoneLessTemporalValueCount);
    }

    [Fact]
    public void Read_ForAnOffsetValue_KeepsTheOffsetOnThePublicProperty()
    {
        ZonedContainer actual = XmlDocumentReader.Read<ZonedContainer>(
            "<ZonedContainer><zonedValue>2006-04-02T15:38:00+01:00</zonedValue></ZonedContainer>", _registry, out _);

        Assert.Equal(new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.FromHours(1)), actual.ZonedValue);
        Assert.Equal(TimeSpan.FromHours(1), actual.XmlTemporalMetadata!.GetOrAbsent("zonedValue").Offset);
    }

    [Fact]
    public void ToBuilder_ForAValueCarryingLexicalMetadata_RoundTripsItThroughBuildAndPrune()
    {
        ZonedContainer original = XmlDocumentReader.Read<ZonedContainer>(
            "<ZonedContainer><zonedValue>2006-04-02T15:38:00+0100</zonedValue></ZonedContainer>", _registry, out _);

        ZonedContainer rebuilt = original.ToBuilder().Prune().Build();

        Assert.Equal(original.XmlTemporalMetadata, rebuilt.XmlTemporalMetadata);
        Assert.Equal("+0100", rebuilt.XmlTemporalMetadata!.GetOrAbsent("zonedValue").Lexical);
    }

    [Fact]
    public void Write_ForAValueWithNoRecordedMetadata_FallsBackToTheOffsetOnTheProperty()
    {
        ZonedContainer value = ZonedContainer.CreateBuilder()
            .SetZonedValue(new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.FromHours(2)))
            .Build();

        string xml = XmlDocumentWriter.WriteToString(value, _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal("<ZonedContainer><zonedValue>2006-04-02T15:38:00+02:00</zonedValue></ZonedContainer>", xml);
    }

    [Fact]
    public void Prune_ForAnEmptyTemporalMetadataMap_CollapsesToNull()
    {
        var builder = ZonedContainer.CreateBuilder();
        builder.SetXmlTemporalMetadata(XmlTemporalMetadata.Empty);

        ZonedContainer built = builder.SetZonedValue(DateTimeOffset.UnixEpoch).Prune().Build();

        Assert.Null(built.XmlTemporalMetadata);
    }

    [Fact]
    public void With_ForASecondProperty_LeavesTheOriginalMapUnchanged()
    {
        XmlTemporalMetadata first = XmlTemporalMetadata.Empty.With("a", XmlZoneMetadata.Utc);
        XmlTemporalMetadata second = first.With("b", XmlZoneMetadata.OfOffset(TimeSpan.FromHours(-3)));

        Assert.Single(first.Properties);
        Assert.Equal(2, second.Properties.Count);
        Assert.Equal("-03:00", second.GetOrAbsent("b").Lexical);
        Assert.True(first.GetOrAbsent("b").IsAbsent);
    }

    [Fact]
    public void Without_ForTheOnlyRecordedProperty_LeavesAnEmptyMap()
    {
        XmlTemporalMetadata metadata = XmlTemporalMetadata.Empty.With("a", XmlZoneMetadata.Utc);

        XmlTemporalMetadata removed = metadata.Without("a");

        Assert.True(removed.IsEmpty);
        Assert.Null(removed.Prune());
        Assert.False(metadata.IsEmpty);
    }

    [Fact]
    public void CopyXmlZones_FromAListProperty_PreservesEveryIndexedEntry()
    {
        XmlTemporalMetadata sourceMetadata = XmlTemporalMetadata.Empty
            .With("dateTime[0]", XmlZoneMetadata.Absent)
            .With("dateTime[1]", XmlZoneMetadata.Utc);
        var source = new StubHolder(sourceMetadata);
        ZonedContainer.Builder target = ZonedContainer.CreateBuilder();

        XmlTemporalMetadataExtensions.CopyXmlZones(source, "dateTime", target, "copiedDateTime");

        Assert.Equal(2, target.XmlTemporalMetadata!.Properties.Count);
        Assert.True(target.XmlTemporalMetadata.TryGet(
            "copiedDateTime[0]", out XmlZoneMetadata firstZone));
        Assert.Equal(
            XmlZoneMetadata.Absent,
            firstZone);
        Assert.Equal(
            XmlZoneMetadata.Utc,
            target.XmlTemporalMetadata.GetOrAbsent("copiedDateTime[1]"));
    }

    [Fact]
    public void Equals_ForTwoMapsWithTheSameEntries_IsTrue()
    {
        XmlTemporalMetadata left = XmlTemporalMetadata.Empty
            .With("a", XmlZoneMetadata.Utc)
            .With("b", XmlZoneMetadata.Absent);
        XmlTemporalMetadata right = XmlTemporalMetadata.Empty
            .With("b", XmlZoneMetadata.Absent)
            .With("a", XmlZoneMetadata.Utc);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, right.With("a", XmlZoneMetadata.Absent));
    }

    [Fact]
    public void RecordXmlZone_OnABuilderWithNoMap_CreatesOne()
    {
        var builder = ZonedContainer.CreateBuilder();

        builder.RecordXmlZone("zonedValue", XmlZoneMetadata.Utc);

        Assert.Equal(XmlZoneMetadata.Utc, builder.XmlTemporalMetadata!.GetOrAbsent("zonedValue"));
    }

    [Fact]
    public void Write_ForPartialMetadata_FallsBackToTheOffsetOfTheUnrecordedProperty()
    {
        ZonedContainer.Builder builder = ZonedContainer.CreateBuilder()
            .SetZonedValue(new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.FromHours(1)))
            .SetSecondValue(new DateTimeOffset(2007, 5, 3, 9, 0, 0, TimeSpan.FromHours(2)));
        builder.RecordXmlZone("zonedValue", XmlZoneMetadata.OfOffset(TimeSpan.FromHours(1), "+0100"));

        string xml = XmlDocumentWriter.WriteToString(
            builder.Prune().Build(), _registry, new XmlWriteOptions { Indent = false });

        Assert.Equal(
            "<ZonedContainer>"
            + "<zonedValue>2006-04-02T15:38:00+0100</zonedValue>"
            + "<secondValue>2007-05-03T09:00:00+02:00</secondValue>"
            + "</ZonedContainer>",
            xml);
    }

    [Fact]
    public void Read_ForTwoTemporalProperties_RecordsEachLexicalFormSeparately()
    {
        const string Xml = "<ZonedContainer>"
            + "<zonedValue>2006-04-02T15:38:00+01</zonedValue>"
            + "<secondValue>2007-05-03T09:00:00Z</secondValue>"
            + "</ZonedContainer>";

        ZonedContainer actual = XmlDocumentReader.Read<ZonedContainer>(Xml, _registry, out _);

        Assert.Equal("+01", actual.XmlTemporalMetadata!.GetOrAbsent("zonedValue").Lexical);
        Assert.Equal(XmlTimeZoneKind.Utc, actual.XmlTemporalMetadata.GetOrAbsent("secondValue").Kind);
        Assert.Equal(Xml, XmlDocumentWriter.WriteToString(actual, _registry, new XmlWriteOptions { Indent = false }));
    }

    [Fact]
    public void ResolveZone_ForAnAbsentEntry_DerivesTheZoneFromTheValue()
    {
        XmlTemporalMetadata metadata = XmlTemporalMetadata.Empty.With("other", XmlZoneMetadata.Absent);
        var holder = new StubHolder(metadata);
        var offset = new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.FromHours(-5));

        XmlZoneMetadata resolved = XmlTemporalMetadataExtensions.ResolveZone(holder, "zonedValue", offset);

        Assert.Equal(XmlTimeZoneKind.Offset, resolved.Kind);
        Assert.Equal("-05:00", resolved.Lexical);
    }

    [Fact]
    public void ResolveZone_ForARecordedEntry_KeepsTheRecordedLexicalForm()
    {
        XmlTemporalMetadata metadata = XmlTemporalMetadata.Empty
            .With("zonedValue", XmlZoneMetadata.OfOffset(TimeSpan.FromHours(1), "+0100"));
        var holder = new StubHolder(metadata);
        var offset = new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.FromHours(1));

        Assert.Equal("+0100", XmlTemporalMetadataExtensions.ResolveZone(holder, "zonedValue", offset).Lexical);
    }

    [Fact]
    public void ResolveZone_ForARecordedAbsentEntry_StaysAbsent()
    {
        XmlTemporalMetadata metadata = XmlTemporalMetadata.Empty.With("zonedValue", XmlZoneMetadata.Absent);
        var holder = new StubHolder(metadata);
        var offset = new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.Zero);

        Assert.True(XmlTemporalMetadataExtensions.ResolveZone(holder, "zonedValue", offset).IsAbsent);
    }

    [Theory]
    [InlineData(0, XmlTimeZoneKind.Utc, "Z")]
    [InlineData(3, XmlTimeZoneKind.Offset, "+03:00")]
    public void ResolveZone_ForNoMetadataAtAll_DerivesTheZoneFromTheValue(
        int offsetHours, XmlTimeZoneKind expectedKind, string expectedLexical)
    {
        var offset = new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.FromHours(offsetHours));

        XmlZoneMetadata fromNullHolder = XmlTemporalMetadataExtensions.ResolveZone(null, "zonedValue", offset);
        XmlZoneMetadata fromEmptyHolder = XmlTemporalMetadataExtensions.ResolveZone(
            new StubHolder(null), "zonedValue", offset);

        Assert.Equal(expectedKind, fromNullHolder.Kind);
        Assert.Equal(expectedLexical, fromNullHolder.Lexical);
        Assert.Equal(fromNullHolder, fromEmptyHolder);
    }

    private sealed class StubHolder : IXmlTemporalMetadataHolder
    {
        internal StubHolder(XmlTemporalMetadata? metadata) => XmlTemporalMetadata = metadata;

        public XmlTemporalMetadata? XmlTemporalMetadata { get; }
    }
}
