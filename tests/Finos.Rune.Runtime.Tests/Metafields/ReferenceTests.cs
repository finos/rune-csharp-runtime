using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Metafields;

public class ReferenceTests
{
    [Fact]
    public void ToBuilder_WithAllThreeMembersSet_RebuildsEachOne()
    {
        var reference = Reference.CreateBuilder()
            .SetScope("DOCUMENT")
            .SetPointsTo("quantity")
            .SetScopedReference("quantity-1")
            .Build();

        var rebuilt = reference.ToBuilder().Build();

        Assert.Equal("DOCUMENT", rebuilt.Scope);
        Assert.Equal("quantity", rebuilt.PointsTo);
        Assert.Equal("quantity-1", rebuilt.ScopedReference);
    }

    [Fact]
    public void HasData_UnlessTheScopedReferenceIsSet_IsFalse()
    {
        Assert.False(Reference.CreateBuilder().HasData());
        Assert.False(Reference.CreateBuilder().SetPointsTo("quantity-1").HasData());
        Assert.False(Reference.CreateBuilder().SetScope("DOCUMENT").HasData());
        Assert.False(Reference.CreateBuilder().SetScope("DOCUMENT").SetPointsTo("quantity").HasData());
        Assert.True(Reference.CreateBuilder().SetScopedReference("quantity-1").HasData());
        Assert.True(Reference.CreateBuilder().SetScope("DOCUMENT").SetScopedReference("quantity-1").HasData());
    }

    [Fact]
    public void Prune_WithOnlyPointsToSet_LeavesNoDataButKeepsTheFieldReadable()
    {
        var builder = Reference.CreateBuilder().SetPointsTo("quantity-1");

        Assert.False(builder.Prune().HasData());
        Assert.Equal("quantity-1", builder.PointsTo);
    }

    [Fact]
    public void HasData_AfterTheScopedReferenceIsSetBackToNull_IsFalse()
    {
        var builder = Reference.CreateBuilder().SetScopedReference("quantity-1");
        Assert.True(builder.HasData());

        builder.SetScopedReference(null);

        Assert.False(builder.HasData());
    }
}
