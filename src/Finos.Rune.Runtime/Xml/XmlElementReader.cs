using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// The streaming element reader: walks an <see cref="XmlReader"/> and hands each attribute, text
/// run and child element to the owning codec. Only a type that declares an XSD content model
/// buffers its children, and then only to run the matcher over their names.
/// </summary>
internal static class XmlElementReader
{
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";
    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Reads the element the reader is positioned on into a fresh builder and advances past it.
    /// </summary>
    internal static RosettaModelObjectBuilder ReadElement(XmlReader reader, IXmlTypeCodec codec, XmlReadContext context)
    {
        if (reader.NodeType != XmlNodeType.Element)
        {
            throw new XmlException("Expected an XML element while reading " + codec.Descriptor.TypeName
                + " but found " + reader.NodeType + ".");
        }

        RosettaModelObjectBuilder builder = codec.CreateBuilder();
        string owner = codec.Descriptor.TypeName;
        bool isEmpty = reader.IsEmptyElement;

        bool hasContentAttributes = ReadAttributes(reader, codec, builder, context, owner);

        if (isEmpty)
        {
            if (!hasContentAttributes && codec.Descriptor.ValueProperty != null)
            {
                codec.TryReadText(builder, string.Empty, context);
            }
            reader.Read();
            return builder;
        }

        List<XElement>? buffered = codec.Descriptor.ContentModel != null ? new List<XElement>() : null;
        var text = new StringBuilder();

        reader.Read();
        while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (buffered != null)
                    {
                        buffered.Add((XElement)XNode.ReadFrom(reader));
                    }
                    else
                    {
                        DispatchElement(reader, codec, builder, context, owner);
                    }
                    break;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                    text.Append(reader.Value);
                    reader.Read();
                    break;
                default:
                    reader.Read();
                    break;
            }
        }
        if (!reader.EOF)
        {
            reader.Read();
        }

        if (buffered != null)
        {
            ApplyContentModel(codec, builder, buffered, context, owner);
        }
        if (codec.Descriptor.ValueProperty != null && (text.Length > 0 || !hasContentAttributes))
        {
            codec.TryReadText(builder, text.ToString(), context);
        }
        return builder;
    }

    private static bool ReadAttributes(
        XmlReader reader, IXmlTypeCodec codec, RosettaModelObjectBuilder builder, XmlReadContext context, string owner)
    {
        if (!reader.HasAttributes)
        {
            return false;
        }
        bool hasContentAttributes = false;
        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (IsInfrastructureAttribute(reader.NamespaceURI, reader.Prefix, reader.LocalName))
            {
                continue;
            }
            hasContentAttributes = true;
            var name = new XmlQName(reader.NamespaceURI, reader.LocalName);
            if (!codec.TryReadAttribute(builder, name, reader.Value, context))
            {
                context.NoteUnknownAttribute(owner, name);
            }
        }
        reader.MoveToElement();
        return hasContentAttributes;
    }

    private static bool IsInfrastructureAttribute(string ns, string prefix, string localName) =>
        string.Equals(ns, XmlnsNamespace, StringComparison.Ordinal)
        || string.Equals(prefix, "xmlns", StringComparison.Ordinal)
        || string.Equals(localName, "xmlns", StringComparison.Ordinal)
        || string.Equals(ns, XsiNamespace, StringComparison.Ordinal);

    private static void DispatchElement(
        XmlReader reader, IXmlTypeCodec codec, RosettaModelObjectBuilder builder, XmlReadContext context, string owner)
    {
        var name = new XmlQName(reader.NamespaceURI, reader.LocalName);
        using (XmlReader subtree = reader.ReadSubtree())
        {
            subtree.Read();
            if (!codec.TryReadElement(builder, name, subtree, context))
            {
                context.NoteUnknownElement(owner, name);
            }
            while (subtree.Read())
            {
                // Drain whatever the codec left, so the parent reader lands on the end element.
            }
        }
        reader.Read();
    }

    /// <summary>
    /// Routes buffered children through the content model and dispatches each one, walking VIRTUAL
    /// path segments through <see cref="IXmlTypeCodec.CreateVirtualSlot"/>. One slot is created per
    /// distinct path prefix and content-model occurrence, so elements matched inside the same
    /// repeated iteration land in the same object.
    /// </summary>
    private static void ApplyContentModel(
        IXmlTypeCodec codec,
        RosettaModelObjectBuilder builder,
        List<XElement> children,
        XmlReadContext context,
        string owner)
    {
        XmlContentModelNode model = codec.Descriptor.ContentModel!;
        XmlContentModelIndex index = XmlContentModelIndex.For(model);

        var inputs = new List<XmlRoutingInput>();
        for (int i = 0; i < children.Count; i++)
        {
            string localName = children[i].Name.LocalName;
            if (!index.HasAny && !index.RoutedNames.Contains(localName))
            {
                continue;
            }
            string ns = children[i].Name.NamespaceName;
            inputs.Add(new XmlRoutingInput(
                i, localName, ns.Length == 0 ? XmlNamespaceState.Absent() : XmlNamespaceState.Present(ns)));
        }

        XmlRoutingResult routing = inputs.Count == 0
            ? XmlRoutingResult.Success(Array.Empty<XmlRoutingAssignment>())
            : XmlContentModelRouter.Route(model, index, inputs, context, owner);

        var routes = new Dictionary<int, XmlRoutingAssignment>();
        foreach (XmlRoutingAssignment assignment in routing.Assignments)
        {
            routes[inputs[assignment.InputIndex].FieldIndex] = assignment;
        }

        var slots = new Dictionary<SlotKey, SlotTarget>();
        for (int i = 0; i < children.Count; i++)
        {
            XElement child = children[i];
            var name = new XmlQName(child.Name.NamespaceName, child.Name.LocalName);
            IXmlTypeCodec targetCodec = codec;
            RosettaModelObjectBuilder targetBuilder = builder;

            if (routes.TryGetValue(i, out XmlRoutingAssignment? assignment) && assignment.Path.Count > 1)
            {
                SlotTarget? resolved = ResolveSlot(codec, builder, assignment, slots, context);
                if (resolved == null)
                {
                    context.NoteUnknownElement(owner, name);
                    continue;
                }
                targetCodec = resolved.Codec;
                targetBuilder = resolved.Builder;
            }

            using XmlReader childReader = child.CreateReader();
            childReader.Read();
            if (!targetCodec.TryReadElement(targetBuilder, name, childReader, context))
            {
                context.NoteUnknownElement(targetCodec.Descriptor.TypeName, name);
            }
        }
    }

    private static SlotTarget? ResolveSlot(
        IXmlTypeCodec codec,
        RosettaModelObjectBuilder builder,
        XmlRoutingAssignment assignment,
        Dictionary<SlotKey, SlotTarget> slots,
        XmlReadContext context)
    {
        IXmlTypeCodec currentCodec = codec;
        RosettaModelObjectBuilder currentBuilder = builder;

        for (int segment = 0; segment < assignment.Path.Count - 1; segment++)
        {
            string property = assignment.Path[segment];
            var key = new SlotKey(string.Join('/', assignment.Path.Take(segment + 1)), assignment.Occurrence);
            if (!slots.TryGetValue(key, out SlotTarget? slot))
            {
                RosettaModelObjectBuilder? created = currentCodec.CreateVirtualSlot(currentBuilder, property, context);
                IXmlTypeCodec? childCodec = currentCodec.PropertyCodec(property);
                if (created == null || childCodec == null)
                {
                    return null;
                }
                slot = new SlotTarget(childCodec, created);
                slots[key] = slot;
            }
            currentCodec = slot.Codec;
            currentBuilder = slot.Builder;
        }
        return new SlotTarget(currentCodec, currentBuilder);
    }

    /// <summary>
    /// Identifies one VIRTUAL slot within a parent read: a path prefix plus the content-model
    /// occurrence that produced it.
    /// </summary>
    /// <remarks>
    /// The occurrence is held as an <see cref="XmlOccurrenceKey"/> and compared by its own equality,
    /// which is frame-wise over <em>node identity</em>. Rendering it to a string instead would fuse
    /// sibling groups of the same kind — two adjacent SEQUENCE nodes both print as
    /// <c>SEQUENCE#0</c> — and silently route their leaves into one shared object.
    /// </remarks>
    private readonly struct SlotKey : IEquatable<SlotKey>
    {
        private readonly string _prefix;
        private readonly XmlOccurrenceKey _occurrence;

        internal SlotKey(string prefix, XmlOccurrenceKey occurrence)
        {
            _prefix = prefix;
            _occurrence = occurrence;
        }

        public bool Equals(SlotKey other) =>
            string.Equals(_prefix, other._prefix, StringComparison.Ordinal) && _occurrence.Equals(other._occurrence);

        public override bool Equals(object? obj) => obj is SlotKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_prefix, _occurrence);

        public override string ToString() => _prefix + "|" + _occurrence;
    }

    private sealed class SlotTarget
    {
        internal SlotTarget(IXmlTypeCodec codec, RosettaModelObjectBuilder builder)
        {
            Codec = codec;
            Builder = builder;
        }

        internal IXmlTypeCodec Codec { get; }

        internal RosettaModelObjectBuilder Builder { get; }
    }
}
