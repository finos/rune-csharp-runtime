using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Metafields;

public class KeyTests
{
    [Fact]
    public void ToBuilder_WithScopeAndKeyValue_RebuildsBoth()
    {
        var key = Key.CreateBuilder()
            .SetScope("DOCUMENT")
            .SetKeyValue("quantity-1")
            .Build();

        var rebuilt = key.ToBuilder().Build();

        Assert.Equal("DOCUMENT", rebuilt.Scope);
        Assert.Equal("quantity-1", rebuilt.KeyValue);
    }

    [Fact]
    public void HasData_UnlessTheKeyValueIsSet_IsFalse()
    {
        Assert.False(Key.CreateBuilder().HasData());
        Assert.False(Key.CreateBuilder().SetScope("DOCUMENT").HasData());
        Assert.True(Key.CreateBuilder().SetKeyValue("quantity-1").HasData());
        Assert.True(Key.CreateBuilder().SetScope("DOCUMENT").SetKeyValue("quantity-1").HasData());
    }
}
