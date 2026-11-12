using Finos.Rune.Runtime.Hashing;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Tests.PostProcess;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Hashing;

/// <summary>
/// What a reference wrapper contributes to its parent's global key.
/// </summary>
/// <remarks>
/// Read off <c>ReferenceWithMetaDate.process</c> in the published artifact, which is unambiguous about
/// which of the wrapper's four members carry <c>AttributeMeta.META</c>:
/// <code>
/// processBasic(path.newSubPath("value"),             Date.class,   getValue(),             this);
/// processBasic(path.newSubPath("globalReference"),   String.class, getGlobalReference(),   this, META);
/// processBasic(path.newSubPath("externalReference"), String.class, getExternalReference(), this, META);
/// processRosetta(path.newSubPath("reference"),       Reference.class, getReference(),      this);
/// </code>
/// <para>
/// So <c>value</c> is an ordinary hashed attribute; <c>globalReference</c> is excluded;
/// <c>externalReference</c> is excluded by the tag and let straight back in by
/// <c>NonNullHashCollector.isExternalKeyOrReference</c>.
/// </para>
/// <para>
/// <b>The test pack cannot reach this.</b> Every reference wrapper on the wire has its value suppressed,
/// so a rule that wrongly excluded <c>value</c> stayed invisible until reference resolution started
/// populating basic values — at which point it silently changed the key of every ancestor of a resolved
/// <c>dateRelativeTo</c>.
/// </para>
/// </remarks>
public class ReferenceWrapperHashTests
{
    private static RefNode NodeWith(ReferenceWithMeta<string>.Builder labelRef) =>
        RefNode.CreateBuilder().SetName("n").SetLabelRef(labelRef).Build();

    [Fact]
    public void Hash_WhenABasicReferenceValueIsPopulated_Changes()
    {
        RefNode withoutValue = NodeWith(
            ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("k"));
        RefNode withValue = NodeWith(
            ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("k").SetValue("resolved"));

        Assert.NotEqual(GlobalKeyHasher.Hash(withoutValue), GlobalKeyHasher.Hash(withValue));
    }

    [Fact]
    public void Hash_ForTwoDifferentGlobalReferencesWithTheSameValue_IsTheSame()
    {
        RefNode first = NodeWith(
            ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("k1").SetValue("resolved"));
        RefNode second = NodeWith(
            ReferenceWithMeta<string>.CreateBuilder().SetGlobalReference("k2").SetValue("resolved"));

        Assert.Equal(GlobalKeyHasher.Hash(first), GlobalKeyHasher.Hash(second));
    }

    [Fact]
    public void Hash_ForTwoDifferentExternalReferencesWithTheSameValue_Differs()
    {
        RefNode first = NodeWith(
            ReferenceWithMeta<string>.CreateBuilder().SetExternalReference("e1").SetValue("resolved"));
        RefNode second = NodeWith(
            ReferenceWithMeta<string>.CreateBuilder().SetExternalReference("e2").SetValue("resolved"));

        Assert.NotEqual(GlobalKeyHasher.Hash(first), GlobalKeyHasher.Hash(second));
    }

    [Fact]
    public void Hash_ForABasicValueUnderAScopedReferenceWrapper_StillCountsIt()
    {
        RefNode withoutValue = NodeWith(ReferenceWithMeta<string>.CreateBuilder()
            .SetScopedReference(Reference.CreateBuilder().SetScopedReference("s")));
        RefNode withValue = NodeWith(ReferenceWithMeta<string>.CreateBuilder()
            .SetScopedReference(Reference.CreateBuilder().SetScopedReference("s")).SetValue("resolved"));

        Assert.NotEqual(GlobalKeyHasher.Hash(withoutValue), GlobalKeyHasher.Hash(withValue));
    }

    [Fact]
    public void Hash_ForAModelValueUnderAScopedReferenceWrapper_IgnoresItEntirely()
    {
        RefNode target = RefNode.CreateBuilder().SetName("target").Build();
        RefNode withoutValue = RefNode.CreateBuilder().SetName("n").SetLink(
            ReferenceWithMeta<RefNode>.CreateBuilder()
                .SetScopedReference(Reference.CreateBuilder().SetScopedReference("s"))).Build();
        RefNode withValue = RefNode.CreateBuilder().SetName("n").SetLink(
            ReferenceWithMeta<RefNode>.CreateBuilder()
                .SetScopedReference(Reference.CreateBuilder().SetScopedReference("s"))
                .SetValue(target)).Build();

        Assert.Equal(GlobalKeyHasher.Hash(withoutValue), GlobalKeyHasher.Hash(withValue));
    }
}
