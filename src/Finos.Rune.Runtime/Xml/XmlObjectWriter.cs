using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// How the writer names an element whose type belongs to an XML substitution group.
/// </summary>
/// <remarks>
/// rune-common's <c>SubstitutingBeanPropertyWriter</c> writes
/// <c>SubstitutionMap.getSubstitutedName(value)</c>, which is the member's <em>local name</em> with
/// no namespace — verified in 12.3.1, the pinned oracle. That is lossy whenever two members of one
/// group share a local name across namespaces (the base and extension <c>llama</c> of rune-common's
/// own test schema): the element reads back as whichever member was registered first. Java has no
/// golden fixture that writes such an element, so this policy chooses what to do about it.
/// </remarks>
public enum XmlSubstitutionNamespacePolicy
{
    /// <summary>
    /// Always write the bare local name — byte-for-byte what Java 12.3.1 does, including its loss.
    /// </summary>
    Never,

    /// <summary>
    /// Write the bare local name when it identifies exactly one member of the group, and the
    /// qualified name when it does not. Byte-identical to Java wherever Java is unambiguous, and
    /// lossless where Java is not. This is the default.
    /// </summary>
    WhenAmbiguous,

    /// <summary>Always write the member's registered namespace.</summary>
    Always,
}

/// <summary>Tuning for a write. The defaults reproduce the Java reference's pretty printer.</summary>
public sealed class XmlWriteOptions
{
    /// <summary>
    /// The default options: two-space indent, <c>\n</c> newlines, no XML declaration.
    /// </summary>
    /// <remarks>
    /// See <see cref="RuneXmlVersionAlignment"/>: <c>\n</c> reproduces rune-common 12.5.1 on every
    /// platform and 12.3.1 — the version the pinned CDM Java oracle was built against — on any
    /// platform whose line separator is already <c>\n</c>.
    /// </remarks>
    public static readonly XmlWriteOptions Default = new XmlWriteOptions();

    public bool Indent { get; init; } = true;

    public string IndentChars { get; init; } = "  ";

    /// <summary>Always <c>\n</c> by default, so output is byte-identical across platforms.</summary>
    public string NewLine { get; init; } = "\n";

    public bool OmitXmlDeclaration { get; init; } = true;

    /// <summary>
    /// Written once, on the root element only, as <c>xsi:schemaLocation</c>. Nested elements never
    /// repeat it, even when they have a content model of their own.
    /// </summary>
    public string? SchemaLocation { get; init; }

    /// <summary>
    /// When true (the default) an empty element is closed as <c>&lt;a/&gt;</c> rather than .NET's
    /// <c>&lt;a /&gt;</c>, matching the Java reference byte for byte. This is a pure text
    /// normalisation and is unambiguous because <c>&gt;</c> is always escaped elsewhere.
    /// </summary>
    public bool CompactEmptyElements { get; init; } = true;

    /// <summary>How a substitution-group member's element name is qualified.</summary>
    public XmlSubstitutionNamespacePolicy SubstitutionNamespace { get; init; } =
        XmlSubstitutionNamespacePolicy.WhenAmbiguous;

    /// <summary>Returns a copy with a different line separator, leaving everything else alone.</summary>
    public XmlWriteOptions WithNewLine(string newLine)
    {
        ArgumentNullException.ThrowIfNull(newLine);
        return new XmlWriteOptions
        {
            Indent = Indent,
            IndentChars = IndentChars,
            NewLine = newLine,
            OmitXmlDeclaration = OmitXmlDeclaration,
            SchemaLocation = SchemaLocation,
            CompactEmptyElements = CompactEmptyElements,
            SubstitutionNamespace = SubstitutionNamespace,
        };
    }
}

/// <summary>Per-write state: the codec registry and the root-only schema location.</summary>
public sealed class XmlWriteContext
{
    public XmlWriteContext(XmlCodecRegistry registry, XmlWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        Options = options ?? XmlWriteOptions.Default;
    }

    public XmlCodecRegistry Registry { get; }

