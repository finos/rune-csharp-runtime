using Finos.Rune.Runtime.Expression;
using Finos.Rune.Runtime.Mapper;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Finos.Rune.Runtime.Validation;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Expression;

/// <summary>
/// Message-parity tests for the operators generated expression code calls. The strings were copied
/// out of ExpressionOperatorsNullSafe.java; 147,062 of the test pack's validation records are data
/// rules, so these become part of the M5 byte comparison.
/// </summary>
public class RuneExprTests
{
    [Fact]
    public void Exists_WhenTheSingleValueIsNull_ReportsTheNullPath()
    {
        Assert.True(RuneExpr.Exists(MapperS<int?>.Of(1)).Get());

        ComparisonResult absent = RuneExpr.Exists(MapperS<int?>.OfNull());
        Assert.False(absent.Get());
        Assert.Equal("[Null] does not exist", absent.Error);
    }

    [Fact]
    public void Exists_ForAnEmptyList_ReportsAnEmptyPath()
    {
        Assert.Equal("[] does not exist", RuneExpr.Exists(MapperC<int?>.OfNull()).Error);
    }

    [Fact]
    public void NotExists_WhenTheValueIsPresent_ReportsItsTypeAndValue()
    {
        Assert.True(RuneExpr.NotExists(MapperS<int?>.OfNull()).Get());

        ComparisonResult present = RuneExpr.NotExists(MapperS<bool?>.Of(true));
        Assert.False(present.Get());
        Assert.Equal("[Boolean] does exist and is [true]", present.Error);
    }

    [Fact]
    public void SingleExists_ForZeroAndForTwoItems_ReportsADifferentMessageForEach()
    {
        Assert.True(RuneExpr.SingleExists(MapperC<int?>.Of([1])).Get());
        Assert.Equal(
            "Expected single [] but found zero",
            RuneExpr.SingleExists(MapperC<int?>.Of([])).Error);
        Assert.Equal(
            "Expected single [Integer, Integer] but found 2 [[1, 2]]",
            RuneExpr.SingleExists(MapperC<int?>.Of([1, 2])).Error);
    }

    [Fact]
    public void MultipleExists_ForZeroAndForOneItem_ReportsADifferentMessageForEach()
    {
        Assert.True(RuneExpr.MultipleExists(MapperC<int?>.Of([1, 2])).Get());
        Assert.Equal(
            "Expected multiple [] but found zero",
            RuneExpr.MultipleExists(MapperC<int?>.Of([])).Error);
        Assert.Equal(
            "Expected multiple [Integer] but only one [[1]]",
            RuneExpr.MultipleExists(MapperC<int?>.Of([1])).Error);
    }

    [Fact]
    public void Contains_ForNumbersEqualButDifferingInScale_FindsNoMatch()
    {
        MapperC<decimal?> haystack = MapperC<decimal?>.Of([1.0m]);
        MapperC<decimal?> needleSameScale = MapperC<decimal?>.Of([1.0m]);
        MapperC<decimal?> needleOtherScale = MapperC<decimal?>.Of([1.00m]);

        Assert.True(RuneExpr.Contains(haystack, needleSameScale).Get());
        Assert.False(RuneExpr.Contains(haystack, needleOtherScale).Get());
        Assert.Equal(1.0m, 1.00m);
    }

    [Fact]
    public void Contains_WhenEitherOperandIsEmpty_UsesTheEmptyListWording()
    {
        Assert.Equal(
            "Empty list does not contain all of [1]",
            RuneExpr.Contains(MapperC<int?>.Of([]), MapperC<int?>.Of([1])).Error);
        Assert.Equal(
            "[1] does not contain empty list",
            RuneExpr.Contains(MapperC<int?>.Of([1]), MapperC<int?>.Of([])).Error);
        Assert.Equal(
            "[1] does not contain all of [2]",
            RuneExpr.Contains(MapperC<int?>.Of([1]), MapperC<int?>.Of([2])).Error);
    }

