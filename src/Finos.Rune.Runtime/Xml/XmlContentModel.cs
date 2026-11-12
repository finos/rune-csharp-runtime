namespace Finos.Rune.Runtime.Xml;

/// <summary>The kind of an XSD content-model node.</summary>
public enum XmlContentModelNodeType
{
    Element,
    Sequence,
    Choice,
    All,
    Any,
}

/// <summary>
/// The <c>maxOccurs</c> bound of a content-model node: a finite non-negative count or
/// "unbounded". Mirrors rune-common's <c>OccursMax</c>; the default value is unbounded.
/// </summary>
public readonly struct XmlOccursMax : IEquatable<XmlOccursMax>
{
    private readonly int _value;
    private readonly bool _bounded;

    private XmlOccursMax(int value, bool bounded)
    {
        _value = value;
        _bounded = bounded;
    }

    /// <summary>The <c>maxOccurs="unbounded"</c> bound.</summary>
    public static XmlOccursMax Unbounded => default;

    /// <summary>A finite bound.</summary>
    public static XmlOccursMax Of(int value) => value < 0
        ? throw new ArgumentOutOfRangeException(nameof(value), value, "maxOccurs must be non-negative.")
        : new XmlOccursMax(value, true);

    public bool IsUnbounded => !_bounded;

    /// <summary>The finite bound, or null when unbounded.</summary>
    public int? Value => _bounded ? _value : null;

    /// <summary>
    /// Resolves the bound against the number of input items still to consume: an unbounded node
    /// can never usefully repeat more times than there is input left.
    /// </summary>
    public int BoundedValue(int inputRemaining) => _bounded ? _value : inputRemaining;

    public bool Equals(XmlOccursMax other) => _bounded == other._bounded && (!_bounded || _value == other._value);

    public override bool Equals(object? obj) => obj is XmlOccursMax other && Equals(other);

    public override int GetHashCode() => _bounded ? _value : -1;

    public override string ToString() =>
        _bounded ? _value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unbounded";

    public static bool operator ==(XmlOccursMax left, XmlOccursMax right) => left.Equals(right);

    public static bool operator !=(XmlOccursMax left, XmlOccursMax right) => !left.Equals(right);
}

/// <summary>
/// One node of an XSD content model, describing where an XML child element is routed to in the
/// Rune property graph. Immutable, and built entirely from generated code — there is no runtime
/// JSON configuration loader on this side.
/// </summary>
public sealed class XmlContentModelNode : IEquatable<XmlContentModelNode>
{
    private static readonly IReadOnlyList<string> NoPath = Array.Empty<string>();
    private static readonly IReadOnlyList<XmlContentModelNode> NoChildren = Array.Empty<XmlContentModelNode>();

    public XmlContentModelNode(
        XmlContentModelNodeType nodeType,
        string? xmlName = null,
        string? ns = null,
        IReadOnlyList<string>? path = null,
        int? minOccurs = null,
        XmlOccursMax? maxOccurs = null,
        IReadOnlyList<XmlContentModelNode>? children = null)
    {
        NodeType = nodeType;
        XmlName = xmlName;
        Namespace = string.IsNullOrEmpty(ns) ? null : ns;
        Path = path?.ToArray();
        MinOccurs = minOccurs;
        MaxOccurs = maxOccurs;
        Children = children?.ToArray();
    }

    public XmlContentModelNodeType NodeType { get; }

    /// <summary>The XML element name this node matches; absent for groups and for <c>ANY</c>.</summary>
    public string? XmlName { get; }

    /// <summary>The namespace this node requires, or null to accept any namespace.</summary>
    public string? Namespace { get; }

    /// <summary>
    /// The Rune property path a matched element is routed to. A single segment is a direct
    /// property of the enclosing type; more than one segment routes through VIRTUAL wrappers.
    /// </summary>
    public IReadOnlyList<string>? Path { get; }

    public int? MinOccurs { get; }

    public XmlOccursMax? MaxOccurs { get; }

    public IReadOnlyList<XmlContentModelNode>? Children { get; }

    /// <summary>Effective <c>minOccurs</c>, defaulting to 1.</summary>
    public int MinOccursOrDefault => MinOccurs ?? 1;

    /// <summary>Effective <c>maxOccurs</c> bounded by the remaining input; defaults to 1.</summary>
    public int MaxOccursOrDefault(int inputRemaining) =>
        MaxOccurs.HasValue ? MaxOccurs.Value.BoundedValue(inputRemaining) : 1;

    /// <summary>The path, or an empty list when the node routes nothing.</summary>
    public IReadOnlyList<string> PathOrEmpty => Path ?? NoPath;

    /// <summary>The children, or an empty list for leaf nodes.</summary>
    public IReadOnlyList<XmlContentModelNode> ChildrenOrEmpty => Children ?? NoChildren;

    public static XmlContentModelNode Element(
        string xmlName, IReadOnlyList<string> path, int minOccurs, XmlOccursMax maxOccurs, string? ns = null) =>
        new XmlContentModelNode(XmlContentModelNodeType.Element, xmlName, ns, path, minOccurs, maxOccurs);

    public static XmlContentModelNode Any(
        IReadOnlyList<string>? path, int minOccurs, XmlOccursMax maxOccurs, string? ns = null) =>
        new XmlContentModelNode(XmlContentModelNodeType.Any, null, ns, path, minOccurs, maxOccurs);

    public static XmlContentModelNode Sequence(
        int minOccurs, XmlOccursMax maxOccurs, params XmlContentModelNode[] children) =>
        new XmlContentModelNode(XmlContentModelNodeType.Sequence, null, null, null, minOccurs, maxOccurs, children);

    public static XmlContentModelNode Choice(
        int minOccurs, XmlOccursMax maxOccurs, params XmlContentModelNode[] children) =>
        new XmlContentModelNode(XmlContentModelNodeType.Choice, null, null, null, minOccurs, maxOccurs, children);

    public static XmlContentModelNode All(
        int minOccurs, XmlOccursMax maxOccurs, params XmlContentModelNode[] children) =>
        new XmlContentModelNode(XmlContentModelNodeType.All, null, null, null, minOccurs, maxOccurs, children);

    public bool Equals(XmlContentModelNode? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (other is null || NodeType != other.NodeType
            || !string.Equals(XmlName, other.XmlName, StringComparison.Ordinal)
            || !string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
            || MinOccurs != other.MinOccurs || !Nullable.Equals(MaxOccurs, other.MaxOccurs))
        {
            return false;
        }
        return SequenceEquals(Path, other.Path) && SequenceEquals(Children, other.Children);
    }

    public override bool Equals(object? obj) => Equals(obj as XmlContentModelNode);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(NodeType);
        hash.Add(XmlName);
        hash.Add(Namespace);
        hash.Add(MinOccurs);
        hash.Add(MaxOccurs);
        hash.Add(Path?.Count ?? -1);
        hash.Add(Children?.Count ?? -1);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        string name = XmlName == null ? string.Empty : "[" + XmlName + "]";
        string path = Path == null ? string.Empty : "->[" + string.Join(", ", Path) + "]";
        return NodeType.ToString().ToUpperInvariant() + name + path;
    }

    private static bool SequenceEquals<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }
        if (left.Count != right.Count)
        {
            return false;
        }
        for (int i = 0; i < left.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }
        return true;
    }
}