    public XmlWriteOptions Options { get; }

    /// <summary>The <c>xsi:schemaLocation</c> value, written on the root element only.</summary>
    public string? SchemaLocation => Options.SchemaLocation;
}

/// <summary>
/// The codec-facing writer. Attributes are always written before child elements, absent values are
/// omitted, substituted values take their element name from the registry, and a type with a
/// content model has its present properties reordered to follow it — falling back to descriptor
/// order rather than throwing when no valid ordering exists.
/// </summary>
public sealed class XmlObjectWriter
{
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";
    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    private static readonly ConditionalWeakTable<XmlContentModelNode, XmlContentModelOrderer> Orderers = new();

    private readonly XmlWriter _writer;
    private readonly Stack<string> _defaultNamespaces = new();

    internal XmlObjectWriter(XmlWriter writer, XmlWriteContext context)
    {
        _writer = writer;
        Context = context;
        _defaultNamespaces.Push(string.Empty);
    }

    public XmlWriteContext Context { get; }

    /// <summary>Writes an XML attribute; a null value writes nothing.</summary>
    public void WriteAttribute(string localName, string? value)
    {
        ArgumentNullException.ThrowIfNull(localName);
        if (value == null)
        {
            return;
        }
        _writer.WriteAttributeString(localName, value);
    }

    /// <summary>Writes a namespace-qualified XML attribute; a null value writes nothing.</summary>
    public void WriteAttribute(XmlQName name, string? value)
    {
        if (value == null)
        {
            return;
        }
        if (name.Namespace == null)
        {
            _writer.WriteAttributeString(name.LocalName, value);
            return;
        }
        _writer.WriteAttributeString(_writer.LookupPrefix(name.Namespace), name.LocalName, name.Namespace, value);
    }

    /// <summary>Writes a leaf element with text content; a null text writes nothing.</summary>
    public void WriteTextElement(XmlQName name, string? text)
    {
        if (text == null)
        {
            return;
        }
        StartElement(name);
        _writer.WriteString(text);
        _writer.WriteEndElement();
    }

    /// <summary>
    /// Writes a leaf element carrying both constant attributes and text, in that order.
    /// </summary>
    public void WriteTextElement(
        XmlQName name, string? text, IReadOnlyList<KeyValuePair<string, string>> constantAttributes)
    {
        ArgumentNullException.ThrowIfNull(constantAttributes);
        if (text == null)
        {
            return;
        }
        StartElement(name);
        foreach (KeyValuePair<string, string> attribute in constantAttributes)
        {
            _writer.WriteAttributeString(attribute.Key, attribute.Value);
        }
        _writer.WriteString(text);
        _writer.WriteEndElement();
    }

    /// <summary>Writes a nested model object under <paramref name="name"/>; null writes nothing.</summary>
    public void WriteObjectElement(XmlQName name, RosettaModelObject? value, IXmlTypeCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (value == null)
        {
            return;
        }
        WriteElement(name, value, codec, isRoot: false);
    }

    /// <summary>
    /// Writes a value whose element name comes from its substitution-group membership rather than
    /// from the property. Each item of a list resolves its own name, so a mixed list writes mixed
    /// element names.
    /// </summary>
    public void WriteSubstitutedElement(RosettaModelObject? value)
    {
        if (value == null)
        {
            return;
        }
        IXmlTypeCodec codec = Context.Registry.FindByValueType(value.GetType())
            ?? throw new InvalidOperationException(
                "No XML codec is registered for substituted value type " + value.GetType().Name + ".");
        XmlQName name = Context.Registry.SubstitutedElementName(value, Context.Options.SubstitutionNamespace)
            ?? throw new InvalidOperationException(
                "No substitution element name is registered for " + value.GetType().Name + ".");
        WriteElement(name, value, codec, isRoot: false);
    }

