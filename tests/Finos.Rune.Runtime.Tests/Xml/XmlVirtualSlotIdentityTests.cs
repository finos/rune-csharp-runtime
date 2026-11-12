using System.Xml;
using Finos.Rune.Runtime.Tests.Xml.Codecs;
using Finos.Rune.Runtime.Tests.Xml.Model;
using Finos.Rune.Runtime.Xml;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Xml;

/// <summary>
/// VIRTUAL slots are cached per (path prefix, content-model occurrence), and the occurrence must be
/// compared by <see cref="XmlOccurrenceKey"/> equality — which is frame-wise over <em>node
/// identity</em> — not by its rendered text.
/// </summary>
/// <remarks>
/// Two sibling groups of the same kind render identically (<c>SEQUENCE#0</c> and <c>SEQUENCE#0</c>)
/// while being different nodes. Keying the cache on the rendered occurrence therefore fuses them,
/// and leaves that XSD places in two separate occurrences of a VIRTUAL wrapper collapse into one
/// object. These tests pin both directions: same group fuses, sibling groups do not.
/// </remarks>
public class XmlVirtualSlotIdentityTests
{
    [Fact]
    public void OccurrenceKey_ForSiblingGroupsOfTheSameKind_IsDistinctEvenThoughItRendersAlike()
    {
        XmlRoutingResult result = XmlContentModelMatcher.Route(
            SiblingGroupModel(),
            new[]
            {
                new XmlRoutingInput(0, "s1", XmlNamespaceState.Unknown()),
                new XmlRoutingInput(1, "s2", XmlNamespaceState.Unknown()),
            });

        Assert.Equal(XmlRoutingStatus.Success, result.Status);
        XmlOccurrenceKey first = result.OccurrenceAt(0)!;
        XmlOccurrenceKey second = result.OccurrenceAt(1)!;

        Assert.Equal(first.ToString(), second.ToString());
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Read_ForSiblingSequenceChoiceAndAllGroups_RoutesEachIntoItsOwnVirtualSlot()
    {
        XmlCodecRegistry registry = Registry();
        const string Xml = "<SiblingGroupContainer>"
            + "<s1>S1</s1><s2>S2</s2><c1>C1</c1><c2>C2</c2><a1>A1</a1><a2>A2</a2>"
            + "</SiblingGroupContainer>";

        var actual = (LeafContainer)XmlDocumentReader.Read(Xml, registry, out XmlReadContext context);

        Assert.Equal(6, actual.Entry!.Count);
        Assert.Equal("S1", actual.Entry[0].FirstValue!.Value);
        Assert.Equal("S2", actual.Entry[1].SecondValue!.Value);
        Assert.Equal("C1", actual.Entry[2].FirstValue!.Value);
        Assert.Equal("C2", actual.Entry[3].SecondValue!.Value);
        Assert.Equal("A1", actual.Entry[4].FirstValue!.Value);
        Assert.Equal("A2", actual.Entry[5].SecondValue!.Value);
        Assert.All(actual.Entry, entry => Assert.True((entry.FirstValue == null) != (entry.SecondValue == null)));
        Assert.True(context.IsClean);
    }

    [Fact]
    public void Read_ForTwoLeavesInOneGroupIteration_StillSharesASingleVirtualSlot()
    {
        XmlCodecRegistry registry = Registry();
        const string Xml = "<SharedGroupContainer><s1>S1</s1><s2>S2</s2></SharedGroupContainer>";

        var actual = (LeafContainer)XmlDocumentReader.Read(Xml, registry, out _);

        Assert.Single(actual.Entry!);
        Assert.Equal("S1", actual.Entry![0].FirstValue!.Value);
        Assert.Equal("S2", actual.Entry[0].SecondValue!.Value);
    }

    [Fact]
    public void Read_ForARepeatedSharedGroup_KeepsOneSlotPerIteration()
    {
        XmlCodecRegistry registry = Registry();
        const string Xml = "<RepeatedGroupContainer>"
            + "<s1>A1</s1><s2>A2</s2><s1>B1</s1><s2>B2</s2>"
            + "</RepeatedGroupContainer>";

        var actual = (LeafContainer)XmlDocumentReader.Read(Xml, registry, out _);

        Assert.Equal(2, actual.Entry!.Count);
        Assert.Equal("A1", actual.Entry[0].FirstValue!.Value);
        Assert.Equal("A2", actual.Entry[0].SecondValue!.Value);
        Assert.Equal("B1", actual.Entry[1].FirstValue!.Value);
        Assert.Equal("B2", actual.Entry[1].SecondValue!.Value);
    }

    private static XmlCodecRegistry Registry() => TestCodecs.CreateRegistry().RegisterAll(
        SiblingEntryCodec.Instance,
        SiblingContainerCodec.SiblingGroups,
        SiblingContainerCodec.SharedGroup,
        SiblingContainerCodec.RepeatedGroup);

    /// <summary>
    /// Six sibling groups — two SEQUENCE, two CHOICE, two ALL — each routing one leaf into the same
    /// VIRTUAL <c>entry</c> property. Each pair renders identically and is a different node.
    /// </summary>
    private static XmlContentModelNode SiblingGroupModel() => XmlContentModelNode.Sequence(
        1,
        XmlOccursMax.Of(1),
        XmlContentModelNode.Sequence(0, XmlOccursMax.Of(1), Leaf("s1", "firstValue")),
        XmlContentModelNode.Sequence(0, XmlOccursMax.Of(1), Leaf("s2", "secondValue")),
        XmlContentModelNode.Choice(0, XmlOccursMax.Of(1), Leaf("c1", "firstValue")),
        XmlContentModelNode.Choice(0, XmlOccursMax.Of(1), Leaf("c2", "secondValue")),
        XmlContentModelNode.All(0, XmlOccursMax.Of(1), Leaf("a1", "firstValue")),
        XmlContentModelNode.All(0, XmlOccursMax.Of(1), Leaf("a2", "secondValue")));

    /// <summary>Both leaves inside one group, so they belong to one occurrence.</summary>
    private static XmlContentModelNode SharedGroupModel() => XmlContentModelNode.Sequence(
        1, XmlOccursMax.Of(1), Leaf("s1", "firstValue"), Leaf("s2", "secondValue"));

    /// <summary>The same shared group, repeated: one occurrence per iteration.</summary>
    private static XmlContentModelNode RepeatedGroupModel() => new XmlContentModelNode(
        XmlContentModelNodeType.Sequence,
        minOccurs: 0,
        maxOccurs: XmlOccursMax.Unbounded,
        children: new[] { Leaf("s1", "firstValue"), Leaf("s2", "secondValue") });

    private static XmlContentModelNode Leaf(string xmlName, string property) =>
        XmlContentModelNode.Element(xmlName, new[] { "entry", property }, 1, XmlOccursMax.Of(1));

    /// <summary>Maps the six leaf element names onto the two properties of a leaf entry.</summary>
    private sealed class SiblingEntryCodec : XmlTypeCodec<LeafEntry, LeafEntry.Builder>
    {
        internal static readonly SiblingEntryCodec Instance = new SiblingEntryCodec();

        private SiblingEntryCodec()
            : base(new XmlTypeDescriptor(
                "com.rosetta.test.SiblingLeafEntry",
                new[]
                {
                    new XmlPropertyDescriptor("firstValue", new XmlQName("firstValue")),
                    new XmlPropertyDescriptor("secondValue", new XmlQName("secondValue")),
                }))
        {
        }

        public override LeafEntry.Builder CreateTypedBuilder() => LeafEntry.CreateBuilder();

        protected override bool TryReadElement(
            LeafEntry.Builder builder, XmlQName name, XmlReader reader, XmlReadContext context)
        {
            FpmlTextValue.Builder text = context.ReadObject<FpmlTextValue.Builder>(reader, FpmlTextValueCodec.Instance);
            switch (name.LocalName)
            {
                case "s1" or "c1" or "a1":
                    builder.SetFirstValue(text);
                    return true;
                case "s2" or "c2" or "a2":
                    builder.SetSecondValue(text);
                    return true;
                default:
                    return false;
            }
        }
    }

    private sealed class SiblingContainerCodec : XmlTypeCodec<LeafContainer, LeafContainer.Builder>
    {
        internal static readonly SiblingContainerCodec SiblingGroups = new SiblingContainerCodec(
            "SiblingGroupContainer", SiblingGroupModel());

        internal static readonly SiblingContainerCodec SharedGroup = new SiblingContainerCodec(
            "SharedGroupContainer", SharedGroupModel());

        internal static readonly SiblingContainerCodec RepeatedGroup = new SiblingContainerCodec(
            "RepeatedGroupContainer", RepeatedGroupModel());

        private SiblingContainerCodec(string elementName, XmlContentModelNode contentModel)
            : base(new XmlTypeDescriptor(
                "com.rosetta.test." + elementName,
                new[]
                {
                    new XmlPropertyDescriptor(
                        "entry", new XmlQName("entry"), XmlPropertyRepresentation.Virtual, isMulti: true),
                },
                elementName: new XmlQName(elementName),
                contentModel: contentModel))
        {
        }

        public override LeafContainer.Builder CreateTypedBuilder() => LeafContainer.CreateBuilder();

        public override IXmlTypeCodec? PropertyCodec(string propertyName) =>
            string.Equals(propertyName, "entry", StringComparison.Ordinal) ? SiblingEntryCodec.Instance : null;

        protected override RosettaModelObjectBuilder? CreateVirtualSlot(
            LeafContainer.Builder builder, string propertyName, XmlReadContext context) =>
            string.Equals(propertyName, "entry", StringComparison.Ordinal) ? builder.AddEntry() : null;
    }
}
