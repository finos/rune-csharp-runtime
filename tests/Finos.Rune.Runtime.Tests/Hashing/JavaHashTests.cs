using Finos.Rune.Runtime.Hashing;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Hashing;

/// <summary>
/// <see cref="JavaHash"/> against values printed by a real JDK 21.
/// </summary>
/// <remarks>
/// Every expectation here was produced by running the corresponding Java expression, not derived by
/// reading the algorithm — the whole point of the type is agreement with Java, so anything computed
/// the same way twice by the same reasoning would prove nothing. The probe that produced them is
/// recorded in <c>harness/NOTES.md</c> so it can be re-run if rune-common's generator ever changes.
/// </remarks>
public class JavaHashTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 97)]
    [InlineData("abc", 96354)]
    [InlineData("USD", 84326)]
    [InlineData("ISDA_1991", 1995033944)]
    [InlineData("InterestRatePayout-with-a-fairly-long-attribute-name", 2049553927)]
    public void String_FromEmptyToOverflowingLength_MatchesJavaStringHashCode(string value, int expected) =>
        Assert.Equal(expected, JavaHash.Of(value));

    [Fact]
    public void Bool_ForTrueAndFalse_MatchesJavaBooleanHashCode()
    {
        Assert.Equal(1231, JavaHash.Of(true));
        Assert.Equal(1237, JavaHash.Of(false));
    }

    [Fact]
    public void Int_ForZeroAndANegativeValue_IsTheValueItself()
    {
        Assert.Equal(0, JavaHash.Of(0));
        Assert.Equal(-7, JavaHash.Of(-7));
    }

    [Theory]
    [InlineData(2020, 1, 15, 4137039)]
    [InlineData(1998, 12, 31, 4092703)]
    [InlineData(2049, 6, 1, 4194689)]
    public void Date_AcrossTheYearMask_MatchesJavaLocalDateHashCode(int year, int month, int day, int expected) =>
        Assert.Equal(expected, JavaHash.Of(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(13, 45, 30, 0, 437152012)]
    [InlineData(23, 59, 59, 999, -1858079916)]
    public void Time_AcrossTheFullDay_MatchesJavaLocalTimeHashCode(int h, int m, int s, int ms, int expected) =>
        Assert.Equal(expected, JavaHash.Of(new TimeOnly(h, m, s, ms)));

    [Fact]
    public void DateTime_ForAZonelessValue_MatchesJavaLocalDateTimeHashCode() =>
        Assert.Equal(439437635, JavaHash.Of(new DateTime(2020, 1, 15, 13, 45, 30, DateTimeKind.Unspecified)));

    [Theory]
    [InlineData(0, 439437593)]
    [InlineData(60, 1445640093)]
    [InlineData(-330, -1438021618)]
    public void DateTimeOffset_AcrossUtcAndSignedOffsets_MatchesRuneIntegerHashGenerator(
        int offsetMinutes,
        int expected)
    {
        var value = new DateTimeOffset(2020, 1, 15, 13, 45, 30, TimeSpan.FromMinutes(offsetMinutes));

        Assert.Equal(expected, JavaHash.Of(value));
    }

    [Fact]
    public void Hash_ForAnXmlZonelessValue_UsesTheSyntheticUnknownZone()
    {
        var value = new DateTimeOffset(2000, 8, 1, 8, 57, 0, TimeSpan.Zero);
        ZonedContainer.Builder builder = ZonedContainer.CreateBuilder().SetZonedValue(value);
        builder.RecordXmlZone("zonedValue", XmlZoneMetadata.Absent);
        ZonedContainer zoneless = builder.Build();

        Assert.Equal(((uint)JavaHash.Of(value, "Unknown")).ToString("x"), GlobalKeyHasher.Hash(zoneless));
        Assert.NotEqual(((uint)JavaHash.Of(value)).ToString("x"), GlobalKeyHasher.Hash(zoneless));
    }

    [Fact]
    public void ZoneOffsetId_ForUtcAndSignedOffsets_RendersAndHashesAsJavaGetId()
    {
        Assert.Equal("Z", JavaHash.ZoneOffsetId(TimeSpan.Zero));
        Assert.Equal(90, JavaHash.Of(JavaHash.ZoneOffsetId(TimeSpan.Zero)));
        Assert.Equal("+01:00", JavaHash.ZoneOffsetId(TimeSpan.FromHours(1)));
        Assert.Equal(1276899534, JavaHash.Of(JavaHash.ZoneOffsetId(TimeSpan.FromHours(1))));
        Assert.Equal("-05:30", JavaHash.ZoneOffsetId(TimeSpan.FromMinutes(-330)));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1.50", "1.5")]
    [InlineData("100", "100")]
    [InlineData("0.00", "0")]
    [InlineData("-2.5", "-2.5")]
    [InlineData("1234567.891", "1234567.891")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("-0.0", "0")]
    public void PlainString_ForRedundantScaleAndSignedZero_MatchesJavaToPlainString(string input, string expected) =>
        Assert.Equal(expected, JavaHash.PlainString(decimal.Parse(input)));

    [Theory]
    [InlineData("0", 48)]
    [InlineData("1.50", 48568)]
    [InlineData("100", 48625)]
    [InlineData("-2.5", 1390124)]
    [InlineData("1234567.891", 745164438)]
    [InlineData("0.000001", 1346019711)]
    public void Decimal_AcrossScalesAndSigns_MatchesJavaBigDecimalHashCode(string input, int expected) =>
        Assert.Equal(expected, JavaHash.Of(decimal.Parse(input)));

    [Fact]
    public void Enum_ForAConstantName_HashesAsItsNameString() =>
        Assert.Equal(JavaHash.Of("ISDA_1991"), JavaHash.OfEnumName("ISDA_1991"));
}
