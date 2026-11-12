using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Tests.Serialization.Model;
using Xunit;

namespace Finos.Rune.Runtime.Tests;

/// <summary>
/// Value equality of model objects, which Rune's <c>=</c> operator depends on.
/// </summary>
/// <remarks>
/// Generated POCOs used to inherit reference equality, so a data rule comparing two structurally
/// identical objects failed: <c>PriceScheduleDerivedQuantityUnit</c> reported 81 failures over the
/// CDM pack against Java's 3, all of them "UnitTypeImpl does not equal UnitTypeImpl".
/// </remarks>
public class RosettaModelObjectEqualityTests
{
    private static TestChild Child(string name) => TestChild.CreateBuilder().SetName(name).Build();

    [Fact]
    public void Equals_ForSeparatelyBuiltObjectsWithTheSameAttributes_IsTrue()
    {
        Assert.Equal(Child("a"), Child("a"));
    }

    [Fact]
    public void GetHashCode_ForTwoEqualObjects_Matches()
    {
        Assert.Equal(Child("a").GetHashCode(), Child("a").GetHashCode());
    }

    [Fact]
    public void Equals_WhenAnAttributeValueDiffers_IsFalse()
    {
        Assert.NotEqual(Child("a"), Child("b"));
    }

    [Fact]
    public void Equals_WhenOneSideLeavesTheAttributeAbsent_IsFalse()
    {
        Assert.NotEqual(Child("a"), TestChild.CreateBuilder().Build());
    }

    [Fact]
    public void Equals_ForANestedModelObjectAttribute_ComparesItByValue()
    {
        TestChild Build() => TestChild.CreateBuilder()
            .SetName("a")
            .SetMeta(MetaFields.CreateBuilder().SetScopedKey("k").Build())
            .Build();

        Assert.Equal(Build(), Build());
        Assert.NotEqual(Build(), Child("a"));
    }

    [Fact]
    public void Equals_ForAFieldWithMetaAttribute_ComparesItByValue()
    {
        TestPojo Build() => TestPojo.CreateBuilder()
            .SetSchemeAttr(FieldWithMeta<string>.CreateBuilder().SetValue("USD").Build())
            .Build();

        Assert.Equal(Build(), Build());
    }

    [Fact]
    public void Equals_BetweenASubtypeAndItsBaseHoldingTheSameAttributes_IsFalseBothWays()
    {
        TestSub sub = (TestSub)TestSub.CreateBuilder().SetName("a").Build();

        Assert.NotEqual<object?>(sub, Child("a"));
        Assert.NotEqual<object?>(Child("a"), sub);
    }

    [Fact]
    public void Equals_ForNullAndAnUnrelatedType_IsFalse()
    {
        Assert.False(Child("a").Equals(null));
        Assert.False(Child("a").Equals("a"));
    }

    [Fact]
    public void Equals_BetweenOneGenericClosedOverDifferentArguments_IsFalse()
    {
        FieldWithMeta<int> asInt = FieldWithMeta<int>.CreateBuilder().SetValue(1).Build();

        Assert.False(asInt.Equals(FieldWithMeta<long>.CreateBuilder().SetValue(1).Build()));
    }

    [Fact]
    public void Equals_BetweenAnUnsetListAndAnEmptyOne_HoldsAndHashesAlike()
    {
        TestPojo absent = TestPojo.CreateBuilder().Build();
        TestPojo empty = TestPojo.CreateBuilder().SetStringList([]).Build();

        Assert.Equal(absent, empty);
        Assert.Equal(absent.GetHashCode(), empty.GetHashCode());
    }

    [Fact]
    public void Equals_ForAListAttribute_ComparesElementsInOrderAndByLength()
    {
        TestPojo Build(params string[] items) => TestPojo.CreateBuilder().SetStringList(items).Build();

        Assert.Equal(Build("a", "b"), Build("a", "b"));
        Assert.NotEqual(Build("a", "b"), Build("b", "a"));
        Assert.NotEqual(Build("a"), Build("a", "b"));
    }

    [Fact]
    public void Equals_ForAListOfModelObjects_ComparesTheElementsByValue()
    {
        TestPojo Build() => TestPojo.CreateBuilder().SetChildren([Child("a"), Child("b")]).Build();

        Assert.Equal(Build(), Build());
        Assert.NotEqual(
            Build(),
            TestPojo.CreateBuilder().SetChildren([Child("a")]).Build());
    }
}
