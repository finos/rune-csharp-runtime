using System.Text.Json;
using System.Text.Json.Serialization;
using Finos.Rune.Runtime.Serialization;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Serialization;

public class RuneEnumConverterTests
{
    [JsonConverter(typeof(TestEnumConverter))]
    public enum TestEnum
    {
        PlainValue,
        WithDisplayName,
        _1
    }

    private sealed class TestEnumConverter : RuneEnumConverter<TestEnum>
    {
        public static readonly IReadOnlyDictionary<TestEnum, string> WireNames =
            new Dictionary<TestEnum, string>
            {
                [TestEnum.PlainValue] = "PlainValue",
                [TestEnum.WithDisplayName] = "30E/360.ISDA",
                [TestEnum._1] = "1",
            };

        public TestEnumConverter() : base(WireNames)
        {
        }
    }

    [Theory]
    [InlineData(TestEnum.PlainValue, "\"PlainValue\"")]
    [InlineData(TestEnum.WithDisplayName, "\"30E/360.ISDA\"")]
    [InlineData(TestEnum._1, "\"1\"")]
    public void Write_AcrossTheWireNameShapes_EmitsTheMappedString(TestEnum value, string expectedJson)
    {
        Assert.Equal(expectedJson, JsonSerializer.Serialize(value));
    }

    [Theory]
    [InlineData("\"PlainValue\"", TestEnum.PlainValue)]
    [InlineData("\"30E/360.ISDA\"", TestEnum.WithDisplayName)]
    [InlineData("\"1\"", TestEnum._1)]
    public void Read_AcrossTheWireNameShapes_ResolvesTheMappedValue(string json, TestEnum expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<TestEnum>(json));
    }

    [Fact]
    public void RoundTrip_OverEveryDeclaredValue_ReturnsTheSameValue()
    {
        foreach (TestEnum value in Enum.GetValues<TestEnum>())
        {
            string json = JsonSerializer.Serialize(value);
            Assert.Equal(value, JsonSerializer.Deserialize<TestEnum>(json));
        }
    }

    [Fact]
    public void Read_ForAWireNameOutsideTheMap_ThrowsNamingTheValue()
    {
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestEnum>("\"NoSuchValue\""));
        Assert.Contains("NoSuchValue", ex.Message);
    }

    [Fact]
    public void Read_ForANumberToken_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestEnum>("42"));
    }

    [Fact]
    public void Read_ForANullTokenOnANonNullableEnum_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<TestEnum>("null"));
    }

    [Fact]
    public void Read_ForANullTokenOnANullableEnum_IsNull()
    {
        Assert.Null(JsonSerializer.Deserialize<TestEnum?>("null"));
    }
}
