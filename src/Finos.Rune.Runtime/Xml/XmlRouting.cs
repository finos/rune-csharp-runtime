using System.Runtime.CompilerServices;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Namespace state of a routed XML element.
/// </summary>
/// <remarks>
/// The distinction between <see cref="Absent"/> and <see cref="Unknown"/> is deliberate and is
/// carried over from rune-common. Real XML tells us that an element has no namespace, and that
/// must still fail when the content model requires one. Content replayed from a buffer that lost
/// its namespace context is <em>unknown</em> rather than absent, and the matcher then falls back
/// to local-name matching while still reporting ambiguity if that leaves several routes open.
/// </remarks>
public readonly struct XmlNamespaceState : IEquatable<XmlNamespaceState>
{
    private enum State
    {
        Absent,
        Present,
        Unknown,
    }

    private readonly State _state;
    private readonly string? _value;

    private XmlNamespaceState(State state, string? value)
    {
        _state = state;
        _value = value;
    }

    /// <summary>The element is known to carry <paramref name="ns"/>.</summary>
    public static XmlNamespaceState Present(string ns) =>
        new XmlNamespaceState(State.Present, ns ?? throw new ArgumentNullException(nameof(ns)));

    /// <summary>The element is known to carry no namespace.</summary>
    public static XmlNamespaceState Absent() => new XmlNamespaceState(State.Absent, null);

    /// <summary>The element's namespace was lost before routing.</summary>
    public static XmlNamespaceState Unknown() => new XmlNamespaceState(State.Unknown, null);

    public bool IsUnknown => _state == State.Unknown;

    public bool IsPresent => _state == State.Present;

    /// <summary>The namespace URI, or null when absent or unknown.</summary>
    public string? Value => _value;

    public bool Equals(XmlNamespaceState other) =>
        _state == other._state && string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is XmlNamespaceState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_state, _value);

    public override string ToString() => _state switch
    {
        State.Present => _value!,
        State.Absent => "<absent>",
        _ => "<unknown>",
    };
}

/// <summary>
/// One ambiguous XML child element handed to <see cref="XmlContentModelMatcher.Route"/>.
/// </summary>
/// <remarks>
/// <see cref="FieldIndex"/> preserves the element's ordinal position among the parent's buffered
/// children, so an assignment can be applied back to the original document order no matter which
/// children were filtered out as XML attributes or as names the content model never mentions.
/// </remarks>
public sealed class XmlRoutingInput
{
    public XmlRoutingInput(int fieldIndex, string xmlName, XmlNamespaceState ns)
    {
        ArgumentNullException.ThrowIfNull(xmlName);
        FieldIndex = fieldIndex;
        XmlName = xmlName;
        Namespace = ns;
    }

    public int FieldIndex { get; }

    public string XmlName { get; }

    public XmlNamespaceState Namespace { get; }

    public override string ToString() => XmlName + "@" + FieldIndex;
}

/// <summary>
/// Identifies one iteration of a repeated content-model group, so that two elements matched inside
/// the same iteration are routed into the same VIRTUAL object.
/// </summary>
public sealed class XmlOccurrenceKey : IEquatable<XmlOccurrenceKey>
{
    internal readonly struct Frame : IEquatable<Frame>
    {
        internal Frame(XmlContentModelNode node, int iteration)
        {
            Node = node;
            Iteration = iteration;
        }

        internal XmlContentModelNode Node { get; }

        internal int Iteration { get; }

        public bool Equals(Frame other) => ReferenceEquals(Node, other.Node) && Iteration == other.Iteration;

        public override bool Equals(object? obj) => obj is Frame other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Node), Iteration);

        public override string ToString() => Node + "#" + Iteration;
    }

    internal static readonly XmlOccurrenceKey Root = new XmlOccurrenceKey(Array.Empty<Frame>());

    private readonly Frame[] _frames;

    internal XmlOccurrenceKey(Frame[] frames)
    {
        _frames = frames;
    }

    public bool Equals(XmlOccurrenceKey? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (other is null || _frames.Length != other._frames.Length)
        {
            return false;
        }
        for (int i = 0; i < _frames.Length; i++)
        {
            if (!_frames[i].Equals(other._frames[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as XmlOccurrenceKey);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (Frame frame in _frames)
        {
            hash.Add(frame);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => "occ[" + string.Join(", ", _frames.Select(f => f.ToString())) + "]";
}

/// <summary>One routed element: where it goes, and in which repeated iteration.</summary>
public sealed class XmlRoutingAssignment
{
    internal XmlRoutingAssignment(int inputIndex, IReadOnlyList<string> path, XmlOccurrenceKey occurrence)
    {
        InputIndex = inputIndex;
        Path = path;
        Occurrence = occurrence;
    }

    /// <summary>Index into the input list passed to <see cref="XmlContentModelMatcher.Route"/>.</summary>
    public int InputIndex { get; }

    /// <summary>The Rune property path the element is routed to.</summary>
    public IReadOnlyList<string> Path { get; }

    public XmlOccurrenceKey Occurrence { get; }

    public override string ToString() => InputIndex + " -> [" + string.Join(", ", Path) + "] " + Occurrence;
}

/// <summary>Outcome of routing one parent object's children against its content model.</summary>
public enum XmlRoutingStatus
{
    Success,
    NoMatch,
    Ambiguous,
}

/// <summary>The routing decision for one parent object.</summary>
public sealed class XmlRoutingResult
{
    private readonly Dictionary<int, XmlRoutingAssignment> _byIndex;

    private XmlRoutingResult(
        XmlRoutingStatus status,
        IReadOnlyList<XmlRoutingAssignment> assignments,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> candidatePaths)
    {
        Status = status;
        Assignments = assignments;
        CandidatePaths = candidatePaths;
        _byIndex = new Dictionary<int, XmlRoutingAssignment>();
        foreach (XmlRoutingAssignment assignment in assignments)
        {
            _byIndex[assignment.InputIndex] = assignment;
        }
    }

    public XmlRoutingStatus Status { get; }

    /// <summary>The routed elements, in the order the matcher assigned them.</summary>
    public IReadOnlyList<XmlRoutingAssignment> Assignments { get; }

    /// <summary>The distinct competing assignments when <see cref="Status"/> is Ambiguous.</summary>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> CandidatePaths { get; }

    internal static XmlRoutingResult NoMatch() => new XmlRoutingResult(
        XmlRoutingStatus.NoMatch,
        Array.Empty<XmlRoutingAssignment>(),
        Array.Empty<IReadOnlyList<IReadOnlyList<string>>>());

    internal static XmlRoutingResult Ambiguous(IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> candidates) =>
        new XmlRoutingResult(XmlRoutingStatus.Ambiguous, Array.Empty<XmlRoutingAssignment>(), candidates);

    internal static XmlRoutingResult Success(IReadOnlyList<XmlRoutingAssignment> assignments) =>
        new XmlRoutingResult(
            XmlRoutingStatus.Success, assignments, Array.Empty<IReadOnlyList<IReadOnlyList<string>>>());

    /// <summary>The path assigned to <paramref name="inputIndex"/>, or null when unrouted.</summary>
    public IReadOnlyList<string>? PathAt(int inputIndex) =>
        _byIndex.TryGetValue(inputIndex, out XmlRoutingAssignment? found) ? found.Path : null;

    /// <summary>The occurrence assigned to <paramref name="inputIndex"/>, or null when unrouted.</summary>
    public XmlOccurrenceKey? OccurrenceAt(int inputIndex) =>
        _byIndex.TryGetValue(inputIndex, out XmlRoutingAssignment? found) ? found.Occurrence : null;
}
