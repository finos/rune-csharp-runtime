using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Tests.Serialization.Model;

[System.Text.Json.Serialization.JsonConverter(typeof(TestEnumConverter))]
public enum TestEnum
{
    FirstValue,
    _30E_360,
}

public sealed class TestEnumConverter : RuneEnumConverter<TestEnum>
{
    public static readonly IReadOnlyDictionary<TestEnum, string> WireNames =
        new Dictionary<TestEnum, string>
        {
            [TestEnum.FirstValue] = "FirstValue",
            [TestEnum._30E_360] = "30E/360",
        };

    public TestEnumConverter() : base(WireNames)
    {
    }
}

[RuneDataType("TestChild", "test.model", "0.0.0")]
[RuneMeta(typeof(TestChildMeta))]
public class TestChild : RosettaModelObject, IGlobalKey
{
    protected TestChild(Builder builder)
    {
        Name = builder.Name;
        Meta = builder.Meta?.Build();
    }

    [RuneAttribute("name")]
    public string? Name { get; }

    [RuneAttribute("meta")]
    [RuneMetaType]
    public MetaFields? Meta { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        SetBuilderFields(builder);
        return builder;
    }

    protected void SetBuilderFields(Builder builder)
    {
        builder.SetName(Name);
        builder.SetMeta(Meta);
    }

    public class Builder : RosettaModelObjectBuilder
    {
        public string? Name { get; private set; }

        public MetaFields.Builder? Meta { get; private set; }

        public virtual Builder SetName(string? name)
        {
            Name = name;
            return this;
        }

        public virtual Builder SetMeta(MetaFields.Builder? meta)
        {
            Meta = meta;
            return this;
        }

        public virtual Builder SetMeta(MetaFields? meta)
        {
            Meta = meta?.ToBuilder();
            return this;
        }

        public override TestChild Build() => new TestChild(this);

        public override Builder Prune()
        {
            if (Meta != null && !Meta.Prune().HasData())
            {
                Meta = null;
            }
            return this;
        }

        public override bool HasData()
        {
            if (Name != null)
            {
                return true;
            }
            return false;
        }
    }
}

[RuneDataType("TestSub", "test.model", "0.0.0")]
[RuneMeta(typeof(TestSubMeta))]
public class TestSub : TestChild
{
    protected TestSub(Builder builder) : base(builder)
    {
        Extra = builder.Extra;
    }

    [RuneAttribute("extra")]
    public string? Extra { get; }

    public static new Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        SetBuilderFields(builder);
        return builder;
    }

    protected void SetBuilderFields(Builder builder)
    {
        base.SetBuilderFields(builder);
        builder.SetExtra(Extra);
    }

    public new class Builder : TestChild.Builder
    {
        public string? Extra { get; private set; }

        public virtual Builder SetExtra(string? extra)
        {
            Extra = extra;
            return this;
        }

        public override TestSub Build() => new TestSub(this);

        public override Builder Prune()
        {
            base.Prune();
            return this;
        }

        public override bool HasData()
        {
            if (base.HasData())
            {
                return true;
            }
            return Extra != null;
        }
    }
}

[RuneDataType("TestChoice", "test.model", "0.0.0")]
[RuneMeta(typeof(TestChoiceMeta))]
[RuneChoiceType]
public class TestChoice : RosettaModelObject
{
    protected TestChoice(Builder builder)
    {
        TestChild = builder.TestChild?.Build();
        TestPojo = builder.TestPojo?.Build();
    }

    [RuneAttribute("TestChild")]
    public TestChild? TestChild { get; }

    [RuneAttribute("TestPojo")]
    public TestPojo? TestPojo { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetTestChild(TestChild);
        builder.SetTestPojo(TestPojo);
        return builder;
    }

    public class Builder : RosettaModelObjectBuilder
    {
        public TestChild.Builder? TestChild { get; private set; }

        public TestPojo.Builder? TestPojo { get; private set; }

        public virtual Builder SetTestChild(TestChild? value)
        {
            TestChild = value?.ToBuilder();
            return this;
        }

