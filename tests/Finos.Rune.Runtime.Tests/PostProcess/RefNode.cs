using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime.Tests.PostProcess;

/// <summary>
/// A self-referential miniature model: a node with a name, nested children and one reference to another
/// node.
/// </summary>
/// <remarks>
/// Deliberately separate from <c>TestModel</c>: the shape this exists to exercise — <b>a reference whose
/// target itself carries a reference</b> — needs a type that can point at its own kind, and adding that to
/// <c>TestPojo</c> would change the attribute surface every serializer test reads.
/// </remarks>
[RuneDataType("RefNode", "test.model", "0.0.0")]
public class RefNode : RosettaModelObject, IGlobalKey
{
    protected RefNode(Builder builder)
    {
        Name = builder.Name;
        Nested = builder.Nested.Count == 0
            ? null
            : builder.Nested.FindAll(item => item != null).ConvertAll(item => item.Build()).AsReadOnly();
        Link = builder.Link?.Build();
        Label = builder.Label?.Build();
        LabelRef = builder.LabelRef?.Build();
        Meta = builder.Meta?.Build();
    }

    [RuneAttribute("name")]
    public string? Name { get; }

    [RuneAttribute("nested")]
    public IReadOnlyList<RefNode>? Nested { get; }

    [RuneAttribute("link")]
    public ReferenceWithMeta<RefNode>? Link { get; }

    /// <summary>A keyed basic value — the shape a <c>[metadata id]</c> string attribute takes.</summary>
    [RuneAttribute("label")]
    public FieldWithMeta<string>? Label { get; }

    /// <summary>A reference to a basic value, Java's <c>BasicReferenceWithMetaString</c>.</summary>
    [RuneAttribute("labelRef")]
    public ReferenceWithMeta<string>? LabelRef { get; }

    [RuneAttribute("meta")]
    [RuneMetaType]
    public MetaFields? Meta { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetName(Name);
        builder.SetNested(Nested);
        builder.SetLink(Link);
        builder.SetLabel(Label);
        builder.SetLabelRef(LabelRef);
        builder.SetMeta(Meta);
        return builder;
    }

    public class Builder : RosettaModelObjectBuilder
    {
        public string? Name { get; private set; }

        public List<RefNode.Builder> Nested { get; } = new();

        public ReferenceWithMeta<RefNode>.Builder? Link { get; private set; }

        public FieldWithMeta<string>.Builder? Label { get; private set; }

        public ReferenceWithMeta<string>.Builder? LabelRef { get; private set; }

        public MetaFields.Builder? Meta { get; private set; }

        public Builder SetName(string? name)
        {
            Name = name;
            return this;
        }

        public Builder SetNested(IEnumerable<RefNode>? nested)
        {
            Nested.Clear();
            if (nested != null)
            {
                Nested.AddRange(nested.Select(item => item.ToBuilder()));
            }

            return this;
        }

        public Builder SetNested(IEnumerable<RefNode.Builder>? nested)
        {
            Nested.Clear();
            if (nested != null)
            {
                Nested.AddRange(nested);
            }

            return this;
        }

        public Builder AddNested(RefNode node)
        {
            Nested.Add(node.ToBuilder());
            return this;
        }

        public Builder SetLink(ReferenceWithMeta<RefNode>? link)
        {
            Link = link?.ToBuilder();
            return this;
        }

        public Builder SetLink(ReferenceWithMeta<RefNode>.Builder? link)
        {
            Link = link;
            return this;
        }

        public Builder SetLabel(FieldWithMeta<string>? label)
        {
            Label = label?.ToBuilder();
            return this;
        }

        public Builder SetLabel(FieldWithMeta<string>.Builder? label)
        {
            Label = label;
            return this;
        }

        public Builder SetLabelRef(ReferenceWithMeta<string>? labelRef)
        {
            LabelRef = labelRef?.ToBuilder();
            return this;
        }

        public Builder SetLabelRef(ReferenceWithMeta<string>.Builder? labelRef)
        {
            LabelRef = labelRef;
            return this;
        }

        public Builder SetMeta(MetaFields? meta)
        {
            Meta = meta?.ToBuilder();
            return this;
        }

        public Builder SetMeta(MetaFields.Builder? meta)
        {
            Meta = meta;
            return this;
        }

        public override RefNode Build() => new RefNode(this);

        public override Builder Prune()
        {
            Nested.RemoveAll(item => !item.Prune().HasData());
            if (Link != null && !Link.Prune().HasData())
            {
                Link = null;
            }

            if (Label != null && !Label.Prune().HasData())
            {
                Label = null;
            }

            if (LabelRef != null && !LabelRef.Prune().HasData())
            {
                LabelRef = null;
            }

            if (Meta != null && !Meta.Prune().HasData())
            {
                Meta = null;
            }

            return this;
        }

        public override bool HasData() =>
            Name != null || Nested.Count > 0 || (Link?.HasData() ?? false)
            || (Label?.HasData() ?? false) || (LabelRef?.HasData() ?? false);
    }
}