    [Fact]
    public void Disjoint_ForOverlappingLists_ReportsTheCommonItems()
    {
        Assert.True(RuneExpr.Disjoint(MapperC<int?>.Of([1]), MapperC<int?>.Of([2])).Get());

        ComparisonResult overlapping =
            RuneExpr.Disjoint(MapperC<int?>.Of([1, 2]), MapperC<int?>.Of([2, 3]));
        Assert.False(overlapping.Get());
        Assert.Equal("[1, 2] is not disjoint from [2, 3]common items are [2]", overlapping.Error);
    }

    [Fact]
    public void DistinctIgnoringPrecision_ForNumbersDifferingOnlyInScale_KeepsTheFirstOccurrence()
    {
        MapperC<decimal?> distinct = RuneExpr.DistinctIgnoringPrecision(
            MapperC<decimal?>.Of([1.0m, 1.00m, 2m]));

        Assert.Equal(["1.0", "2"], distinct.GetMulti().Select(
            n => n!.Value.ToString(Culture.Invariant)));
    }

    /// <summary>
    /// Basic values have no builder form, so provenance cannot make equal strings distinct.
    /// </summary>
    /// <remarks>
    /// <c>distinctIgnoringPrecision</c> is a <c>HashSet</c>, so <c>hashCode()</c> decides. Java's
    /// Java's <c>HashSet&lt;String&gt;</c> sees the same values on either side. Builder-vs-built hash
    /// asymmetry applies only to generated model objects.
    /// </remarks>
    [Fact]
    public void DistinctIgnoringPrecision_ForEqualBasicValuesReadFromInputAndOutput_KeepsOne()
    {
        MapperC<string?> distinct = RuneExpr.DistinctIgnoringPrecision(
            MapperC<string?>.OfMappers(
                MapperC<string?>.Of(["a", "b"]),
                MapperC<string?>.OfOutputBuilder(["a", "b"])));

        Assert.Equal(["a", "b"], distinct.GetMulti());
    }

    [Fact]
    public void DistinctIgnoringPrecision_ForEqualValuesBothReadFromTheOutput_KeepsOne()
    {
        MapperC<string?> distinct = RuneExpr.DistinctIgnoringPrecision(
            MapperC<string?>.OfMappers(
                MapperC<string?>.OfOutputBuilder(["a", "b"]),
                MapperC<string?>.OfOutputBuilder(["a", "b"])));

        Assert.Equal(["a", "b"], distinct.GetMulti());
    }

    [Fact]
    public void DistinctIgnoringPrecision_ForBasicValuesMappedOffTheOutput_DropsBuilderProvenance()
    {
        var fromOutput = new Choicey { First = "x" };
        var fromInput = new Choicey { First = "x" };

        MapperC<string?> distinct = RuneExpr.DistinctIgnoringPrecision(
            MapperC<string?>.OfMappers(
                MapperS<Choicey?>.Of(fromInput).Map("getFirst", c => c!.First),
                MapperS<Choicey?>.OfOutputBuilder(fromOutput).Map("getFirst", c => c!.First)));

        Assert.Equal(["x"], distinct.GetMulti());
    }

    [Fact]
    public void DistinctIgnoringPrecision_ForEquivalentModelObjectsAcrossBuilderAndBuiltForms_KeepsBoth()
    {
        TestPojo input = TestPojo.CreateBuilder().SetStringAttr("same").Build();
        TestPojo output = TestPojo.CreateBuilder().SetStringAttr("same").Build();

        MapperC<TestPojo?> distinct = RuneExpr.DistinctIgnoringPrecision(
            MapperC<TestPojo?>.OfMappers(
                MapperC<TestPojo?>.Of([input]),
                MapperC<TestPojo?>.OfOutputBuilder([output])));

        Assert.Equal(2, distinct.ResultCount);
        Assert.All(distinct.GetMulti(), value => Assert.Equal("same", value!.StringAttr));
    }

