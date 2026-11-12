using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Metafields;

public class MetaFieldsTests
{
    [Fact]
    public void ToBuilder_WithSchemeAndAllThreeKeys_RebuildsEveryField()
    {
        var meta = MetaFields.CreateBuilder()
            .SetScheme("urn:iso:std:iso:3166")
            .SetGlobalKey("3f0b12")
            .SetExternalKey("ext-1")
            .SetScopedKey("scoped-1")
            .Build();

        var rebuilt = meta.ToBuilder().Build();

        Assert.Equal("urn:iso:std:iso:3166", rebuilt.Scheme);
        Assert.Equal("3f0b12", rebuilt.GlobalKey);
        Assert.Equal("ext-1", rebuilt.ExternalKey);
        Assert.Equal("scoped-1", rebuilt.ScopedKey);
    }

    [Fact]
    public void HasData_WhenAnySingleFieldIsSet_IsTrue()
    {
        Assert.False(MetaFields.CreateBuilder().HasData());
        Assert.True(MetaFields.CreateBuilder().SetScheme("s").HasData());
        Assert.True(MetaFields.CreateBuilder().SetGlobalKey("g").HasData());
        Assert.True(MetaFields.CreateBuilder().SetExternalKey("e").HasData());
        Assert.True(MetaFields.CreateBuilder().SetScopedKey("k").HasData());
    }
}