        public virtual Builder SetTestPojo(TestPojo? value)
        {
            TestPojo = value?.ToBuilder();
            return this;
        }

        public override TestChoice Build() => new TestChoice(this);

        public override Builder Prune()
        {
            if (TestChild != null && !TestChild.Prune().HasData())
            {
                TestChild = null;
            }
            if (TestPojo != null && !TestPojo.Prune().HasData())
            {
                TestPojo = null;
            }
            return this;
        }

        public override bool HasData()
        {
            if (TestChild != null && TestChild.HasData())
            {
                return true;
            }
            if (TestPojo != null && TestPojo.HasData())
            {
                return true;
            }
            return false;
        }
    }
}

[RuneDataType("TestPojo", "test.model", "0.0.0")]
[RuneMeta(typeof(TestPojoMeta))]
public class TestPojo : RosettaModelObject, IGlobalKey
{
    protected TestPojo(Builder builder)
    {
        StringAttr = builder.StringAttr;
        NumberAttr = builder.NumberAttr;
        BoolAttr = builder.BoolAttr;
        DateAttr = builder.DateAttr;
        ZonedAttr = builder.ZonedAttr;
        EnumAttr = builder.EnumAttr;
        StringList = builder.StringList.Count == 0
            ? null
            : new List<string>(builder.StringList).AsReadOnly();
        Child = builder.Child?.Build();
        Children = builder.Children.Count == 0
            ? null
            : builder.Children.FindAll(item => item != null).ConvertAll(item => item.Build()).AsReadOnly();
        SchemeAttr = builder.SchemeAttr?.Build();
        LocationAttr = builder.LocationAttr?.Build();
        RefAttr = builder.RefAttr?.Build();
        ChoiceAttr = builder.ChoiceAttr?.Build();
        Meta = builder.Meta?.Build();
    }

    [RuneAttribute("stringAttr")]
    public string? StringAttr { get; }

    [RuneAttribute("numberAttr")]
    public decimal? NumberAttr { get; }

    [RuneAttribute("boolAttr")]
    public bool? BoolAttr { get; }

    [RuneAttribute("dateAttr")]
    public DateOnly? DateAttr { get; }

    [RuneAttribute("zonedAttr")]
    public DateTimeOffset? ZonedAttr { get; }

    [RuneAttribute("enumAttr")]
    public TestEnum? EnumAttr { get; }

    [RuneAttribute("stringList")]
    public IReadOnlyList<string>? StringList { get; }

    [RuneAttribute("child")]
    public TestChild? Child { get; }

    [RuneAttribute("children")]
    public IReadOnlyList<TestChild>? Children { get; }

    [RuneAttribute("schemeAttr")]
    public FieldWithMeta<string>? SchemeAttr { get; }

    [RuneAttribute("locationAttr")]
    public FieldWithMeta<TestChild>? LocationAttr { get; }

    [RuneAttribute("refAttr")]
    public ReferenceWithMeta<TestChild>? RefAttr { get; }

    [RuneAttribute("choiceAttr")]
    public TestChoice? ChoiceAttr { get; }

    [RuneAttribute("meta")]
    [RuneMetaType]
    public MetaFields? Meta { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetStringAttr(StringAttr);
        builder.SetNumberAttr(NumberAttr);
        builder.SetBoolAttr(BoolAttr);
        builder.SetDateAttr(DateAttr);
        builder.SetZonedAttr(ZonedAttr);
        builder.SetEnumAttr(EnumAttr);
        builder.SetStringList(StringList);
        builder.SetChild(Child);
        builder.SetChildren(Children);
        builder.SetSchemeAttr(SchemeAttr);
        builder.SetLocationAttr(LocationAttr);
        builder.SetRefAttr(RefAttr);
        builder.SetChoiceAttr(ChoiceAttr);
        builder.SetMeta(Meta);
        return builder;
    }

    public class Builder : RosettaModelObjectBuilder
    {
        public string? StringAttr { get; private set; }

        public decimal? NumberAttr { get; private set; }

        public bool? BoolAttr { get; private set; }

        public DateOnly? DateAttr { get; private set; }

