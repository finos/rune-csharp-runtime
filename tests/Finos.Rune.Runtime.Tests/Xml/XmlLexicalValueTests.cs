using System.Globalization;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Lexical conversion for the XSD scalar types: invariant plain decimals, the trailing <c>Z</c> on
/// a written time, offset normalisation on a read time, and the optional timezone of a dateTime.
/// </summary>
public class XmlLexicalValueTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("-0.0000000001")]
    [InlineData("123456789012345678901234567.8")]
    public void FormatDecimal_ForAnyScale_WritesPlainInvariantNotation(string text)
    {
        decimal value = XmlLexicalValue.ParseDecimal(text);

        Assert.Equal(text, XmlLexicalValue.FormatDecimal(value));
        Assert.DoesNotContain("E", XmlLexicalValue.FormatDecimal(value), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatDecimal_UnderACommaDecimalCulture_StillWritesAPoint()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("1.5", XmlLexicalValue.FormatDecimal(1.5m));
            Assert.Equal(1.5m, XmlLexicalValue.ParseDecimal("1.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatTime_ForAZonelessTime_AppendsZ() =>
        Assert.Equal("01:23:45Z", XmlLexicalValue.FormatTime(new TimeOnly(1, 23, 45)));

    [Fact]
    public void FormatTime_ForASubSecondTime_KeepsTheFraction() =>
        Assert.Equal("01:23:45.5Z", XmlLexicalValue.FormatTime(new TimeOnly(1, 23, 45, 500)));

    [Theory]
    [InlineData("01:23:45")]
    [InlineData("01:23:45Z")]
    [InlineData("03:23:45+02:00")]
    [InlineData("00:23:45-01:00")]
    public void ParseTime_ForEveryTimezoneForm_NormalisesToUtc(string text) =>
        Assert.Equal(new TimeOnly(1, 23, 45), XmlLexicalValue.ParseTime(text));

    [Theory]
    [InlineData("2006-04-02")]
    [InlineData("2006-04-02Z")]
    [InlineData("2006-04-02+01:00")]
    public void ParseDate_ForATimezoneSuffix_DiscardsIt(string text) =>
        Assert.Equal(new DateOnly(2006, 4, 2), XmlLexicalValue.ParseDate(text));

    [Fact]
    public void FormatDate_ForADate_WritesTheIsoForm() =>
        Assert.Equal("2026-05-09", XmlLexicalValue.FormatDate(new DateOnly(2026, 5, 9)));

    [Fact]
    public void ParseZonedDateTime_ForADateOnlyValue_DefaultsTheTimeToMidnight()
    {
        DateTimeOffset value = XmlLexicalValue.ParseZonedDateTime("2006-04-02", out XmlZoneMetadata zone);

        Assert.Equal(new DateTimeOffset(2006, 4, 2, 0, 0, 0, TimeSpan.Zero), value);
        Assert.True(zone.IsAbsent);
    }

    [Fact]
    public void ParseZonedDateTime_ForADateOnlyValueWithACompactOffset_KeepsTheSpelling()
    {
        DateTimeOffset value = XmlLexicalValue.ParseZonedDateTime("2006-04-02+0100", out XmlZoneMetadata zone);

        Assert.Equal(new DateTimeOffset(2006, 4, 2, 0, 0, 0, TimeSpan.FromHours(1)), value);
        Assert.Equal("+0100", zone.Lexical);
    }

    [Fact]
    public void SplitZone_ForAValueWithNoZone_ReturnsItUnchanged()
    {
        string body = XmlLexicalValue.SplitZone("2006-04-02T15:38:00", out XmlZoneMetadata zone);

        Assert.Equal("2006-04-02T15:38:00", body);
        Assert.Equal(XmlTimeZoneKind.Absent, zone.Kind);
    }

    [Fact]
    public void SplitZone_ForANegativeYearSeparator_DoesNotMistakeItForAnOffset()
    {
        string body = XmlLexicalValue.SplitZone("2006-04-02", out XmlZoneMetadata zone);

        Assert.Equal("2006-04-02", body);
        Assert.True(zone.IsAbsent);
    }

    [Fact]
    public void FormatZonedDateTime_ForNoRecordedZoneAndAZeroOffset_WritesZ() =>
        Assert.Equal(
            "2006-04-02T15:38:00Z",
            XmlLexicalValue.FormatZonedDateTime(new DateTimeOffset(2006, 4, 2, 15, 38, 0, TimeSpan.Zero)));

    [Fact]
    public void FormatDateTime_ForALocalDateTime_WritesNoTimezone() =>
        Assert.Equal(
            "2006-04-02T15:38:00", XmlLexicalValue.FormatDateTime(new DateTime(2006, 4, 2, 15, 38, 0)));

    [Fact]
    public void ParseDateTime_ForATimezoneSuffix_DiscardsIt() =>
        Assert.Equal(
            new DateTime(2006, 4, 2, 15, 38, 0), XmlLexicalValue.ParseDateTime("2006-04-02T15:38:00+01:00"));

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void ParseBoolean_ForEveryXsdSpelling_ReadsIt(string text, bool expected) =>
        Assert.Equal(expected, XmlLexicalValue.ParseBoolean(text));

    [Fact]
    public void FormatBoolean_ForBothValues_WritesTheXsdSpelling()
    {
        Assert.Equal("true", XmlLexicalValue.FormatBoolean(true));
        Assert.Equal("false", XmlLexicalValue.FormatBoolean(false));
    }

    [Fact]
    public void ParseBoolean_ForAnUnknownSpelling_Throws() =>
        Assert.Throws<FormatException>(() => XmlLexicalValue.ParseBoolean("yes"));

    [Fact]
    public void ParseTime_ForAMalformedValue_Throws() =>
        Assert.Throws<FormatException>(() => XmlLexicalValue.ParseTime("25:00"));

    [Fact]
    public void ParseZonedDateTime_ForAMalformedValue_Throws() =>
        Assert.Throws<FormatException>(() => XmlLexicalValue.ParseZonedDateTime("not-a-date", out _));

    [Fact]
    public void FormatInt_AndParseInt_RoundTripInvariantly()
    {
        Assert.Equal("-42", XmlLexicalValue.FormatInt(-42));
        Assert.Equal(-42, XmlLexicalValue.ParseInt("-42"));
        Assert.Equal("9007199254740993", XmlLexicalValue.FormatLong(9007199254740993L));
        Assert.Equal(9007199254740993L, XmlLexicalValue.ParseLong("9007199254740993"));
    }
}
