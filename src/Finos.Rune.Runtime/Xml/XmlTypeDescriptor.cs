namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// How a Rune attribute is represented in XML. Mirrors rune-common's
/// <c>AttributeXMLRepresentation</c>.
/// </summary>
public enum XmlPropertyRepresentation
{
    /// <summary>A child element (the default).</summary>
    Element,

    /// <summary>An XML attribute on the enclosing element.</summary>
    Attribute,

    /// <summary>The text content of the enclosing element.</summary>
    Value,

    /// <summary>
    /// A synthetic grouping object with no element of its own: its own attributes are flattened
    /// into the enclosing element's content.
    /// </summary>
    Virtual,
}

/// <summary>
/// Immutable description of one Rune attribute's XML representation.
/// </summary>
public sealed class XmlPropertyDescriptor
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> NoAttributes =
        Array.Empty<KeyValuePair<string, string>>();

    public XmlPropertyDescriptor(
        string propertyName,
        XmlQName xmlName,
        XmlPropertyRepresentation representation = XmlPropertyRepresentation.Element,
        bool isMulti = false,
        XmlQName? elementRef = null,
        IReadOnlyList<KeyValuePair<string, string>>? constantAttributes = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        PropertyName = propertyName;
        XmlName = xmlName;
        Representation = representation;
        IsMulti = isMulti;
        ElementRef = elementRef;
        ConstantAttributes = constantAttributes == null ? NoAttributes : constantAttributes.ToArray();
    }

    /// <summary>The Rune attribute name; also the first segment of a content-model path.</summary>
    public string PropertyName { get; }

    /// <summary>The XML element or attribute name carrying this property.</summary>
    public XmlQName XmlName { get; }

    public XmlPropertyRepresentation Representation { get; }

    /// <summary>True for a Rune list attribute. Lists are never wrapped in XML.</summary>
    public bool IsMulti { get; }

    /// <summary>
    /// The substitution-group head this property refers to, when its element name is decided by
    /// the runtime type of each value rather than by the property itself.
    /// </summary>
    public XmlQName? ElementRef { get; }

    /// <summary>
    /// Constant XML attributes written on this property's element, in declaration order.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> ConstantAttributes { get; }

    public override string ToString() => PropertyName + " -> " + XmlName + " (" + Representation + ")";
}

/// <summary>
/// One member of an XML substitution group: the concrete element name a type is written as, and
/// the group head it substitutes for.
/// </summary>
public sealed class XmlSubstitutionEntry
{
    public XmlSubstitutionEntry(XmlQName group, XmlQName elementName, bool isAbstract = false)
    {
        Group = group;
        ElementName = elementName;
        IsAbstract = isAbstract;
    }

    /// <summary>The substitution-group head, e.g. <c>urn:my.schema/animal</c>.</summary>
    public XmlQName Group { get; }

    /// <summary>The element name this type is written as, e.g. <c>urn:my.schema/goat</c>.</summary>
    public XmlQName ElementName { get; }

    /// <summary>True for a group head that is never instantiated directly.</summary>
    public bool IsAbstract { get; }

    public override string ToString() => ElementName + " substitutes " + Group;
}

/// <summary>
/// Immutable XML description of one Rune type: its element name, its constant attributes, its
/// properties in declaration order, its substitution-group membership, and its XSD content model.
/// </summary>
public sealed class XmlTypeDescriptor
{
    private static readonly IReadOnlyList<KeyValuePair<string, string>> NoAttributes =
        Array.Empty<KeyValuePair<string, string>>();

    private readonly Dictionary<string, XmlPropertyDescriptor> _byName;

    public XmlTypeDescriptor(
        string typeName,
        IReadOnlyList<XmlPropertyDescriptor>? properties = null,
        XmlQName? elementName = null,
        bool isAbstract = false,
        XmlSubstitutionEntry? substitution = null,
        IReadOnlyList<KeyValuePair<string, string>>? constantAttributes = null,
        XmlContentModelNode? contentModel = null)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        TypeName = typeName;
        Properties = properties == null ? Array.Empty<XmlPropertyDescriptor>() : properties.ToArray();
        ElementName = elementName;
        IsAbstract = isAbstract;
        Substitution = substitution;
        ConstantAttributes = constantAttributes == null ? NoAttributes : constantAttributes.ToArray();
        ContentModel = contentModel;
        _byName = new Dictionary<string, XmlPropertyDescriptor>(StringComparer.Ordinal);
        foreach (XmlPropertyDescriptor property in Properties)
        {
            _byName[property.PropertyName] = property;
        }
        ValueProperty = Properties.FirstOrDefault(p => p.Representation == XmlPropertyRepresentation.Value);
    }

    /// <summary>The fully-qualified Rune type name, used as the registry key.</summary>
    public string TypeName { get; }

    public IReadOnlyList<XmlPropertyDescriptor> Properties { get; }

    /// <summary>The element name for a document root or a substitution-group member.</summary>
    public XmlQName? ElementName { get; }

    public bool IsAbstract { get; }

    public XmlSubstitutionEntry? Substitution { get; }

    /// <summary>
    /// Constant attributes written on this type's element — namespace declarations on a root type,
    /// for instance — in declaration order and before any value-carrying attribute.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> ConstantAttributes { get; }

    public XmlContentModelNode? ContentModel { get; }

    /// <summary>The single property represented as element text, when the type has one.</summary>
    public XmlPropertyDescriptor? ValueProperty { get; }

    public XmlPropertyDescriptor? FindProperty(string propertyName) =>
        _byName.TryGetValue(propertyName, out XmlPropertyDescriptor? found) ? found : null;

    public override string ToString() => TypeName;
}
