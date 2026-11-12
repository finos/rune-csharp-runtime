namespace Finos.Rune.Runtime.Xml;

/// <summary>The kind of timezone an XML lexical date/time value carried.</summary>
public enum XmlTimeZoneKind
{
    /// <summary>No timezone was written. XSD allows this; the value is zone-less.</summary>
    Absent,

    /// <summary>The value ended in <c>Z</c>.</summary>
    Utc,

    /// <summary>The value carried an explicit numeric offset.</summary>
    Offset,
}

/// <summary>
/// The timezone an XML date/time value was written with, kept losslessly alongside the
/// <see cref="DateTimeOffset"/> the generated property exposes.
/// </summary>
/// <remarks>
/// XSD makes the timezone of <c>xsd:dateTime</c> optional and allows several lexical spellings of
/// the same offset (<c>+01</c>, <c>+0100</c>, <c>+01:00</c>). A <see cref="DateTimeOffset"/> can
/// represent the instant but not which of those was written, and the Java reference works around
/// the zone-less case with a synthetic "Unknown" zone id. Keeping the original
/// <see cref="Lexical"/> spelling here lets a generated codec write back exactly what it read
/// without changing the public property type.
/// </remarks>
public readonly struct XmlZoneMetadata : IEquatable<XmlZoneMetadata>
{
    private readonly string? _lexical;

    private XmlZoneMetadata(XmlTimeZoneKind kind, TimeSpan offset, string? lexical)
    {
        Kind = kind;
        Offset = offset;
        _lexical = lexical;
    }

    /// <summary>A value written with no timezone at all.</summary>
    public static XmlZoneMetadata Absent => default;

    /// <summary>A value written with a trailing <c>Z</c>.</summary>
    public static XmlZoneMetadata Utc => new XmlZoneMetadata(XmlTimeZoneKind.Utc, TimeSpan.Zero, "Z");

    /// <summary>A value written with an explicit offset, preserving its lexical spelling.</summary>
    public static XmlZoneMetadata OfOffset(TimeSpan offset, string lexical)
    {
        ArgumentNullException.ThrowIfNull(lexical);
        return new XmlZoneMetadata(XmlTimeZoneKind.Offset, offset, lexical);
    }

    /// <summary>A value written with an explicit offset in canonical <c>+HH:mm</c> spelling.</summary>
    public static XmlZoneMetadata OfOffset(TimeSpan offset) => OfOffset(offset, FormatOffset(offset));

    public XmlTimeZoneKind Kind { get; }

    /// <summary>The UTC offset. Zero for <see cref="XmlTimeZoneKind.Absent"/> and Utc.</summary>
    public TimeSpan Offset { get; }

    /// <summary>The exact suffix that was written: empty, <c>Z</c>, or an offset spelling.</summary>
    public string Lexical => _lexical ?? string.Empty;

    /// <summary>True when the value carried no timezone.</summary>
    public bool IsAbsent => Kind == XmlTimeZoneKind.Absent;

    /// <summary>Canonical <c>+HH:mm</c>/<c>-HH:mm</c> spelling of an offset.</summary>
    public static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);

    public bool Equals(XmlZoneMetadata other) =>
        Kind == other.Kind && Offset == other.Offset && string.Equals(Lexical, other.Lexical, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is XmlZoneMetadata other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Offset, Lexical);

    public override string ToString() => Kind switch
    {
        XmlTimeZoneKind.Absent => "<no timezone>",
        XmlTimeZoneKind.Utc => "Z",
        _ => Lexical,
    };

    public static bool operator ==(XmlZoneMetadata left, XmlZoneMetadata right) => left.Equals(right);

    public static bool operator !=(XmlZoneMetadata left, XmlZoneMetadata right) => !left.Equals(right);
}

/// <summary>
/// XML-only lexical metadata for a model object's temporal attributes, keyed by Rune property
/// name. Immutable, so it can be carried through <c>Build</c>/<c>ToBuilder</c> unchanged.
/// </summary>
public sealed class XmlTemporalMetadata : IEquatable<XmlTemporalMetadata>
{
    /// <summary>The empty instance; carries no zone information.</summary>
    public static readonly XmlTemporalMetadata Empty = new XmlTemporalMetadata(new Dictionary<string, XmlZoneMetadata>(StringComparer.Ordinal));

    private readonly Dictionary<string, XmlZoneMetadata> _zones;

    private XmlTemporalMetadata(Dictionary<string, XmlZoneMetadata> zones)
    {
        _zones = zones;
    }

    /// <summary>True when nothing is recorded — the state a Prune() collapses to null.</summary>
    public bool IsEmpty => _zones.Count == 0;

    /// <summary>The recorded property names, in insertion order.</summary>
    public IReadOnlyCollection<string> Properties => _zones.Keys;

