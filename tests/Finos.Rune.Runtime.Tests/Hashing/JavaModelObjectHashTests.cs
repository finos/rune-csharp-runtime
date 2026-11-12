using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Hashing;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Hashing;

public class JavaModelObjectHashTests
{
    [Fact]
    public void Hash_WhenAValueIsTypedAsItsBase_UsesTheRuntimeSubtypesProperties()
    {
        HashDerived.Builder builder = HashDerived.CreateBuilder();
        builder.SetName("base");
        builder.SetExtra("derived");
        HashBase value = builder.Build();

        int expected = Add(Add(0, JavaHash.Of("base")), JavaHash.Of("derived"));

        Assert.Equal(expected, JavaModelObjectHash.Of(value));
    }

    [Fact]
    public void Hash_ForFpmlPlatformValues_UsesGeneratedJavaHashCodesInDeclarationOrder()
    {
        DateOnly date = new(2024, 2, 29);
        DateTimeOffset zoned = new(2030, 6, 15, 0, 0, 0, TimeSpan.FromMinutes(330));
        HashValues value = HashValues.CreateBuilder()
            .SetText("text")
            .SetNumber(1.25m)
            .SetFlag(true)
            .SetDate(date)
            .SetZoned(zoned)
            .SetNames(["x", "y"])
            .Build();

        int expected = 0;
        expected = Add(expected, JavaHash.Of("text"));
        expected = Add(expected, 3877);       // new BigDecimal("1.25").hashCode()
        expected = Add(expected, 1231);       // Boolean.TRUE.hashCode()
        expected = Add(expected, 59746);      // Date.of(2024, 2, 29).hashCode()
        expected = Add(expected, 4019735);    // ZonedDateTime 2030-06-15T00:00+05:30
        expected = Add(expected, JavaListHash("x", "y"));

        Assert.Equal(expected, JavaModelObjectHash.Of(value));
    }

    [Fact]
    public void Hash_ForANonFpmlModel_ThrowsRatherThanClaimingUnsupportedParity()
    {
        NonFpmlValue value = NonFpmlValue.CreateBuilder().SetValue("x").Build();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => JavaModelObjectHash.Of(value));

        Assert.Contains("only for FpML", thrown.Message, StringComparison.Ordinal);
    }

    private static int JavaListHash(params string[] values)
    {
        int result = 1;
        foreach (string value in values)
        {
            result = Add(result, JavaHash.Of(value));
        }
        return result;
    }

    private static int Add(int result, int contribution)
    {
        unchecked
        {
            return (31 * result) + contribution;
        }
    }

    [RuneDataType("HashBase", "fpml.test", "0.0.0")]
    private class HashBase : RosettaModelObject
    {
        protected HashBase(Builder builder)
        {
            Name = builder.Name;
        }

        [RuneAttribute("name")]
        public string? Name { get; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => new Builder().SetName(Name);

        public class Builder : RosettaModelObjectBuilder
        {
            public string? Name { get; private set; }

            public Builder SetName(string? value)
            {
                Name = value;
                return this;
            }

            public override HashBase Build() => new(this);

            public override Builder Prune() => this;

            public override bool HasData() => Name != null;
        }
    }

    [RuneDataType("HashDerived", "fpml.test", "0.0.0")]
    private sealed class HashDerived : HashBase
    {
        private HashDerived(Builder builder)
            : base(builder)
        {
            Extra = builder.Extra;
        }

        [RuneAttribute("extra")]
        public string? Extra { get; }

        public static new Builder CreateBuilder() => new();

        public override Builder ToBuilder()
        {
            var builder = new Builder();
            builder.SetName(Name);
            builder.SetExtra(Extra);
            return builder;
        }

        public new sealed class Builder : HashBase.Builder
        {
            public string? Extra { get; private set; }

            public Builder SetExtra(string? value)
            {
                Extra = value;
                return this;
            }

            public override HashDerived Build() => new(this);

            public override Builder Prune() => this;

            public override bool HasData() => base.HasData() || Extra != null;
        }
    }

    [RuneDataType("HashValues", "fpml.test", "0.0.0")]
    private sealed class HashValues : RosettaModelObject
    {
        private HashValues(Builder builder)
        {
            Text = builder.Text;
            Number = builder.Number;
            Flag = builder.Flag;
            Date = builder.Date;
            Zoned = builder.Zoned;
            Names = builder.Names.Count == 0 ? null : builder.Names.ToArray();
        }

        [RuneAttribute("text")]
        public string? Text { get; }

        [RuneAttribute("number")]
        public decimal? Number { get; }

        [RuneAttribute("flag")]
        public bool? Flag { get; }

        [RuneAttribute("date")]
        public DateOnly? Date { get; }

        [RuneAttribute("zoned")]
        public DateTimeOffset? Zoned { get; }

        [RuneAttribute("names")]
        public IReadOnlyList<string>? Names { get; }

        public static Builder CreateBuilder() => new();

        public override Builder ToBuilder() => throw new NotSupportedException();

        public sealed class Builder : RosettaModelObjectBuilder
        {
            public string? Text { get; private set; }
            public decimal? Number { get; private set; }
            public bool? Flag { get; private set; }
            public DateOnly? Date { get; private set; }
            public DateTimeOffset? Zoned { get; private set; }
            public List<string> Names { get; } = [];

            public Builder SetText(string? value) { Text = value; return this; }
            public Builder SetNumber(decimal? value) { Number = value; return this; }
            public Builder SetFlag(bool? value) { Flag = value; return this; }
            public Builder SetDate(DateOnly? value) { Date = value; return this; }
            public Builder SetZoned(DateTimeOffset? value) { Zoned = value; return this; }
            public Builder SetNames(IEnumerable<string> values) { Names.AddRange(values); return this; }
            public override HashValues Build() => new(this);
            public override Builder Prune() => this;
            public override bool HasData() => true;
        }
    }

    [RuneDataType("NonFpmlValue", "test.model", "0.0.0")]
    private sealed class NonFpmlValue : RosettaModelObject
    {
        private NonFpmlValue(Builder builder)
        {
            Value = builder.Value;
        }

        [RuneAttribute("value")]
        public string? Value { get; }

        public static Builder CreateBuilder() => new();
        public override Builder ToBuilder() => new Builder().SetValue(Value);

        public sealed class Builder : RosettaModelObjectBuilder
        {
            public string? Value { get; private set; }
            public Builder SetValue(string? value) { Value = value; return this; }
            public override NonFpmlValue Build() => new(this);
            public override Builder Prune() => this;
            public override bool HasData() => Value != null;
        }
    }
}