    /// <summary>
    /// Writes an object carried by an <c>elementRef</c>. A substitution member uses its registered
    /// concrete name; a directly-referenced global element keeps the reference's name.
    /// </summary>
    public void WriteElementReference(XmlQName reference, RosettaModelObject? value, IXmlTypeCodec fallbackCodec)
    {
        ArgumentNullException.ThrowIfNull(fallbackCodec);
        if (value == null)
        {
            return;
        }
        IXmlTypeCodec codec = Context.Registry.FindByValueType(value.GetType()) ?? fallbackCodec;
        XmlQName name = codec.Descriptor.Substitution == null
            ? reference
            : Context.Registry.SubstitutedElementName(value, Context.Options.SubstitutionNamespace)
                ?? throw new InvalidOperationException(
                    "No substitution element name is registered for " + value.GetType().Name + ".");
        WriteElement(name, value, codec, isRoot: false);
    }

    /// <summary>
    /// Writes a scalar carried by a direct <c>elementRef</c>. Scalar substitution groups do not occur
    /// in rune-fpml's pinned configuration; a direct reference is therefore its declared element.
    /// </summary>
    public void WriteTextElementReference(XmlQName reference, string? text) =>
        WriteTextElement(reference, text);

    /// <summary>
    /// Flattens a VIRTUAL group into the element currently open: its own properties are written
    /// inline, with no element of its own.
    /// </summary>
    public void WriteVirtual(RosettaModelObject? value, IXmlTypeCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (value == null)
        {
            return;
        }
        WriteChildren(value, codec);
    }

    /// <summary>
    /// Writes the attributes a VIRTUAL group contributes to the element currently open. Codecs call
    /// this from <see cref="IXmlTypeCodec.WriteAttributes"/> so that flattened attributes still
    /// precede all child content.
    /// </summary>
    public void WriteVirtualAttributes(RosettaModelObject? value, IXmlTypeCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (value == null)
        {
            return;
        }
        WriteConstantAttributes(codec.Descriptor);
        codec.WriteAttributes(value, this, Context);
    }

    internal void WriteRoot(XmlQName name, RosettaModelObject value, IXmlTypeCodec codec) =>
        WriteElement(name, value, codec, isRoot: true);

    private void WriteElement(XmlQName name, RosettaModelObject value, IXmlTypeCodec codec, bool isRoot)
    {
        XmlTypeDescriptor descriptor = codec.Descriptor;
        string parentDefault = _defaultNamespaces.Peek();
        string? declaredDefault = FindConstantAttribute(descriptor, "xmlns");
        if (declaredDefault == null && name.Namespace != null
            && !string.Equals(name.Namespace, parentDefault, StringComparison.Ordinal))
        {
            declaredDefault = name.Namespace;
        }
        if (declaredDefault != null)
        {
            _defaultNamespaces.Push(declaredDefault);
        }

        StartElement(name);
        if (declaredDefault != null && !string.Equals(declaredDefault, parentDefault, StringComparison.Ordinal))
        {
            // Written first, or XmlWriter would append its own declaration after these attributes.
            _writer.WriteAttributeString(null, "xmlns", XmlnsNamespace, declaredDefault);
        }
        WriteConstantAttributes(descriptor);
        if (isRoot && Context.SchemaLocation != null)
        {
            _writer.WriteAttributeString("xsi", "schemaLocation", XsiNamespace, Context.SchemaLocation);
        }
        codec.WriteAttributes(value, this, Context);

        string? text = codec.TextValue(value, Context);
        if (text != null)
        {
            _writer.WriteString(text);
        }
        WriteChildren(value, codec);
        _writer.WriteEndElement();

        if (declaredDefault != null)
        {
            _defaultNamespaces.Pop();
        }
    }

    private void WriteChildren(RosettaModelObject value, IXmlTypeCodec codec)
    {
        IReadOnlyList<string> present = codec.PresentProperties(value);
        foreach (string property in OrderProperties(codec.Descriptor, present))
        {
            codec.WriteProperty(value, property, this, Context);
        }
    }

    private void StartElement(XmlQName name)
    {
        string ns = name.Namespace ?? _defaultNamespaces.Peek();
        _writer.WriteStartElement(null, name.LocalName, ns.Length == 0 ? null : ns);
    }