        public DateTimeOffset? ZonedAttr { get; private set; }

        public TestEnum? EnumAttr { get; private set; }

        public List<string> StringList { get; private set; } = new List<string>();

        public TestChild.Builder? Child { get; private set; }

        public List<TestChild.Builder> Children { get; private set; } = new List<TestChild.Builder>();

        public FieldWithMeta<string>.Builder? SchemeAttr { get; private set; }

        public FieldWithMeta<TestChild>.Builder? LocationAttr { get; private set; }

        public ReferenceWithMeta<TestChild>.Builder? RefAttr { get; private set; }

        public TestChoice.Builder? ChoiceAttr { get; private set; }

        public MetaFields.Builder? Meta { get; private set; }

        public virtual Builder SetStringAttr(string? value) { StringAttr = value; return this; }

        public virtual Builder SetNumberAttr(decimal? value) { NumberAttr = value; return this; }

        public virtual Builder SetBoolAttr(bool? value) { BoolAttr = value; return this; }

        public virtual Builder SetDateAttr(DateOnly? value) { DateAttr = value; return this; }

        public virtual Builder SetZonedAttr(DateTimeOffset? value) { ZonedAttr = value; return this; }

        public virtual Builder SetEnumAttr(TestEnum? value) { EnumAttr = value; return this; }

        public virtual Builder SetStringList(IEnumerable<string>? items)
        {
            StringList = items == null ? new List<string>() : new List<string>(items);
            return this;
        }

        public virtual Builder SetChild(TestChild? value) { Child = value?.ToBuilder(); return this; }

        public virtual Builder SetChildren(IEnumerable<TestChild>? items)
        {
            Children = new List<TestChild.Builder>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    Children.Add(item.ToBuilder());
                }
            }
            return this;
        }

        public virtual Builder SetSchemeAttr(FieldWithMeta<string>? value) { SchemeAttr = value?.ToBuilder(); return this; }

        public virtual Builder SetLocationAttr(FieldWithMeta<TestChild>? value) { LocationAttr = value?.ToBuilder(); return this; }

        public virtual Builder SetRefAttr(ReferenceWithMeta<TestChild>? value) { RefAttr = value?.ToBuilder(); return this; }

        public virtual Builder SetChoiceAttr(TestChoice? value) { ChoiceAttr = value?.ToBuilder(); return this; }

        public virtual Builder SetMeta(MetaFields? meta) { Meta = meta?.ToBuilder(); return this; }

        public override TestPojo Build() => new TestPojo(this);

        public override Builder Prune()
        {
            if (Child != null && !Child.Prune().HasData()) { Child = null; }
            PruneList(Children);
            if (SchemeAttr != null && !SchemeAttr.Prune().HasData()) { SchemeAttr = null; }
            if (LocationAttr != null && !LocationAttr.Prune().HasData()) { LocationAttr = null; }
            if (RefAttr != null && !RefAttr.Prune().HasData()) { RefAttr = null; }
            if (ChoiceAttr != null && !ChoiceAttr.Prune().HasData()) { ChoiceAttr = null; }
            if (Meta != null && !Meta.Prune().HasData()) { Meta = null; }
            return this;
        }

        public override bool HasData()
        {
            if (StringAttr != null || NumberAttr != null || BoolAttr != null || DateAttr != null
                || ZonedAttr != null || EnumAttr != null || StringList.Count > 0 || SchemeAttr != null)
            {
                return true;
            }
            if (Child != null && Child.HasData())
            {
                return true;
            }
            if (Children.Exists(item => item != null && item.HasData()))
            {
                return true;
            }
            if (LocationAttr != null && LocationAttr.HasData())
            {
                return true;
            }
            if (RefAttr != null && RefAttr.HasData())
            {
                return true;
            }
            if (ChoiceAttr != null && ChoiceAttr.HasData())
            {
                return true;
            }
            return false;
        }
    }
}

