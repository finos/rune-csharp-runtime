using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// Enum aliasing accepts exactly two things: a declared wire alias, and the exact name of a declared
/// enum member. Anything else — an ordinal, a signed ordinal, padded text, a flag list — is rejected.
/// </summary>
/// <remarks>
/// This is the trap <c>Enum.TryParse</c> sets. It happily turns <c>"0"</c>, <c>"-1"</c>,
/// <c>" Meter "</c> and <c>"Meter,Kilogram"</c> into enum values, so a fallback routed through it
/// silently accepts documents that never named a member at all — and, worse, resolves a numeric
/// token to whichever member happens to sit at that ordinal.
/// </remarks>
public class XmlEnumCodecTests
{
    /// <summary>Aliases that are themselves numeric, so ordinals and aliases would collide.</summary>
    private enum NumericAliasEnum
    {
        First,
        Second,
    }

    private static readonly XmlEnumCodec<UnitEnum> Units = new XmlEnumCodec<UnitEnum>(
        new Dictionary<UnitEnum, string>
        {
            [UnitEnum.Meter] = "Meter",
            [UnitEnum.Kilogram] = "Kilogram",
        });

    private static readonly XmlEnumCodec<NumericAliasEnum> NumericAliases =
        new XmlEnumCodec<NumericAliasEnum>(new Dictionary<NumericAliasEnum, string>
        {
            [NumericAliasEnum.First] = "1",
            [NumericAliasEnum.Second] = "2",
        });

    [Fact]
    public void Format_ForADeclaredValue_WritesItsAlias()
    {
        Assert.Equal("Meter", Units.Format(UnitEnum.Meter));
        Assert.Equal("Kilogram", Units.Format(UnitEnum.Kilogram));
        Assert.Null(Units.Format((UnitEnum?)null));
        Assert.Equal("Kilogram", Units.Format((UnitEnum?)UnitEnum.Kilogram));
    }

    [Fact]
    public void Parse_ForADeclaredAlias_ReadsIt() => Assert.Equal(UnitEnum.Kilogram, Units.Parse("Kilogram"));

    [Fact]
    public void Parse_ForAnExactMemberName_ReadsIt()
    {
        Assert.True(Units.TryParse("Meter", out UnitEnum value));
        Assert.Equal(UnitEnum.Meter, value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("00")]
    [InlineData("0x1")]
    public void TryParse_ForANumericOrSignedOrdinal_IsRejected(string text)
    {
        Assert.False(Units.TryParse(text, out UnitEnum value));
        Assert.Equal(default, value);
        Assert.Throws<FormatException>(() => Units.Parse(text));
    }

    [Theory]
    [InlineData(" Meter")]
    [InlineData("Meter ")]
    [InlineData(" Meter ")]
    [InlineData("\tMeter")]
    [InlineData("Meter\n")]
    public void TryParse_ForPaddedText_IsRejected(string text) => Assert.False(Units.TryParse(text, out _));

    [Theory]
    [InlineData("Meter,Kilogram")]
    [InlineData("Meter, Kilogram")]
    [InlineData("0,1")]
    public void TryParse_ForAFlagList_IsRejected(string text) => Assert.False(Units.TryParse(text, out _));

    [Theory]
    [InlineData("meter")]
    [InlineData("METER")]
    [InlineData("Metre")]
    [InlineData("")]
    public void TryParse_ForAWrongCaseOrUnknownName_IsRejected(string text) =>
        Assert.False(Units.TryParse(text, out _));

    [Fact]
    public void TryParse_ForNull_IsRejected() => Assert.False(Units.TryParse(null, out _));

    [Fact]
    public void TryParse_ForANumericAlias_ResolvesTheAliasAndNeverTheOrdinal()
    {
        Assert.Equal(NumericAliasEnum.First, NumericAliases.Parse("1"));
        Assert.Equal(NumericAliasEnum.Second, NumericAliases.Parse("2"));
        Assert.False(NumericAliases.TryParse("0", out _));
        Assert.False(NumericAliases.TryParse("3", out _));
    }

    [Fact]
    public void TryParse_ForAMemberNameOfANumericallyAliasedEnum_StillReadsIt()
    {
        Assert.Equal(NumericAliasEnum.First, NumericAliases.Parse("First"));
        Assert.Equal(NumericAliasEnum.Second, NumericAliases.Parse("Second"));
    }

    [Fact]
    public void TryParse_ForAMemberThatWasNeverDeclared_IsRejected()
    {
        var partial = new XmlEnumCodec<UnitEnum>(new Dictionary<UnitEnum, string>
        {
            [UnitEnum.Meter] = "Meter",
        });

        Assert.Equal(UnitEnum.Meter, partial.Parse("Meter"));
        Assert.False(partial.TryParse("Kilogram", out _));
        Assert.Throws<InvalidOperationException>(() => partial.Format(UnitEnum.Kilogram));
    }

    [Fact]
    public void Read_ForAnEnumAttributeCarryingAnOrdinal_IsReportedRatherThanSilentlyAccepted()
    {
        XmlCodecRegistry registry = Codecs.TestCodecs.CreateRegistry();
        const string Xml = "<TopLevel xmlns=\"urn:my.schema\"><Value Unit=\"0\">1</Value></TopLevel>";

        Assert.Throws<FormatException>(() => XmlDocumentReader.Read<TopLevel>(Xml, registry, out _));
    }
}
