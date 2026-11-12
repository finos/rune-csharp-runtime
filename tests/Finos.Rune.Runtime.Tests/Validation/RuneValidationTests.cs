using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Finos.Rune.Runtime.Validation;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Validation;

/// <summary>
/// Traversal-parity tests. The walk order and the paths are compared positionally against the Java
/// reference over the whole CDM test pack, so each of these pins one rule taken from
/// RosettaTypeValidator, SimpleProcessor or RosettaModelObject.processRosetta.
/// </summary>
public class RuneValidationTests
{
    [Fact]
    public void RootPath_ForTheInstanceUnderValidation_IsTheTypesSimpleName()
    {
        ValidationReport report = Pojo().Validate();

        Assert.Equal("TestPojo", report.Results[0].Path?.BuildPath());
    }

    [Fact]
    public void Cardinality_WhenARequiredFieldIsMissing_FailsNamingTheFieldAndItsPath()
    {
        ValidationReport report = TestPojo.CreateBuilder().Build().Validate();

        IValidationResult failure = Assert.Single(report.Failures);
        Assert.False(report.Success);
        Assert.Equal("'stringAttr' is a required field but does not exist.", failure.FailureReason);
        Assert.Equal(ValidationType.Cardinality, failure.ValidationType);
        Assert.Equal("TestPojo", failure.ModelObjectName);
        Assert.Equal("TestPojo", failure.Path?.BuildPath());
    }

