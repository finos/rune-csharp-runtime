using System.Xml;

namespace Finos.Rune.Runtime.Xml;

/// <summary>Tuning for a read. Leniency is on by default, matching the Java reference.</summary>
public sealed class XmlReadOptions
{
    /// <summary>The default options: lenient, recording every diagnostic.</summary>
    public static readonly XmlReadOptions Default = new XmlReadOptions();

    /// <summary>
    /// When true (the default) an element or attribute the codec does not know is skipped and
    /// counted. When false the reader throws on the first one.
    /// </summary>
    public bool Lenient { get; init; } = true;

    /// <summary>Upper bound on retained diagnostic messages; counters are always exact.</summary>
    public int MaxDiagnostics { get; init; } = 100;

    /// <summary>
    /// Keeps explicitly present empty child objects instead of pruning them at the document root.
    /// The default remains the Rune object-pruning behavior; the Java XML oracle preserves empty
    /// elements, so cross-language canonical reads enable this option.
    /// </summary>
    public bool PreserveEmptyElements { get; init; }
}

/// <summary>
/// Per-read state: the codec registry, the leniency policy, the observable counters of everything
/// the reader could not place, and the helpers a codec calls to consume the current element.
/// </summary>
public sealed class XmlReadContext
{
    private readonly List<string> _diagnostics = new();

    public XmlReadContext(XmlCodecRegistry registry, XmlReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        Options = options ?? XmlReadOptions.Default;
    }

    public XmlCodecRegistry Registry { get; }

    public XmlReadOptions Options { get; }

    /// <summary>Child elements no codec claimed, and which were therefore skipped.</summary>
    public int UnknownElementCount { get; private set; }

    /// <summary>XML attributes no codec claimed.</summary>
    public int UnknownAttributeCount { get; private set; }

    /// <summary>Objects whose children had to be reordered before the content model matched.</summary>
    public int ReorderedObjectCount { get; private set; }

    /// <summary>Elements dropped because the content model could not route them uniquely.</summary>
    public int UnroutedElementCount { get; private set; }

    /// <summary>Objects for which content-model routing was abandoned entirely.</summary>
    public int AbandonedRoutingCount { get; private set; }

    /// <summary>Configured zoned-date/time values read by generated codecs.</summary>
    public int TemporalValueCount { get; private set; }

    /// <summary>dateTime lexicals (not date-only values) that carried no timezone.</summary>
    public int ZoneLessTemporalValueCount { get; private set; }

    /// <summary>Human-readable notes about everything counted above, capped by the options.</summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    /// <summary>True when the document read cleanly, with nothing skipped or recovered.</summary>
    public bool IsClean => UnknownElementCount == 0 && UnknownAttributeCount == 0
        && ReorderedObjectCount == 0 && UnroutedElementCount == 0 && AbandonedRoutingCount == 0;

    internal void NoteUnknownElement(string owner, XmlQName name)
    {
        if (!Options.Lenient)
        {
            throw new XmlException("Unexpected XML element '" + name + "' while reading " + owner + ".");
        }
        UnknownElementCount++;
        Note("Skipped unknown element '" + name + "' in " + owner + ".");
    }

    internal void NoteUnknownAttribute(string owner, XmlQName name)
    {
        if (!Options.Lenient)
        {
            throw new XmlException("Unexpected XML attribute '" + name + "' while reading " + owner + ".");
        }
        UnknownAttributeCount++;
        Note("Skipped unknown attribute '" + name + "' in " + owner + ".");
    }

    internal void NoteReordered(string owner, string sequence)
    {
        ReorderedObjectCount++;
        Note("XML content for " + owner + " was not in schema order; reordered to deserialize. "
            + "XML child sequence: " + sequence);
    }

    internal void NoteUnrouted(string owner, int count, string sequence)
    {
        UnroutedElementCount += count;
        Note("Skipped " + count + " XML element(s) of " + owner
            + " that could not be routed by the content model: " + sequence);
    }

    internal void NoteAbandonedRouting(string owner, string sequence)
    {
        AbandonedRoutingCount++;
        Note("Cannot route XML content for " + owner + " (child sequence: " + sequence
            + "); skipping content-model routing.");
    }

    /// <summary>Records the lexical timezone classification of one generated temporal property.</summary>
    public void RecordTemporalZone(XmlZoneMetadata zone, bool isDateTimeLexical = true)
    {
        TemporalValueCount++;
        if (zone.IsAbsent && isDateTimeLexical)
        {
            ZoneLessTemporalValueCount++;
        }
    }

    private void Note(string message)
    {
        if (_diagnostics.Count < Options.MaxDiagnostics)
        {
            _diagnostics.Add(message);
        }
    }

    /// <summary>
    /// Reads the current element's text content and advances past it. An empty element reads as the
    /// empty string.
    /// </summary>
    public string ReadText(XmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.ReadElementContentAsString();
    }

    /// <summary>
    /// Reads the current element into a fresh builder produced by <paramref name="codec"/>, and
    /// advances past it.
    /// </summary>
    public RosettaModelObjectBuilder ReadObject(XmlReader reader, IXmlTypeCodec codec)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(codec);
        return XmlElementReader.ReadElement(reader, codec, this);
    }

    /// <summary>
    /// Reads the current element into a fresh builder produced by <paramref name="codec"/>, cast to
    /// the generated builder type.
    /// </summary>
    public TBuilder ReadObject<TBuilder>(XmlReader reader, IXmlTypeCodec codec)
        where TBuilder : RosettaModelObjectBuilder =>
        (TBuilder)ReadObject(reader, codec);

    /// <summary>
    /// Resolves the current element against a substitution group and reads it. Returns null (after
    /// counting an unknown element) when no member of the group has that element name.
    /// </summary>
    public RosettaModelObjectBuilder? ReadSubstituted(XmlReader reader, XmlQName group, string owner)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var elementName = new XmlQName(reader.NamespaceURI, reader.LocalName);
        IXmlTypeCodec? codec = Registry.FindSubstitute(group, elementName);
        if (codec == null)
        {
            NoteUnknownElement(owner, elementName);
            return null;
        }
        return ReadObject(reader, codec);
    }

    /// <summary>
    /// Resolves the current element through an <c>elementRef</c> and reads it. The reference may
    /// name a declared element or a transitive substitution-group head.
    /// </summary>
    public RosettaModelObjectBuilder? ReadElementReference(XmlReader reader, XmlQName reference, string owner)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var elementName = new XmlQName(reader.NamespaceURI, reader.LocalName);
        IXmlTypeCodec? codec = Registry.FindElementReference(reference, elementName);
        if (codec == null)
        {
            NoteUnknownElement(owner, elementName);
            return null;
        }
        return ReadObject(reader, codec);
    }

    /// <summary>Skips the current element and everything under it.</summary>
    public void Skip(XmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Skip();
    }
}