    [Fact]
    public void DistinctIgnoringPrecision_WhenAppliedTwice_PreservesBuilderBucketsInTheSecondPass()
    {
        TestPojo input = TestPojo.CreateBuilder().SetStringAttr("same").Build();
        TestPojo output = TestPojo.CreateBuilder().SetStringAttr("same").Build();
        MapperC<TestPojo?> first = RuneExpr.DistinctIgnoringPrecision(
            MapperC<TestPojo?>.OfMappers(
                MapperC<TestPojo?>.Of([input]),
                MapperC<TestPojo?>.OfOutputBuilder([output])));

        MapperC<TestPojo?> second = RuneExpr.DistinctIgnoringPrecision(first);

        Assert.Equal(2, second.ResultCount);
    }

    [Fact]
    public void Choice_UnderTheRequiredMethod_FailsUnlessExactlyOneFieldIsSet()
    {
        Assert.True(RuneExpr.Choice(
            MapperS<Choicey?>.Of(new Choicey { First = "x" }),
            ["first", "second"],
            ChoiceRuleValidationMethod.Required).Get());

        ComparisonResult none = RuneExpr.Choice(
            MapperS<Choicey?>.Of(new Choicey()), ["first", "second"], ChoiceRuleValidationMethod.Required);
        Assert.Equal("One and only one field must be set of 'first', 'second'. No fields are set.", none.Error);

        ComparisonResult both = RuneExpr.Choice(
            MapperS<Choicey?>.Of(new Choicey { First = "x", Second = "y" }),
            ["first", "second"],
            ChoiceRuleValidationMethod.Required);
        Assert.Equal(
            "One and only one field must be set of 'first', 'second'. Set fields are 'first', 'second'.",
            both.Error);
    }

    [Fact]
    public void Choice_UnderTheOptionalMethod_FailsOnlyWhenMoreThanOneFieldIsSet()
    {
        Assert.True(RuneExpr.Choice(
            MapperS<Choicey?>.Of(new Choicey()), ["first", "second"],
            ChoiceRuleValidationMethod.Optional).Get());

        Assert.Equal(
            "Zero or one field must be set of 'first', 'second'. Set fields are 'first', 'second'.",
            RuneExpr.Choice(
                MapperS<Choicey?>.Of(new Choicey { First = "x", Second = "y" }),
                ["first", "second"],
                ChoiceRuleValidationMethod.Optional).Error);
    }

    [Fact]
    public void OnlyExists_WhenTheSetFieldsDifferFromTheExpectedSet_ReportsWhatWasSet()
    {
        Assert.True(RuneExpr.OnlyExists(
            MapperS<Choicey?>.Of(new Choicey { First = "x" }), ["first", "second"], ["first"]).Get());

        Assert.Equal(
            "Expected only 'first' to be set, but set fields are: 'first', 'second'.",
            RuneExpr.OnlyExists(
                MapperS<Choicey?>.Of(new Choicey { First = "x", Second = "y" }),
                ["first", "second"], ["first"]).Error);

        Assert.Equal(
            "Expected only 'first' to be set, but no fields were set.",
            RuneExpr.OnlyExists(
                MapperS<Choicey?>.Of(new Choicey()), ["first", "second"], ["first"]).Error);

        Assert.Equal(
            "Expected only 'first' to be set, but object was absent.",
            RuneExpr.OnlyExists(MapperS<Choicey?>.OfNull(), ["first", "second"], ["first"]).Error);
    }

    [Fact]
    public void Choice_ForAnAttributeWhosePropertyWasRenamedByTheCollisionRule_ResolvesItByRuneName()
    {
        Assert.True(RuneExpr.Choice(
            MapperS<Choicey?>.Of(new Choicey { ChoiceyValue = "x" }),
            ["choicey"],
            ChoiceRuleValidationMethod.Required).Get());
    }

    [Runtime.Attributes.RuneDataType("Choicey", "test.expr", "0.0.0")]
    private sealed class Choicey : RosettaModelObject
    {
        [Runtime.Attributes.RuneAttribute("first")]
        public string? First { get; init; }

        [Runtime.Attributes.RuneAttribute("second")]
        public string? Second { get; init; }

        [Runtime.Attributes.RuneAttribute("choicey")]
        public string? ChoiceyValue { get; init; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => new();

        public sealed class Builder : RosettaModelObjectBuilder
        {
            public override Choicey Build() => new();

            public override Builder Prune() => this;

            public override bool HasData() => false;
        }
    }
}