    [Fact]
    public void Report_WhenEveryRequiredFieldIsPresent_SucceedsWithNoFailures()
    {
        ValidationReport report = Pojo().Validate();

        Assert.True(report.Success);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void Descent_IntoASingleChildObject_ReportsItAtTheChildsSubPath()
    {
        ValidationReport report = Pojo(b => b.SetChild(TestChild.CreateBuilder().Build())).Validate();

        IValidationResult childResult = Assert.Single(
            report.Results, r => r.ModelObjectName == "TestChild");
        Assert.Equal("TestPojo.child", childResult.Path?.BuildPath());
        Assert.Equal("'name' is a required field but does not exist.", childResult.FailureReason);
    }

    [Fact]
    public void Descent_WhenTheSingleChildIsNull_ProducesNoResultsForIt()
    {
        ValidationReport report = Pojo().Validate();

        Assert.DoesNotContain(report.Results, r => r.ModelObjectName == "TestChild");
    }

    [Fact]
    public void ElementPaths_ForATwoElementChildList_AreNumberedFromZero()
    {
        ValidationReport report = Pojo(b => b.SetChildren(
            [TestChild.CreateBuilder().Build(), TestChild.CreateBuilder().Build()]))
            .Validate();

        string[] childPaths = report.Results
            .Where(r => r.ModelObjectName == "TestChild")
            .Select(r => r.Path!.BuildPath())
            .ToArray();
        Assert.Equal(new[] { "TestPojo.children(0)", "TestPojo.children(1)" }, childPaths);
    }

    [Fact]
    public void ElementPaths_AcrossSuccessiveElementsOfOneList_DoNotCompoundIndices()
    {
        ValidationReport report = Pojo(b => b.SetChildren([
            TestChild.CreateBuilder().Build(),
            TestChild.CreateBuilder().Build(),
            TestChild.CreateBuilder().Build(),
        ])).Validate();

        Assert.Equal(
            new[] { "TestPojo.children(0)", "TestPojo.children(1)", "TestPojo.children(2)" },
            report.Results.Where(r => r.ModelObjectName == "TestChild")
                .Select(r => r.Path!.BuildPath()).ToArray());
    }

    [Fact]
    public void WalkOrder_ForAListOfChildren_ValidatesEveryElementBeforeAnyDescent()
    {
        ValidationReport report = Pojo(b => b.SetChildren([
            TestChild.CreateBuilder().SetName("a").Build(),
            TestChild.CreateBuilder().SetName("b").Build(),
        ])).Validate();

        string[] names = report.Results.Select(r => r.ModelObjectName).ToArray();
        Assert.Equal(new[] { "TestPojo", "TestChild", "TestChild" }, names);
    }

    [Fact]
    public void Descent_WhenAListHoldsANullElement_StillReportsTheNonNullElementsOwnResult()
    {
        var holder = new NullableListHolder([TestChild.CreateBuilder().SetName("a").Build(), null]);

        ValidationReport report = holder.Validate();

        IValidationResult only = Assert.Single(report.Results, r => r.ModelObjectName == "TestChild");
        Assert.Equal("NullableListHolder.items(0)", only.Path?.BuildPath());
    }

    [Fact]
    public void ElementPaths_ForANullableListHoldingNoNulls_AreNumberedByPosition()
    {
        var holder = new NullableListHolder([
            TestChild.CreateBuilder().SetName("a").Build(),
            TestChild.CreateBuilder().SetName("b").Build(),
        ]);

        ValidationReport report = holder.Validate();

        Assert.Equal(
            new[] { "NullableListHolder.items(0)", "NullableListHolder.items(1)" },
            report.Results.Select(r => r.Path!.BuildPath()).ToArray());
    }

    [Fact]
    public void Descent_ThroughAFieldWithMetaOverABasic_ProducesNoResults()
    {
        ValidationReport report = Pojo(b => b
            .SetSchemeAttr(FieldWithMeta<string>.CreateBuilder().SetValue("USD").Build()))
            .Validate();

        Assert.True(report.Success);
    }

    [Fact]
    public void Descent_ThroughAReferenceWithMeta_ReportsTheWrappedObjectUnderValue()
    {
        ValidationReport report = Pojo(b => b
            .SetRefAttr(ReferenceWithMeta<TestChild>.CreateBuilder()
                .SetValue(TestChild.CreateBuilder().Build())
                .Build()))
            .Validate();

        IValidationResult wrapped = Assert.Single(report.Results, r => r.ModelObjectName == "TestChild");
        Assert.Equal("TestPojo.refAttr.value", wrapped.Path?.BuildPath());
    }

    [Fact]
    public void Descent_ThroughAChoiceTypeWithNoValidatorsOfItsOwn_ReportsTheSelectedOptionUnderItsTypeName()
    {
        ValidationReport report = Pojo(b => b
            .SetChoiceAttr(TestChoice.CreateBuilder()
                .SetTestChild(TestChild.CreateBuilder().Build())
                .Build()))
            .Validate();

        IValidationResult nested = Assert.Single(report.Results, r => r.ModelObjectName == "TestChild");
        Assert.Equal("TestPojo.choiceAttr.TestChild", nested.Path?.BuildPath());
    }

    [Fact]
    public void ValidatorSelection_WhenTheChildIsASubtypeInstance_UsesTheSubtypesValidator()
    {
        ValidationReport report = Pojo(b => b.SetChild(TestSub.CreateBuilder().SetName("a").Build())).Validate();

        IValidationResult failure = Assert.Single(report.Failures);
        Assert.Equal("TestSub", failure.ModelObjectName);
        Assert.Equal("'extra' is a required field but does not exist.", failure.FailureReason);
        Assert.Equal("TestPojo.child", failure.Path?.BuildPath());
    }

    [Fact]
    public void Results_WhenIncludeSuccessesIsOff_ContainOnlyFailures()
    {
        var options = new RuneValidationOptions { IncludeSuccesses = false };

        ValidationReport report = TestPojo.CreateBuilder().Build().Validate(options);

        Assert.Single(report.Results);
        Assert.All(report.Results, r => Assert.False(r.IsSuccess));
    }

    [Fact]
    public void ValidatorSubstitution_KeyedOnTheGeneratedValidatorType_ReplacesThatValidator()
    {
        var validator = new RuneModelValidator(
            new SubstitutingValidatorFactory()
                .Substitute<TestPojoValidator>(new AlwaysFailsPojoValidator()));

        ValidationReport report = validator.Validate(Pojo());

        IValidationResult failure = Assert.Single(report.Failures);
        Assert.Equal("substituted", failure.FailureReason);
    }

    [Fact]
    public void ValidateOrThrow_WhenARequiredFieldIsMissing_ThrowsCarryingTheFailureReport()
    {
        var exception = Assert.Throws<RuneValidationException>(
            () => TestPojo.CreateBuilder().Build().ValidateOrThrow());

        Assert.Contains("'stringAttr' is a required field but does not exist.", exception.Message, StringComparison.Ordinal);
        Assert.Single(exception.Report.Failures);
    }

    [Fact]
    public void ValidateOrThrow_WhenValidationSucceeds_DoesNotThrow()
    {
        Pojo().ValidateOrThrow();
    }

    private static TestPojo Pojo(Action<TestPojo.Builder>? configure = null)
    {
        TestPojo.Builder builder = TestPojo.CreateBuilder().SetStringAttr("x");
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// A model object whose list can hold a null element — the generated builders filter those out
    /// on Build(), so the walker's null-element rule is otherwise unreachable from a fixture.
    /// </summary>
    [Runtime.Attributes.RuneDataType("NullableListHolder", "test.model", "0.0.0")]
    private sealed class NullableListHolder : RosettaModelObject
    {
        public NullableListHolder(IReadOnlyList<TestChild?> items)
        {
            Items = items;
        }

        [Runtime.Attributes.RuneAttribute("items")]
        public IReadOnlyList<TestChild?> Items { get; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => new();

        public sealed class Builder : RosettaModelObjectBuilder
        {
            public override NullableListHolder Build() => new([]);

            public override Builder Prune() => this;

            public override bool HasData() => false;
        }
    }

    private sealed class AlwaysFailsPojoValidator : TestPojoValidator
    {
        public override IReadOnlyList<IValidationResult> GetValidationResults(
            Runtime.Path.RosettaPath? path, TestPojo? o) =>
            [ValidationResult.Failure(
                "TestPojo", ValidationType.Cardinality, "TestPojo", path, string.Empty, "substituted")];
    }
}
