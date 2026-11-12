using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Meta;

/// <summary>
/// Value-with-metadata wrapper for attributes annotated [metadata scheme|id|location].
/// Where the Java generator emits one wrapper type per value type (FieldWithMetaString,
/// ...), C# generics are reified so a single generic class serves all value types.
/// Generated code instantiates T as the optional form of the value type
/// (e.g. FieldWithMeta&lt;string&gt;, FieldWithMeta&lt;DateOnly?&gt;) so absence is
/// always representable.
/// On the wire, basic/enum values serialize under "@data" and Meta is unwrapped;
/// model-object values are additionally flattened into the wrapper object.
/// </summary>
public sealed class FieldWithMeta<T> : RosettaModelObject, IFieldWithMeta<T>, IFieldWithMetaView
{
    private FieldWithMeta(Builder builder)
    {
        Value = builder.Value;
        Meta = builder.Meta?.Build();
    }

    object? IFieldWithMetaView.ValueObject => Value;

    MetaFields? IFieldWithMetaView.MetaObject => Meta;

    Type IFieldWithMetaView.ValueType => Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

    [RuneAttribute(WireNames.Data)]
    [RunePathName("value")]
    public T? Value { get; }

    [RuneAttribute(WireNames.Meta)]
    [RuneMetaType]
    public MetaFields? Meta { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetValue(Value);
        if (Meta != null)
        {
            builder.SetMeta(Meta.ToBuilder());
        }
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        private T? _value;
        private IRosettaModelObjectBuilder? _valueBuilder;

        public T? Value => _valueBuilder != null ? (T)_valueBuilder.Build() : _value;

        public MetaFields.Builder? Meta { get; private set; }

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

        public Builder SetMeta(MetaFields.Builder? meta)
        {
            Meta = meta;
            return this;
        }

        public Builder SetMeta(MetaFields? meta)
        {
            Meta = meta?.ToBuilder();
            return this;
        }

        public MetaFields.Builder GetOrCreateMeta() => Meta ??= MetaFields.CreateBuilder();

        /// <summary>The wrapped value's builder, created if absent.</summary>
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

        public override FieldWithMeta<T> Build() => new FieldWithMeta<T>(this);

        public override Builder Prune()
        {
            if (Meta != null && !Meta.Prune().HasData())
            {
                Meta = null;
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

        /// <summary>Presence of the (unwrapped) value decides; Meta is ignored, matching
        /// the Java generated hasData() which skips the "meta" attribute.</summary>
        public override bool HasData()
        {
            if (_valueBuilder != null)
            {
                return _valueBuilder.HasData();
            }
            return _value != null;
        }
    }
}