/// <summary>Cardinality validator for TestChild: <c>name</c> is required.</summary>
public sealed class TestChildValidator : Finos.Rune.Runtime.Validation.IValidator<TestChild>
{
    public IReadOnlyList<Finos.Rune.Runtime.Validation.IValidationResult> GetValidationResults(
        Finos.Rune.Runtime.Path.RosettaPath? path, TestChild? o) =>
        ValidatorSupport.Cardinality(
            "TestChild", path, ("name", o?.Name != null ? 1 : 0, 1, 1));
}

/// <summary>Cardinality validator for TestSub: adds <c>extra</c> as required.</summary>
public sealed class TestSubValidator : Finos.Rune.Runtime.Validation.IValidator<TestSub>
{
    public IReadOnlyList<Finos.Rune.Runtime.Validation.IValidationResult> GetValidationResults(
        Finos.Rune.Runtime.Path.RosettaPath? path, TestSub? o) =>
        ValidatorSupport.Cardinality(
            "TestSub", path, ("name", o?.Name != null ? 1 : 0, 1, 1), ("extra", o?.Extra != null ? 1 : 0, 1, 1));
}

/// <summary>Cardinality validator for TestPojo: <c>stringAttr</c> is required.</summary>
public class TestPojoValidator : Finos.Rune.Runtime.Validation.IValidator<TestPojo>
{
    public virtual IReadOnlyList<Finos.Rune.Runtime.Validation.IValidationResult> GetValidationResults(
        Finos.Rune.Runtime.Path.RosettaPath? path, TestPojo? o) =>
        ValidatorSupport.Cardinality(
            "TestPojo", path, ("stringAttr", o?.StringAttr != null ? 1 : 0, 1, 1));
}

/// <summary>Shared shape of the generated cardinality validators.</summary>
internal static class ValidatorSupport
{
    internal static IReadOnlyList<Finos.Rune.Runtime.Validation.IValidationResult> Cardinality(
        string typeName,
        Finos.Rune.Runtime.Path.RosettaPath? path,
        params (string Name, int Actual, int Min, int Max)[] checks)
    {
        var results = new List<Finos.Rune.Runtime.Validation.IValidationResult>(checks.Length);
        foreach ((string name, int actual, int min, int max) in checks)
        {
            Finos.Rune.Runtime.Expression.ComparisonResult result =
                Finos.Rune.Runtime.Expression.RuneOperators.CheckCardinality(name, actual, min, max);
            if (string.IsNullOrEmpty(result.Error))
            {
                results.Add(Finos.Rune.Runtime.Validation.ValidationResult.Success(
                    typeName, Finos.Rune.Runtime.Validation.ValidationType.Cardinality, typeName, path, string.Empty));
            }
            else
            {
                results.Add(Finos.Rune.Runtime.Validation.ValidationResult.Failure(
                    typeName, Finos.Rune.Runtime.Validation.ValidationType.Cardinality, typeName, path, string.Empty,
                    result.Error));
            }
        }
        return results;
    }
}

public sealed class TestChildMeta : Finos.Rune.Runtime.Validation.RosettaMetaData<TestChild>
{
    public override Finos.Rune.Runtime.Validation.IValidator<TestChild>? Validator(
        Finos.Rune.Runtime.Validation.IValidatorFactory factory) => factory.Create<TestChildValidator>();
}

public sealed class TestSubMeta : Finos.Rune.Runtime.Validation.RosettaMetaData<TestSub>
{
    public override Finos.Rune.Runtime.Validation.IValidator<TestSub>? Validator(
        Finos.Rune.Runtime.Validation.IValidatorFactory factory) => factory.Create<TestSubValidator>();
}

public sealed class TestPojoMeta : Finos.Rune.Runtime.Validation.RosettaMetaData<TestPojo>
{
    public override Finos.Rune.Runtime.Validation.IValidator<TestPojo>? Validator(
        Finos.Rune.Runtime.Validation.IValidatorFactory factory) => factory.Create<TestPojoValidator>();
}

/// <summary>TestChoice has no validators, so it is walked through but contributes nothing.</summary>
public sealed class TestChoiceMeta : Finos.Rune.Runtime.Validation.RosettaMetaData<TestChoice>
{
}
