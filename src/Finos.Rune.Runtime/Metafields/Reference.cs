using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Metafields;

/// <summary>
/// A scoped reference pointing at a scoped key elsewhere in the same document.
/// Mirrors com.rosetta.model.lib.meta.Reference; serialized unwrapped, its value
/// surfacing as "@ref:scoped".
/// </summary>
public sealed class Reference : RosettaModelObject
{
    private Reference(Builder builder)
    {
        Scope = builder.Scope;
        PointsTo = builder.PointsTo;
        ScopedReference = builder.ScopedReference;
    }

    /// <summary>
    /// Resolution scope. Populated by reference-resolution machinery, not by document
    /// authors: it has no wire binding (Java annotates it @RosettaAttribute("scope") but
    /// not @RuneAttribute) and so never serializes.
    /// </summary>
    public string? Scope { get; }

    /// <summary>
    /// Name of the attribute the reference resolves against. Populated by
    /// reference-resolution machinery, not by document authors: it carries no annotation
    /// at all in Java and never serializes.
    /// </summary>
    public string? PointsTo { get; }

    /// <summary>
    /// The reference value — this is the wire-bound attribute, surfacing as
    /// "@ref:scoped". Java calls it <c>reference</c>; renamed here because a
    /// <c>Reference.Reference</c> member is illegal in C# (CS0542).
    /// </summary>
    // Java uses "address" here but "reference" inside ReferenceWithMeta.
    [RuneAttribute(WireNames.ScopedReference)]
    [RunePathName("address")]
    public string? ScopedReference { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetScope(Scope);
        builder.SetPointsTo(PointsTo);
        builder.SetScopedReference(ScopedReference);
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        /// <summary>Resolution scope; not wire-bound, see <see cref="Reference.Scope"/>.</summary>
        public string? Scope { get; private set; }

        /// <summary>Resolution target attribute; not wire-bound, see <see cref="Reference.PointsTo"/>.</summary>
        public string? PointsTo { get; private set; }

        /// <summary>The wire-bound reference value ("@ref:scoped"). This is the one to set
        /// when authoring a scoped reference.</summary>
        public string? ScopedReference { get; private set; }

        public Builder SetScope(string? scope)
        {
            Scope = scope;
            return this;
        }

        public Builder SetPointsTo(string? pointsTo)
        {
            PointsTo = pointsTo;
            return this;
        }

        public Builder SetScopedReference(string? scopedReference)
        {
            ScopedReference = scopedReference;
            return this;
        }

        public override Reference Build() => new Reference(this);

        public override Builder Prune() => this;

        /// <summary>
        /// Only the wire-bound reference counts as data, mirroring
        /// ReferenceBuilderImpl.hasData() (<c>getReference() != null</c>). Scope and
        /// PointsTo have no serialization binding, so a Reference carrying only those has
        /// nothing to emit and must prune away rather than serialize as an empty object.
        /// </summary>
        public override bool HasData() => ScopedReference != null;
    }
}
