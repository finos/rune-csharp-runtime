using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Finos.Rune.Runtime.Validation;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Validation;

/// <summary>
/// Key is the only source of ValidationType.Key results, and C#'s MetaFields flattens Java's
/// List&lt;Key&gt; to a single ScopedKey string. These pin the bridge that keeps the two walks
/// producing the same results.
/// </summary>
public class KeyValidationTests
{
    [Fact]
    public void ScopedKey_SetOnAnObjectsMeta_YieldsOneKeySuccessAtKeyIndexZero()
    {
        TestChild child = TestChild.CreateBuilder()
            .SetName("a")
            .SetMeta(MetaFields.CreateBuilder().SetScopedKey("quantity-1").Build())
            .Build();

        ValidationReport report = child.Validate();

        IValidationResult key = Assert.Single(report.Results, r => r.ValidationType == ValidationType.Key);
        Assert.True(key.IsSuccess);
        Assert.Equal("Key", key.Name);
        Assert.Equal("Key", key.ModelObjectName);
        Assert.Equal("TestChild.meta.key(0)", key.Path?.BuildPath());
    }

    [Fact]
    public void ResultOrder_WhenMetaCarriesAScopedKey_PutsTheKeyResultLast()
    {
        TestChild child = TestChild.CreateBuilder()
            .SetName("a")
            .SetMeta(MetaFields.CreateBuilder().SetScopedKey("k").Build())
            .Build();

        ValidationReport report = child.Validate();

        Assert.Equal(ValidationType.Cardinality, report.Results[0].ValidationType);
        Assert.Equal(ValidationType.Key, report.Results[^1].ValidationType);
    }

    [Fact]
    public void KeyResult_WhenMetaCarriesOnlyAGlobalKey_IsNotProduced()
    {
        TestChild child = TestChild.CreateBuilder()
            .SetName("a")
            .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("abc123").Build())
            .Build();

        ValidationReport report = child.Validate();

        Assert.DoesNotContain(report.Results, r => r.ValidationType == ValidationType.Key);
    }

    [Fact]
    public void KeyResult_WhenTheObjectHasNoMetaAtAll_IsNotProduced()
    {
        ValidationReport report = TestChild.CreateBuilder().SetName("a").Build().Validate();

        Assert.DoesNotContain(report.Results, r => r.ValidationType == ValidationType.Key);
    }

    [Fact]
    public void KeyValidator_ForAKeyWithAScopeButNoValue_FailsOnKeyValue()
    {
        IReadOnlyList<IValidationResult> results = KeyValidator.Instance.GetValidationResults(
            null, Key.CreateBuilder().SetScope("DOCUMENT").Build());

        IValidationResult result = Assert.Single(results);
        Assert.Equal("Key.value", result.Name);
        Assert.Equal("Key value must be set", result.FailureReason);
    }

    [Fact]
    public void KeyValidator_ForAKeyWithAValueButNoScope_FailsOnKeyScope()
    {
        IReadOnlyList<IValidationResult> results = KeyValidator.Instance.GetValidationResults(
            null, Key.CreateBuilder().SetKeyValue("k").Build());

        IValidationResult result = Assert.Single(results);
        Assert.Equal("Key.scope", result.Name);
        Assert.Equal("Key scope must be set", result.FailureReason);
    }

    [Fact]
    public void Descent_IntoAMaterializedKeyNode_ProducesNoResultsBelowIt()
    {
        TestChild child = TestChild.CreateBuilder()
            .SetName("a")
            .SetMeta(MetaFields.CreateBuilder().SetScopedKey("k").Build())
            .Build();

        ValidationReport report = child.Validate();

        Assert.DoesNotContain(report.Results, r => r.Path!.BuildPath().Contains("key(0).", StringComparison.Ordinal));
    }
}
