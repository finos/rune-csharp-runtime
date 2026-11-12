using System.Text.Json;
using System.Text.Json.Nodes;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Serialization;

public class RuneJsonTests
{
    private static void AssertTreeEqual(string expected, string actual)
    {
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)),
            $"JSON trees differ.\nExpected: {expected}\nActual:   {actual}");
    }

    private static string RoundTrip(string json)
    {
        var obj = RuneJson.Deserialize<TestPojo>(json);
        Assert.NotNull(obj);
        return RuneJson.Serialize(obj!);
    }

    [Fact]
    public void Serialize_ForAPojoOfScalarAttributes_WritesTheHeadersAndOmitsUnsetMembers()
    {
        var pojo = TestPojo.CreateBuilder()
            .SetStringAttr("hello")
            .SetNumberAttr(1.10m)
            .SetBoolAttr(true)
            .SetDateAttr(new DateOnly(2024, 12, 10))
            .SetEnumAttr(TestEnum._30E_360)
            .Build();

        string json = RuneJson.Serialize(pojo);

        AssertTreeEqual("""
            {
              "@model": "test",
              "@type": "test.model.TestPojo",
              "@version": "0.0.0",
              "stringAttr": "hello",
              "numberAttr": 1.10,
              "boolAttr": true,
              "dateAttr": "2024-12-10",
              "enumAttr": "30E/360"
            }
            """, json);
    }

    [Fact]
    public void Serialize_ForADecimalWithATrailingZero_PreservesTheScale()
    {
        string json = RuneJson.Serialize(TestPojo.CreateBuilder().SetNumberAttr(1.10m).Build());
        Assert.Contains("1.10", json);
        Assert.DoesNotContain("1.1,", json);
    }

    [Fact]
    public void RoundTrip_OfScalarsAndAStringList_ReproducesTheInputTree()
    {
        string input = """
            {
              "@model": "test",
              "@type": "test.model.TestPojo",
              "@version": "0.0.0",
              "stringAttr": "hello",
              "numberAttr": 123.456,
              "stringList": [ "a", "b" ]
            }
            """;
        AssertTreeEqual(input, RoundTrip(input));
    }

    [Fact]
    public void ZonedDateTime_WithAZeroOffset_WritesZ()
    {
        var pojo = TestPojo.CreateBuilder()
            .SetZonedAttr(new DateTimeOffset(2024, 12, 12, 11, 18, 57, TimeSpan.Zero))
            .Build();
        Assert.Contains("\"2024-12-12T11:18:57Z\"", RuneJson.Serialize(pojo));
    }

    [Fact]
    public void ZonedDateTime_WithATwoHourOffset_KeepsThatOffsetThroughRoundTrip()
    {
        string input = """{ "zonedAttr": "2024-12-12T11:18:57+02:00" }""";
        var pojo = RuneJson.Deserialize<TestPojo>(input)!;
        Assert.Equal(TimeSpan.FromHours(2), pojo.ZonedAttr!.Value.Offset);
        Assert.Contains("\"2024-12-12T11:18:57+02:00\"", RuneJson.Serialize(pojo));
    }

    [Fact]
    public void Date_GivenTheLegacyYearMonthDayObject_ReadsItAndRewritesTheIsoString()
    {
        var pojo = RuneJson.Deserialize<TestPojo>(
            """{ "dateAttr": { "year": 2017, "month": 12, "day": 18 } }""")!;
        Assert.Equal(new DateOnly(2017, 12, 18), pojo.DateAttr);
        Assert.Contains("\"2017-12-18\"", RuneJson.Serialize(pojo));
    }

    [Fact]
    public void FieldWithMeta_ForASchemeAndDataPair_RoundTripsBothTheValueAndTheScheme()
    {
        string input = """
            {
              "schemeAttr": { "@scheme": "urn:iso:std:iso:3166", "@data": "GB" }
            }
            """;
        var pojo = RuneJson.Deserialize<TestPojo>(input)!;
        Assert.Equal("GB", pojo.SchemeAttr!.Value);
        Assert.Equal("urn:iso:std:iso:3166", pojo.SchemeAttr.Meta!.Scheme);
        AssertTreeEqual(WithHeaders(input), RuneJson.Serialize(pojo));
    }

    [Fact]
    public void Keys_WhenInlinedOnTheObject_PopulateMetaAndRoundTrip()
    {
        string input = """
            {
              "@key": "52ffe975",
              "@key:external": "trade-1",
              "stringAttr": "x"
            }
            """;
        var pojo = RuneJson.Deserialize<TestPojo>(input)!;
        Assert.Equal("52ffe975", pojo.Meta!.GlobalKey);
        Assert.Equal("trade-1", pojo.Meta.ExternalKey);
        AssertTreeEqual(WithHeaders(input), RuneJson.Serialize(pojo));
    }

    [Fact]
    public void ReferenceWithMeta_ForTheGlobalExternalAndScopedForms_RoundTripsAllThree()
    {
        string input = """
            {
              "refAttr": { "@ref": "b6bdbfc2", "@ref:external": "item-1", "@ref:scoped": "loc-1" }
            }
            """;
        var pojo = RuneJson.Deserialize<TestPojo>(input)!;
        Assert.Equal("b6bdbfc2", pojo.RefAttr!.GlobalReference);
        Assert.Equal("item-1", pojo.RefAttr.ExternalReference);
        Assert.Equal("loc-1", pojo.RefAttr.ScopedReference!.ScopedReference);
        AssertTreeEqual(WithHeaders(input), RuneJson.Serialize(pojo));
    }

    [Fact]
    public void ReferenceWithMeta_WhenItCarriesAResolvedValue_IsFlattenedToThatValue()
    {
        var pojo = TestPojo.CreateBuilder()
            .SetRefAttr(ReferenceWithMeta<TestChild>.CreateBuilder()
                .SetValue(TestChild.CreateBuilder().SetName("resolved").Build())
                .Build())
            .Build();

        string json = RuneJson.Serialize(pojo);
        AssertTreeEqual(WithHeaders("""{ "refAttr": { "name": "resolved" } }"""), json);

        var back = RuneJson.Deserialize<TestPojo>(json)!;
        Assert.Equal("resolved", back.RefAttr!.Value!.Name);
    }

    [Fact]
    public void Prune_ForAReferenceHoldingOnlyPointsToAndScope_DropsTheMember()
    {
        var builder = TestPojo.CreateBuilder()
            .SetStringAttr("x")
            .SetRefAttr(ReferenceWithMeta<TestChild>.CreateBuilder()
                .SetScopedReference(Reference.CreateBuilder().SetPointsTo("child").SetScope("DOCUMENT"))
                .Build());

        builder.Prune();

        Assert.Null(builder.RefAttr);
        AssertTreeEqual(WithHeaders("""{ "stringAttr": "x" }"""), RuneJson.Serialize(builder.Build()));
    }

    [Fact]
    public void Prune_ForAReferenceHoldingAScopedReference_KeepsTheMember()
    {
        var builder = TestPojo.CreateBuilder()
            .SetStringAttr("x")
            .SetRefAttr(ReferenceWithMeta<TestChild>.CreateBuilder()
                .SetScopedReference(Reference.CreateBuilder().SetPointsTo("child").SetScopedReference("child-1"))
                .Build());

        builder.Prune();

        Assert.NotNull(builder.RefAttr);
        AssertTreeEqual(
            WithHeaders("""{ "stringAttr": "x", "refAttr": { "@ref:scoped": "child-1" } }"""),
            RuneJson.Serialize(builder.Build()));
    }

    [Fact]
    public void Subtype_WhenSubstitutedForItsBase_WritesATrailingTypeDiscriminatorAndReadsBack()
    {
        var pojo = TestPojo.CreateBuilder()
            .SetChild(((TestSub.Builder)TestSub.CreateBuilder().SetExtra("e").SetName("n")).Build())
            .Build();

        string json = RuneJson.Serialize(pojo);
        AssertTreeEqual(WithHeaders("""
            { "child": { "name": "n", "extra": "e", "@type": "test.model.TestSub" } }
            """), json);

        var back = RuneJson.Deserialize<TestPojo>(json)!;
        var sub = Assert.IsType<TestSub>(back.Child);
        Assert.Equal("e", sub.Extra);
    }

    [Fact]
    public void TypeDiscriminator_WhenTheValueIsOfTheDeclaredType_IsNotWritten()
    {
        var pojo = TestPojo.CreateBuilder()
            .SetChild(TestChild.CreateBuilder().SetName("n").Build())
            .Build();
        Assert.DoesNotContain("@type\": \"test.model.TestChild", RuneJson.Serialize(pojo));
    }

    [Fact]
    public void Choice_WithALeadingTypeDiscriminator_SelectsThatOptionAndRoundTrips()
    {
        string input = """
            {
              "choiceAttr": { "@type": "test.model.TestChild", "name": "picked" }
            }
            """;
        var pojo = RuneJson.Deserialize<TestPojo>(input)!;
        Assert.Equal("picked", pojo.ChoiceAttr!.TestChild!.Name);
        Assert.Null(pojo.ChoiceAttr.TestPojo);
        AssertTreeEqual(WithHeaders(input), RuneJson.Serialize(pojo));
    }

    [Fact]
    public void Choice_WhenTwoOptionsAreSet_ThrowsOnWrite()
    {
        var choice = TestChoice.CreateBuilder()
            .SetTestChild(TestChild.CreateBuilder().SetName("a").Build())
            .SetTestPojo(TestPojo.CreateBuilder().SetStringAttr("b").Build())
            .Build();
        Assert.Throws<JsonException>(() => RuneJson.Serialize(choice));
    }

    [Fact]
    public void Deserialize_ForAnUnknownMemberInStrictMode_ThrowsNamingTheMember()
    {
        var ex = Assert.Throws<JsonException>(
            () => RuneJson.Deserialize<TestPojo>("""{ "noSuchAttr": 1 }"""));
        Assert.Contains("noSuchAttr", ex.Message);
    }

    [Fact]
    public void Deserialize_ForAnUnknownMemberInLenientMode_SkipsItAndKeepsTheRest()
    {
        var options = RuneJson.CreateOptions(strict: false);
        var pojo = RuneJson.Deserialize<TestPojo>(
            """{ "noSuchAttr": { "deep": [1,2] }, "stringAttr": "kept" }""", options);
        Assert.Equal("kept", pojo!.StringAttr);
    }

    [Fact]
    public void Deserialize_ForAnEmptyNestedObject_LeavesTheMemberNull()
    {
        var pojo = RuneJson.Deserialize<TestPojo>(
            """{ "stringAttr": "x", "child": {} }""")!;
        Assert.Null(pojo.Child);
    }

    [Fact]
    public void Serialize_ForUnsetListsAndScalars_WritesNeitherTheMemberNorNull()
    {
        string json = RuneJson.Serialize(TestPojo.CreateBuilder().SetStringAttr("x").Build());
        Assert.DoesNotContain("stringList", json);
        Assert.DoesNotContain("null", json);
    }

    private static string WithHeaders(string bareObject)
    {
        var node = (JsonObject)JsonNode.Parse(bareObject)!;
        var result = new JsonObject
        {
            ["@model"] = "test",
            ["@type"] = "test.model.TestPojo",
            ["@version"] = "0.0.0",
        };
        foreach (var pair in node.ToList())
        {
            node.Remove(pair.Key);
            result[pair.Key] = pair.Value;
        }
        return result.ToJsonString();
    }
}
