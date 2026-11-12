using System.Xml;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// The contract a concrete, generated XML codec implements for one Rune type. Every member is
/// dispatched by the codec itself against its own builder — there is no reflection, no property
/// lookup table and no runtime configuration file behind any of it.
/// </summary>
/// <remarks>
/// Implementations normally derive from <see cref="XmlTypeCodec{TValue, TBuilder}"/>, which does
/// the casting and leaves typed methods to override.
/// </remarks>
public interface IXmlTypeCodec
{
    /// <summary>The immutable XML description of the type this codec handles.</summary>
    XmlTypeDescriptor Descriptor { get; }

    /// <summary>The generated immutable model type.</summary>
    Type ValueType { get; }

    /// <summary>Creates an empty builder for this type.</summary>
    RosettaModelObjectBuilder CreateBuilder();

    /// <summary>
    /// Applies an XML attribute. Returns false when the codec does not know the name, which the
    /// reader records as an unknown and otherwise ignores.
    /// </summary>
    bool TryReadAttribute(RosettaModelObjectBuilder builder, XmlQName name, string text, XmlReadContext context);

    /// <summary>
    /// Applies the element's text content to the type's VALUE property. Returns false when the type
    /// has none.
    /// </summary>
    bool TryReadText(RosettaModelObjectBuilder builder, string text, XmlReadContext context);

    /// <summary>
    /// Applies one child element. The reader is positioned on that element and the codec must read
    /// it through <see cref="XmlReadContext"/>'s helpers. Returns false when the codec does not know
    /// the name.
    /// </summary>
    bool TryReadElement(RosettaModelObjectBuilder builder, XmlQName name, XmlReader reader, XmlReadContext context);

    /// <summary>
    /// Creates the builder that content-model routing should place a nested path segment into: a
    /// fresh list item for a multi-cardinality VIRTUAL property, or the get-or-created child for a
    /// single one. The reader calls this once per distinct content-model occurrence, so a codec
    /// never has to reason about occurrence keys itself.
    /// </summary>
    RosettaModelObjectBuilder? CreateVirtualSlot(
        RosettaModelObjectBuilder builder, string propertyName, XmlReadContext context);

    /// <summary>The codec of a property's own type, used to walk a multi-segment routed path.</summary>
    IXmlTypeCodec? PropertyCodec(string propertyName);

    /// <summary>
    /// Writes this value's XML attributes, including any flattened from VIRTUAL children. Called
    /// before any child content, because XML attributes must precede it.
    /// </summary>
    void WriteAttributes(RosettaModelObject value, XmlObjectWriter writer, XmlWriteContext context);

    /// <summary>The element's text content when the type has a VALUE property, else null.</summary>
    string? TextValue(RosettaModelObject value, XmlWriteContext context);

    /// <summary>
    /// The names of the element-producing properties that actually carry data, in descriptor order.
    /// This is the input to content-model ordering.
    /// </summary>
    IReadOnlyList<string> PresentProperties(RosettaModelObject value);

    /// <summary>Writes one property's child content into the currently open element.</summary>
    void WriteProperty(
        RosettaModelObject value, string propertyName, XmlObjectWriter writer, XmlWriteContext context);
}

/// <summary>
/// Typed base class for a generated XML codec. Subclasses override only the typed members they
/// need; everything else is a no-op that reports "not mine" so the reader stays lenient.
/// </summary>
public abstract class XmlTypeCodec<TValue, TBuilder> : IXmlTypeCodec
    where TValue : RosettaModelObject
    where TBuilder : RosettaModelObjectBuilder
{
    protected XmlTypeCodec(XmlTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    public XmlTypeDescriptor Descriptor { get; }

    public Type ValueType => typeof(TValue);

    public abstract TBuilder CreateTypedBuilder();

    protected virtual bool TryReadAttribute(TBuilder builder, XmlQName name, string text, XmlReadContext context) => false;

    protected virtual bool TryReadText(TBuilder builder, string text, XmlReadContext context) => false;

    protected virtual bool TryReadElement(TBuilder builder, XmlQName name, XmlReader reader, XmlReadContext context) => false;

    protected virtual RosettaModelObjectBuilder? CreateVirtualSlot(
        TBuilder builder, string propertyName, XmlReadContext context) => null;

    public virtual IXmlTypeCodec? PropertyCodec(string propertyName) => null;

    protected virtual void WriteAttributes(TValue value, XmlObjectWriter writer, XmlWriteContext context)
    {
    }

    protected virtual string? TextValue(TValue value, XmlWriteContext context) => null;

    protected virtual IReadOnlyList<string> PresentProperties(TValue value) => Array.Empty<string>();

    protected virtual void WriteProperty(
        TValue value, string propertyName, XmlObjectWriter writer, XmlWriteContext context)
    {
    }

    RosettaModelObjectBuilder IXmlTypeCodec.CreateBuilder() => CreateTypedBuilder();

    bool IXmlTypeCodec.TryReadAttribute(
        RosettaModelObjectBuilder builder, XmlQName name, string text, XmlReadContext context) =>
        TryReadAttribute(Cast(builder), name, text, context);

    bool IXmlTypeCodec.TryReadText(RosettaModelObjectBuilder builder, string text, XmlReadContext context) =>
        TryReadText(Cast(builder), text, context);

    bool IXmlTypeCodec.TryReadElement(
        RosettaModelObjectBuilder builder, XmlQName name, XmlReader reader, XmlReadContext context) =>
        TryReadElement(Cast(builder), name, reader, context);

    RosettaModelObjectBuilder? IXmlTypeCodec.CreateVirtualSlot(
        RosettaModelObjectBuilder builder, string propertyName, XmlReadContext context) =>
        CreateVirtualSlot(Cast(builder), propertyName, context);

    void IXmlTypeCodec.WriteAttributes(RosettaModelObject value, XmlObjectWriter writer, XmlWriteContext context) =>
        WriteAttributes(Cast(value), writer, context);

    string? IXmlTypeCodec.TextValue(RosettaModelObject value, XmlWriteContext context) =>
        TextValue(Cast(value), context);

    IReadOnlyList<string> IXmlTypeCodec.PresentProperties(RosettaModelObject value) => PresentProperties(Cast(value));

    void IXmlTypeCodec.WriteProperty(
        RosettaModelObject value, string propertyName, XmlObjectWriter writer, XmlWriteContext context) =>
        WriteProperty(Cast(value), propertyName, writer, context);

    private static TBuilder Cast(RosettaModelObjectBuilder builder) => builder as TBuilder
        ?? throw new ArgumentException(
            "Expected " + typeof(TBuilder).FullName + " but got " + builder.GetType().FullName + ".", nameof(builder));

    private static TValue Cast(RosettaModelObject value) => value as TValue
        ?? throw new ArgumentException(
            "Expected " + typeof(TValue).FullName + " but got " + value.GetType().FullName + ".", nameof(value));
}
