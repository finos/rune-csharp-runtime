using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Meta;

/// <summary>
/// <c>GetOrCreateValue</c> on the metadata wrappers, which a deep <c>set</c> walks through.
/// </summary>
/// <remarks>
/// <c>MetaFields</c> stands in for a wrapped model object here: the wrappers are generic over any
/// <c>IRosettaModelObject</c>, and the runtime assembly has no model of its own to reach for.
/// </remarks>
public class GetOrCreateValueTests
{
    [Fact]
    public void FieldWithMeta_WhenTheValueIsAbsent_CreatesABuilderAndReturnsTheSameOneAgain()
    {
        var builder = FieldWithMeta<MetaFields>.CreateBuilder();

        MetaFields.Builder value = builder.GetOrCreateValue(MetaFields.CreateBuilder);

        Assert.NotNull(value);
        Assert.Same(value, builder.GetOrCreateValue(MetaFields.CreateBuilder));
    }

    [Fact]
    public void FieldWithMeta_WhenMutatedThroughTheCreatedBuilder_ExposesTheChangeOnTheValue()
    {
        var builder = FieldWithMeta<MetaFields>.CreateBuilder();

        builder.GetOrCreateValue(MetaFields.CreateBuilder).SetGlobalKey("abc123");

        Assert.Equal("abc123", builder.Value!.GlobalKey);
    }

    [Fact]
    public void FieldWithMeta_WhenAValueIsAlreadyPresent_ReturnsItInsteadOfDiscardingIt()
    {
        var builder = FieldWithMeta<MetaFields>.CreateBuilder();
        builder.SetValue(MetaFields.CreateBuilder().SetGlobalKey("original").Build());

        MetaFields.Builder value = builder.GetOrCreateValue(MetaFields.CreateBuilder);

        Assert.Equal("original", value.GlobalKey);
    }

    [Fact]
    public void ReferenceWithMeta_WhenTheValueIsAbsent_KeepsMutationsAndReturnsOneStableBuilder()
    {
        var builder = ReferenceWithMeta<MetaFields>.CreateBuilder();

        builder.GetOrCreateValue(MetaFields.CreateBuilder).SetGlobalKey("xyz789");

        Assert.Equal("xyz789", builder.Value!.GlobalKey);
        Assert.Same(
            builder.GetOrCreateValue(MetaFields.CreateBuilder),
            builder.GetOrCreateValue(MetaFields.CreateBuilder));
    }
}