    /// <summary>Returns a copy with <paramref name="propertyName"/> recorded.</summary>
    public XmlTemporalMetadata With(string propertyName, XmlZoneMetadata zone)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        var copy = new Dictionary<string, XmlZoneMetadata>(_zones, StringComparer.Ordinal)
        {
            [propertyName] = zone,
        };
        return new XmlTemporalMetadata(copy);
    }

    /// <summary>Returns a copy with <paramref name="propertyName"/> removed.</summary>
    public XmlTemporalMetadata Without(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (!_zones.ContainsKey(propertyName))
        {
            return this;
        }
        var copy = new Dictionary<string, XmlZoneMetadata>(_zones, StringComparer.Ordinal);
        copy.Remove(propertyName);
        return new XmlTemporalMetadata(copy);
    }

    public bool TryGet(string propertyName, out XmlZoneMetadata zone) => _zones.TryGetValue(propertyName, out zone);

    /// <summary>
    /// The zone recorded for <paramref name="propertyName"/>, or
    /// <see cref="XmlZoneMetadata.Absent"/> when nothing was recorded.
    /// </summary>
    public XmlZoneMetadata GetOrAbsent(string propertyName) =>
        _zones.TryGetValue(propertyName, out XmlZoneMetadata zone) ? zone : XmlZoneMetadata.Absent;

    /// <summary>
    /// Prune semantics for a generated builder: an instance carrying nothing becomes null, so an
    /// object that only ever held XML metadata is still pruned away like any other empty child.
    /// </summary>
    public XmlTemporalMetadata? Prune() => IsEmpty ? null : this;

    public bool Equals(XmlTemporalMetadata? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (other is null || _zones.Count != other._zones.Count)
        {
            return false;
        }
        foreach (KeyValuePair<string, XmlZoneMetadata> entry in _zones)
        {
            if (!other._zones.TryGetValue(entry.Key, out XmlZoneMetadata theirs) || !entry.Value.Equals(theirs))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as XmlTemporalMetadata);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (KeyValuePair<string, XmlZoneMetadata> entry in _zones.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            hash = (hash * 31) + entry.Key.GetHashCode(StringComparison.Ordinal);
            hash = (hash * 31) + entry.Value.GetHashCode();
        }
        return hash;
    }

    public override string ToString() =>
        "{" + string.Join(", ", _zones.Select(e => e.Key + "=" + e.Value)) + "}";
}

/// <summary>
/// Implemented by a generated model object that can carry XML lexical timezone metadata.
/// The public temporal properties stay <see cref="DateTimeOffset"/>; this is additive.
/// </summary>
public interface IXmlTemporalMetadataHolder
{
    /// <summary>The recorded lexical timezones, or null when none were recorded.</summary>
    XmlTemporalMetadata? XmlTemporalMetadata { get; }
}

/// <summary>
/// Implemented by a generated builder that can carry XML lexical timezone metadata, so a codec can
/// preserve it across <c>ToBuilder</c>/<c>Build</c>/<c>Prune</c>.
/// </summary>
public interface IXmlTemporalMetadataBuilder : IXmlTemporalMetadataHolder
{
    /// <summary>Replaces the recorded lexical timezones.</summary>
    void SetXmlTemporalMetadata(XmlTemporalMetadata? metadata);
}

/// <summary>Convenience over <see cref="IXmlTemporalMetadataBuilder"/> for generated codecs.</summary>
public static class XmlTemporalMetadataExtensions
{
    /// <summary>Records one property's lexical timezone, creating the map when needed.</summary>
    public static void RecordXmlZone(this IXmlTemporalMetadataBuilder builder, string propertyName, XmlZoneMetadata zone)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetXmlTemporalMetadata(
            (builder.XmlTemporalMetadata ?? XmlTemporalMetadata.Empty).With(propertyName, zone));
    }

    /// <summary>
    /// Copies one scalar or list-valued temporal property's recorded XML zones to another builder.
    /// </summary>
    public static void CopyXmlZones(
        IXmlTemporalMetadataHolder? source,
        string sourcePropertyName,
        IXmlTemporalMetadataBuilder target,
        string targetPropertyName)
    {
        ArgumentNullException.ThrowIfNull(sourcePropertyName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetPropertyName);
        if (source?.XmlTemporalMetadata == null)
        {
            return;
        }

        string listPrefix = sourcePropertyName + "[";
        foreach (string property in source.XmlTemporalMetadata.Properties)
        {
            string? targetProperty = null;
            if (string.Equals(property, sourcePropertyName, StringComparison.Ordinal))
            {
                targetProperty = targetPropertyName;
            }
            else if (property.StartsWith(listPrefix, StringComparison.Ordinal))
            {
                targetProperty = targetPropertyName + property[sourcePropertyName.Length..];
            }

            if (targetProperty != null
                && source.XmlTemporalMetadata.TryGet(property, out XmlZoneMetadata zone))
            {
                target.RecordXmlZone(targetProperty, zone);
            }
        }
    }

    /// <summary>
    /// The timezone to write one property with: the lexical form it was read with when that was
    /// recorded, and otherwise one derived from the value's own offset.
    /// </summary>
    /// <remarks>
    /// The fallback has to be driven by <see cref="XmlTemporalMetadata.TryGet"/> rather than by a
    /// null check on the holder. A value read from XML records a zone for every temporal property it
    /// saw, but an object assembled in code — or one whose other temporal property came from a
    /// different source — carries a metadata map that is present yet silent about this property.
    /// Treating "map present" as "zone known" would then write
    /// <see cref="XmlZoneMetadata.Absent"/> and drop a real offset from the document.
    /// </remarks>
    public static XmlZoneMetadata ResolveZone(
        IXmlTemporalMetadataHolder? holder, string propertyName, DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (holder?.XmlTemporalMetadata != null
            && holder.XmlTemporalMetadata.TryGet(propertyName, out XmlZoneMetadata recorded))
        {
            return recorded;
        }
        return value.Offset == TimeSpan.Zero ? XmlZoneMetadata.Utc : XmlZoneMetadata.OfOffset(value.Offset);
    }
}
