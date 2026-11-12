using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Meta;

public class FieldWithMetaTests
{
    [Fact]
    public void Build_WithAValueAndAScheme_PopulatesBoth()
    {
        var field = FieldWithMeta<string>.CreateBuilder()
            .SetValue("GB")
            .SetMeta(MetaFields.CreateBuilder().SetScheme("urn:iso:std:iso:3166"))
            .Build();

        Assert.Equal("GB", field.Value);
        Assert.Equal("urn:iso:std:iso:3166", field.Meta!.Scheme);
    }

    [Fact]
    public void ToBuilder_ForAValueCarryingAGlobalKey_RebuildsBoth()
    {
        var field = FieldWithMeta<string>.CreateBuilder()
            .SetValue("GB")
            .SetMeta(MetaFields.CreateBuilder().SetGlobalKey("abc"))
            .Build();

        var rebuilt = field.ToBuilder().Build();

        Assert.Equal("GB", rebuilt.Value);
        Assert.Equal("abc", rebuilt.Meta!.GlobalKey);
    }

    [Fact]
    public void NullableValueTypePayload_WhenTheDateIsSet_CountsAsDataAndSurvivesBuild()
    {
        var builder = FieldWithMeta<DateOnly?>.CreateBuilder();
        Assert.False(builder.HasData());

        builder.SetValue(new DateOnly(2017, 12, 18));
        Assert.True(builder.HasData());
        Assert.Equal(new DateOnly(2017, 12, 18), builder.Build().Value);
    }

    [Fact]
    public void HasData_WithMetaButNoValue_IsFalse()
    {
        var builder = FieldWithMeta<string>.CreateBuilder()
            .SetMeta(MetaFields.CreateBuilder().SetScheme("scheme"));

        Assert.False(builder.HasData());

        builder.SetValue("x");
        Assert.True(builder.HasData());
    }

    [Fact]
    public void Prune_WithEmptyMeta_RemovesItAndKeepsTheValue()
    {
        var builder = FieldWithMeta<string>.CreateBuilder()
            .SetValue("x")
            .SetMeta(MetaFields.CreateBuilder());

        builder.Prune();

        Assert.Null(builder.Meta);
        Assert.True(builder.HasData());
    }

    [Fact]
    public void Prune_WithMetaCarryingAnExternalKey_LeavesItInPlace()
    {
        var builder = FieldWithMeta<string>.CreateBuilder()
            .SetValue("x")
            .SetMeta(MetaFields.CreateBuilder().SetExternalKey("k"));

        builder.Prune();

        Assert.NotNull(builder.Meta);
        Assert.Equal("k", builder.Meta!.ExternalKey);
    }
}
