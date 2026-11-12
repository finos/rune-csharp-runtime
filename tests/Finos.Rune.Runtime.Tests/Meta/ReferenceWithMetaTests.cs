using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Meta;

public class ReferenceWithMetaTests
{
    [Fact]
    public void Build_WithAllThreeReferenceForms_PopulatesEachAndLeavesTheValueAbsent()
    {
        var reference = ReferenceWithMeta<string>.CreateBuilder()
            .SetGlobalReference("b6bdbfc2")
            .SetExternalReference("item-1")
            .SetScopedReference(Reference.CreateBuilder().SetScopedReference("someLocation"))
            .Build();

        Assert.Equal("b6bdbfc2", reference.GlobalReference);
        Assert.Equal("item-1", reference.ExternalReference);
        Assert.Equal("someLocation", reference.ScopedReference!.ScopedReference);
        Assert.Null(reference.Value);
    }

    [Fact]
    public void HasData_WhenAnySingleReferenceFormOrTheValueIsSet_IsTrue()
    {
        Assert.False(ReferenceWithMeta<string>.CreateBuilder().HasData());
        Assert.True(ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("g").HasData());
        Assert.True(ReferenceWithMeta<string>.CreateBuilder().SetExternalReference("e").HasData());
        Assert.True(ReferenceWithMeta<string>.CreateBuilder()
            .SetScopedReference(Reference.CreateBuilder().SetScopedReference("s")).HasData());
        Assert.True(ReferenceWithMeta<string>.CreateBuilder().SetValue("v").HasData());
    }

    [Fact]
    public void Prune_WithAnEmptyScopedReference_RemovesItAndLeavesTheGlobalReference()
    {
        var builder = ReferenceWithMeta<string>.CreateBuilder()
            .SetGlobalReference("g")
            .SetScopedReference(Reference.CreateBuilder());

        builder.Prune();

        Assert.Null(builder.ScopedReference);
        Assert.True(builder.HasData());
    }

    [Fact]
    public void Prune_WithAScopedReferenceHoldingOnlyPointsTo_LeavesTheWrapperWithoutData()
    {
        var builder = ReferenceWithMeta<string>.CreateBuilder();
        builder.GetOrCreateScopedReference().SetPointsTo("quantity-1");

        builder.Prune();

        Assert.Null(builder.ScopedReference);
        Assert.False(builder.HasData());
    }

    [Fact]
    public void HasData_WithAScopedReferenceMissingItsWireValue_IsFalse()
    {
        var builder = ReferenceWithMeta<string>.CreateBuilder();
        builder.GetOrCreateScopedReference().SetScope("DOCUMENT").SetPointsTo("quantity-1");

        Assert.False(builder.HasData());
    }

    [Fact]
    public void ToBuilder_ForAResolvedValueAndExternalReference_RebuildsBoth()
    {
        var reference = ReferenceWithMeta<string>.CreateBuilder()
            .SetValue("resolved")
            .SetExternalReference("someExternalKey")
            .Build();

        var rebuilt = reference.ToBuilder().Build();

        Assert.Equal("resolved", rebuilt.Value);
        Assert.Equal("someExternalKey", rebuilt.ExternalReference);
    }
}
