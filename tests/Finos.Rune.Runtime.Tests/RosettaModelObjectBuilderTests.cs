using Finos.Rune.Runtime.Metafields;
using Xunit;

namespace Finos.Rune.Runtime.Tests;

public class RosettaModelObjectBuilderTests
{
    private sealed class Harness : RosettaModelObjectBuilder
    {
        public override RosettaModelObject Build() => throw new NotSupportedException();

        public override RosettaModelObjectBuilder Prune() => this;

        public override bool HasData() => false;

        public static T CallGetIndex<T>(List<T> list, int index, Func<T> factory) =>
            GetIndex(list, index, factory);

        public static void CallPruneList<T>(List<T> list) where T : RosettaModelObjectBuilder =>
            PruneList(list);
    }

    [Fact]
    public void GetIndex_ForAnIndexOfMinusOne_AppendsANewEntryPerCall()
    {
        var list = new List<MetaFields.Builder>();
        var first = Harness.CallGetIndex(list, -1, MetaFields.CreateBuilder);
        var second = Harness.CallGetIndex(list, -1, MetaFields.CreateBuilder);

        Assert.Equal(2, list.Count);
        Assert.Same(first, list[0]);
        Assert.Same(second, list[1]);
    }

    [Fact]
    public void GetIndex_ForIntMaxValue_AppendsJustAsMinusOneDoes()
    {
        var list = new List<MetaFields.Builder>();
        var appended = Harness.CallGetIndex(list, int.MaxValue, MetaFields.CreateBuilder);

        Assert.Single(list);
        Assert.Same(appended, list[0]);
    }

    [Fact]
    public void GetIndex_ForAnIndexPastTheEnd_PadsTheGapWithNulls()
    {
        var list = new List<MetaFields.Builder>();
        var created = Harness.CallGetIndex(list, 2, MetaFields.CreateBuilder);

        Assert.Equal(3, list.Count);
        Assert.Null(list[0]);
        Assert.Null(list[1]);
        Assert.Same(created, list[2]);
    }

    [Fact]
    public void GetIndex_ForAPaddedNullSlot_ReplacesItWithANewBuilder()
    {
        var list = new List<MetaFields.Builder>();
        Harness.CallGetIndex(list, 2, MetaFields.CreateBuilder);
        var filled = Harness.CallGetIndex(list, 0, MetaFields.CreateBuilder);

        Assert.NotNull(filled);
        Assert.Same(filled, list[0]);
    }

    [Fact]
    public void GetIndex_ForAnAlreadyPopulatedSlot_ReturnsThatEntryUntouched()
    {
        var list = new List<MetaFields.Builder> { MetaFields.CreateBuilder().SetScheme("s") };
        var existing = Harness.CallGetIndex(list, 0, MetaFields.CreateBuilder);

        Assert.Single(list);
        Assert.Equal("s", existing.Scheme);
    }

    [Fact]
    public void PruneList_WhenOnlyOneEntryHasData_KeepsThatEntryAlone()
    {
        var populated = MetaFields.CreateBuilder().SetGlobalKey("keep");
        var list = new List<MetaFields.Builder>
        {
            MetaFields.CreateBuilder(),
            populated,
            MetaFields.CreateBuilder(),
        };

        Harness.CallPruneList(list);

        Assert.Single(list);
        Assert.Same(populated, list[0]);
    }

    [Fact]
    public void PruneList_ForANullPaddedList_DropsThePaddingAndCompacts()
    {
        var list = new List<MetaFields.Builder>();
        var created = Harness.CallGetIndex(list, 2, MetaFields.CreateBuilder);
        created.SetGlobalKey("keep");

        Harness.CallPruneList(list);

        Assert.Single(list);
        Assert.Same(created, list[0]);
    }
}