    private void WriteConstantAttributes(XmlTypeDescriptor descriptor)
    {
        foreach (KeyValuePair<string, string> attribute in descriptor.ConstantAttributes)
        {
            if (string.Equals(attribute.Key, "xmlns", StringComparison.Ordinal))
            {
                continue;
            }
            if (attribute.Key.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                _writer.WriteAttributeString("xmlns", attribute.Key[6..], XmlnsNamespace, attribute.Value);
                continue;
            }
            _writer.WriteAttributeString(attribute.Key, attribute.Value);
        }
    }

    private static string? FindConstantAttribute(XmlTypeDescriptor descriptor, string name)
    {
        foreach (KeyValuePair<string, string> attribute in descriptor.ConstantAttributes)
        {
            if (string.Equals(attribute.Key, name, StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }
        return null;
    }

    /// <summary>
    /// Reorders the content-model properties of <paramref name="present"/> in place, leaving every
    /// other property where it was. A model that cannot consume the present set leaves the order
    /// untouched — the writer never throws over content-model ordering.
    /// </summary>
    private static IReadOnlyList<string> OrderProperties(XmlTypeDescriptor descriptor, IReadOnlyList<string> present)
    {
        if (descriptor.ContentModel == null || present.Count < 2)
        {
            return present;
        }
        XmlContentModelOrderer orderer = Orderers.GetValue(
            descriptor.ContentModel, model => new XmlContentModelOrderer(model));
        var participating = new HashSet<string>(orderer.ContentModelProperties, StringComparer.Ordinal);
        var subset = new List<string>();
        foreach (string property in present)
        {
            if (participating.Contains(property))
            {
                subset.Add(property);
            }
        }
        if (subset.Count < 2)
        {
            return present;
        }
        IReadOnlyList<string>? ordered = orderer.Order(subset);
        if (ordered == null)
        {
            return present;
        }
        var result = new List<string>(present.Count);
        int next = 0;
        foreach (string property in present)
        {
            result.Add(participating.Contains(property) ? ordered[next++] : property);
        }
        return result;
    }
}

/// <summary>Entry point for writing a Rune model object as an XML document.</summary>
public static class XmlDocumentWriter
{
    /// <summary>Writes <paramref name="value"/> using the codec registered for its type.</summary>
    public static string WriteToString(
        RosettaModelObject value, XmlCodecRegistry registry, XmlWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(registry);
        IXmlTypeCodec codec = registry.FindByValueType(value.GetType())
            ?? throw new InvalidOperationException(
                "No XML codec is registered for " + value.GetType().Name + ".");
        return WriteToString(value, codec, registry, options);
    }

    /// <summary>Writes <paramref name="value"/> with an explicitly chosen root codec.</summary>
    public static string WriteToString(
        RosettaModelObject value, IXmlTypeCodec codec, XmlCodecRegistry registry, XmlWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(registry);
        XmlWriteOptions effective = options ?? XmlWriteOptions.Default;
        var context = new XmlWriteContext(registry, effective);
        XmlQName rootName = codec.Descriptor.ElementName
            ?? codec.Descriptor.Substitution?.ElementName
            ?? new XmlQName(LastSegment(codec.Descriptor.TypeName));

        var text = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = effective.Indent,
            IndentChars = effective.IndentChars,
            NewLineChars = effective.NewLine,
            OmitXmlDeclaration = effective.OmitXmlDeclaration,
            NewLineHandling = NewLineHandling.Replace,
        };
        using (XmlWriter writer = XmlWriter.Create(text, settings))
        {
            new XmlObjectWriter(writer, context).WriteRoot(rootName, value, codec);
        }
        string result = text.ToString();
        return effective.CompactEmptyElements
            ? result.Replace(" />", "/>", StringComparison.Ordinal)
            : result;
    }

    private static string LastSegment(string typeName)
    {
        int dot = typeName.LastIndexOf('.');
        return dot < 0 ? typeName : typeName[(dot + 1)..];
    }
}
