using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Meta;

/// <summary>
/// Reference wrapper for attributes annotated [metadata reference] or [metadata address]:
/// holds either the resolved value or one of three reference forms (global, external,
/// scoped). A single reified generic replaces Java's per-type ReferenceWithMetaX /
/// BasicReferenceWithMetaX; generated code instantiates T as the optional form of the
/// value type. On the wire: value → "@data" (flattened when a model object),
/// globalReference → "@ref", externalReference → "@ref:external", scoped reference
/// unwrapped as "@ref:scoped".
/// </summary>
public sealed class ReferenceWithMeta<T> : RosettaModelObject, IReferenceWithMeta<T>, IReferenceWithMetaView
{
    private ReferenceWithMeta(Builder builder)
    {
        Value = builder.Value;
        GlobalReference = builder.GlobalReference;
        ExternalReference = builder.ExternalReference;
        ScopedReference = builder.ScopedReference?.Build();
    }

    object? IReferenceWithMetaView.ValueObject => Value;

    string? IReferenceWithMetaView.GlobalReferenceValue => GlobalReference;

    string? IReferenceWithMetaView.ExternalReferenceValue => ExternalReference;

    Reference? IReferenceWithMetaView.ScopedReferenceObject => ScopedReference;

    Type IReferenceWithMetaView.ValueType => Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

    [RuneAttribute(WireNames.Data)]
    [RunePathName("value")]
    public T? Value { get; }

    [RuneAttribute(WireNames.Reference)]
    [RunePathName("globalReference")]
    public string? GlobalReference { get; }

    [RuneAttribute(WireNames.ExternalReference)]
    [RunePathName("externalReference")]
    public string? ExternalReference { get; }

    [RuneAttribute(WireNames.ScopedReference)]
    [RunePathName("reference")]
    [RuneMetaType]
    public Reference? ScopedReference { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetValue(Value);
        builder.SetGlobalReference(GlobalReference);
        builder.SetExternalReference(ExternalReference);
        if (ScopedReference != null)
        {
            builder.SetScopedReference(ScopedReference.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        private T? _value;
        private IRosettaModelObjectBuilder? _valueBuilder;

        public T? Value => _valueBuilder != null ? (T)_valueBuilder.Build() : _value;

        public string? GlobalReference { get; private set; }

        public string? ExternalReference { get; private set; }

        public Reference.Builder? ScopedReference { get; private set; }

        public Builder SetValue(T? value)
        {
            if (value is IRosettaModelObject model)
            {
                _valueBuilder = model.ToBuilder();
                _value = default;
            }
            else
            {
                _value = value;
                _valueBuilder = null;
            }
            return this;
        }

        /// <summary>The referenced value's builder, created if absent.</summary>
        /// <remarks>
        /// The counterpart of a generated builder's <c>GetOrCreateX()</c>, and of the reference's
        /// <c>getOrCreateValue()</c>. A deep <c>set</c> whose path runs through a metadata-wrapped attribute
        /// needs this hop: the wrapper is what the parent holds, while the path continues on the value
        /// inside it. The factory is a parameter because the wrapped type's builder cannot be constructed
        /// from <c>T</c> alone — a generated call site knows the concrete type and hands over its
        /// <c>CreateBuilder</c>.
        /// </remarks>
        public TBuilder GetOrCreateValue<TBuilder>(Func<TBuilder> create)
            where TBuilder : class, IRosettaModelObjectBuilder
        {
            if (_valueBuilder == null)
            {
                _valueBuilder = create();
                _value = default;
            }
            return (TBuilder)_valueBuilder;
        }

        public Builder SetGlobalReference(string? globalReference)
        {
            GlobalReference = globalReference;
            return this;
        }

        public Builder SetExternalReference(string? externalReference)
        {
            ExternalReference = externalReference;
            return this;
        }

        public Builder SetScopedReference(Reference.Builder? scopedReference)
        {
            ScopedReference = scopedReference;
            return this;
        }

        public Builder SetScopedReference(Reference? scopedReference)
        {
            ScopedReference = scopedReference?.ToBuilder();
            return this;
        }

        public Reference.Builder GetOrCreateScopedReference() =>
            ScopedReference ??= Reference.CreateBuilder();

        public override ReferenceWithMeta<T> Build() => new ReferenceWithMeta<T>(this);

        public override Builder Prune()
        {
            if (ScopedReference != null && !ScopedReference.Prune().HasData())
            {
                ScopedReference = null;
            }
            if (_valueBuilder != null)
            {
                _valueBuilder.Prune();
                if (!_valueBuilder.HasData())
                {
                    _valueBuilder = null;
                }
            }
            return this;
        }

        public override bool HasData()
        {
            if (GlobalReference != null || ExternalReference != null)
            {
                return true;
            }
            if (ScopedReference != null && ScopedReference.HasData())
            {
                return true;
            }
            if (_valueBuilder != null)
            {
                return _valueBuilder.HasData();
            }
            return _value != null;
        }
    }
}
