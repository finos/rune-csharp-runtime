using System.Xml;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Entry point for reading a Rune model object out of an XML document. The root element is
/// dispatched through the <see cref="XmlCodecRegistry"/>, and the whole tree is pruned once at the
/// end so that empty elements collapse away exactly as they do after a JSON read.
/// </summary>
public static class XmlDocumentReader
{
    private static readonly XmlReaderSettings ReaderSettings = new XmlReaderSettings
    {
        IgnoreWhitespace = true,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        DtdProcessing = DtdProcessing.Prohibit,
        CloseInput = false,
    };

    /// <summary>Reads a document whose root element the registry can resolve.</summary>
    public static RosettaModelObject Read(string xml, XmlCodecRegistry registry, out XmlReadContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        context = new XmlReadContext(registry);
        return Read(xml, context);
    }

    /// <summary>Reads a document into the supplied context, so its counters can be inspected.</summary>
    public static RosettaModelObject Read(string xml, XmlReadContext context)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(context);
        using XmlReader reader = CreateReader(xml);
        MoveToRoot(reader);
        var rootName = new XmlQName(reader.NamespaceURI, reader.LocalName);
        IXmlTypeCodec codec = context.Registry.FindRoot(rootName)
            ?? throw new XmlException("No XML codec is registered for root element '" + rootName + "'.");
        return Finish(XmlElementReader.ReadElement(reader, codec, context), context.Options.PreserveEmptyElements);
    }

    /// <summary>Reads a document with an explicitly chosen root codec.</summary>
    public static RosettaModelObject ReadAs(string xml, IXmlTypeCodec codec, XmlReadContext context)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(context);
        using XmlReader reader = CreateReader(xml);
        MoveToRoot(reader);
        return Finish(XmlElementReader.ReadElement(reader, codec, context), context.Options.PreserveEmptyElements);
    }

    /// <summary>
    /// Reads a document with an explicitly chosen root codec, typed, creating the read context.
    /// </summary>
    /// <remarks>
    /// The registry is required rather than synthesised from <paramref name="codec"/>: a
    /// single-codec registry cannot resolve a substitution group, so any substituted child would be
    /// silently skipped and counted as unknown instead of read.
    /// </remarks>
    public static TValue ReadAs<TValue>(
        string xml, IXmlTypeCodec codec, XmlCodecRegistry registry, out XmlReadContext context)
        where TValue : RosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(registry);
        context = new XmlReadContext(registry);
        return (TValue)ReadAs(xml, codec, context);
    }

    /// <summary>Reads a document with an explicitly chosen root codec, typed, into a context.</summary>
    public static TValue ReadAs<TValue>(string xml, IXmlTypeCodec codec, XmlReadContext context)
        where TValue : RosettaModelObject =>
        (TValue)ReadAs(xml, codec, context);

    /// <summary>Reads a document and casts the root to the expected generated type.</summary>
    public static TValue Read<TValue>(string xml, XmlCodecRegistry registry, out XmlReadContext context)
        where TValue : RosettaModelObject =>
        (TValue)Read(xml, registry, out context);

    private static RosettaModelObject Finish(RosettaModelObjectBuilder builder, bool preserveEmptyElements) =>
        (preserveEmptyElements ? builder : builder.Prune()).Build();

    private static XmlReader CreateReader(string xml) =>
        XmlReader.Create(new StringReader(xml), ReaderSettings);

    private static void MoveToRoot(XmlReader reader)
    {
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                return;
            }
        }
        throw new XmlException("The XML document contains no root element.");
    }
}
