using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Finos.Rune.Runtime.Path;

/// <summary>
/// Location of a value inside a model object tree, e.g. <c>TradeState.trade.tradeLot(0).priceQuantity</c>.
/// Mirrors com.rosetta.model.lib.path.RosettaPath.
/// </summary>
/// <remarks>
/// <para>The rendered form is the primary key of every validation result, so
/// <see cref="BuildPath"/> is byte-compatible with the Java implementation: elements joined with
/// <c>.</c>, each element being its name, then <c>(index)</c> when indexed, then
/// <c>[k=v,...]</c> when it carries attributes.</para>
/// <para>Elements with no index compare and hash equal to elements with index 0 — Java relies on
/// this so that a path built during a walk matches one parsed from a string.</para>
/// <para>Java's RosettaPathTree is not ported: it serves the mapping/reference tier, which has no
/// C# counterpart yet.</para>
/// </remarks>
public sealed class RosettaPath : IComparable<RosettaPath>, IEquatable<RosettaPath>
{
    private readonly RosettaPath? _parent;
    private readonly Element? _element;
    private readonly bool _isNullPath;

    private RosettaPath(RosettaPath? parent, Element? element, bool isNullPath = false)
    {
        _parent = parent;
        _element = element;
        _isNullPath = isNullPath;
    }

    /// <summary>
    /// A path with zero notional elements: <see cref="NewSubPath(string)"/> on it yields a
    /// one-element root path rather than appending. Mirrors Java's RosettaPath.NullPath.
    /// </summary>
    public static RosettaPath Null { get; } = new RosettaPath(null, null, isNullPath: true);

    public static RosettaPath CreatePath(Element element) => new RosettaPath(null, element);

    public static RosettaPath CreatePath(RosettaPath? parent, Element element) => new RosettaPath(parent, element);

    /// <summary>Builds a path from an ordered element list; returns null for an empty list.</summary>
    public static RosettaPath? CreatePathFromElements(IEnumerable<Element> elements)
    {
        RosettaPath? path = null;
        foreach (Element element in elements)
        {
            if (path == null)
            {
                path = CreatePath(Element.Create(element.Uri, element.PathName, element.Index, element.Attributes));
            }
            else
            {
                path = path.NewSubPath(element);
            }
        }
        return path;
    }

    /// <summary>Parses the rendered form, e.g. <c>TradeState.trade.tradeLot(0)</c>.</summary>
    public static RosettaPath ValueOf(string stringPath)
    {
        ArgumentNullException.ThrowIfNull(stringPath);
        string[] sections = stringPath.Split('.');
        RosettaPath path = CreatePath(Element.ValueOf(sections[0]));
        for (int i = 1; i < sections.Length; i++)
        {
            path = path.NewSubPath(Element.ValueOf(sections[i]));
        }
        return path;
    }

    public RosettaPath NewSubPath(Element? element)
    {
        if (_parent == null && _element == null)
        {
            return new RosettaPath(null, element, _isNullPath && element == null);
        }
        return new RosettaPath(this, element);
    }

    public RosettaPath NewSubPath(string path)
    {
        Element element = Element.Create(path, null, null);
        if (_isNullPath)
        {
            return CreatePath(element);
        }
        return NewSubPath(element);
    }

    public RosettaPath NewSubPath(string path, int index)
    {
        Element element = Element.Create(path, index, null);
        if (_isNullPath)
        {
            return CreatePath(element);
        }
        return NewSubPath(element);
    }

    public RosettaPath WithIndex(int index) =>
        new RosettaPath(_parent, RequireElement().WithIndex(index));

    /// <summary>Drops the root element, e.g. <c>TradeState.trade</c> becomes <c>trade</c>.</summary>
    public RosettaPath? TrimFirst()
    {
        List<Element> elements = AllElements();
        elements.RemoveAt(0);
        return CreatePathFromElements(elements);
    }

    /// <summary>All elements, root first.</summary>
    public List<Element> AllElements()
    {
        var elements = new List<Element>();
        if (_parent != null)
        {
            elements.AddRange(_parent.AllElements());
        }
        if (_element != null)
        {
            elements.Add(_element);
        }
        return elements;
    }

    public List<string> AllElementPaths() => AllElements().ConvertAll(e => e.PathName);

    /// <summary>The rendered form. This is what appears in validation results.</summary>
    public string BuildPath()
    {
        var sb = new StringBuilder();
        AppendTo(sb);
        return sb.ToString();
    }

    private void AppendTo(StringBuilder sb)
    {
        if (_parent != null)
        {
            _parent.AppendTo(sb);
            sb.Append('.');
        }
        if (_element != null)
        {
            _element.AppendTo(sb);
        }
    }

    public RosettaPath? Parent => _parent;

    public Element? PathElement => _element;

    private bool HasParent => _parent != null;

    private Element RequireElement() =>
        _element ?? throw new InvalidOperationException("Path has no element.");

    /// <summary>True when this path ends with <paramref name="other"/>; <c>*</c> matches any name.</summary>
    public bool EndsWith(RosettaPath? other)
    {
        if (other == null)
        {
            return true;
        }
        if (other._element == null || _element == null)
        {
            return other._element == null && _element == null;
        }
        if (other._element.PathName != "*" && other._element.PathName != _element.PathName)
        {
            return false;
        }
        if (other.HasParent && !HasParent)
        {
            return false;
        }
        if (!HasParent && !other.HasParent)
        {
            return true;
        }
        return _parent!.EndsWith(other._parent);
    }

    public bool ContainsPath(RosettaPath? subPath)
    {
        if (subPath == null)
        {
            return true;
        }
        if (subPath._element != null && _element != null
            && (subPath._element.PathName == _element.PathName || subPath._element.PathName == "*"))
        {
            return _parent != null && _parent.EndsWith(subPath._parent);
        }
        if (_parent == null)
        {
            return false;
        }
        return _parent.ContainsPath(subPath);
    }

    public bool StartsWith(RosettaPath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        List<Element> list = AllElements();
        List<Element> prefix = other.AllElements();
        if (prefix.Count == 0)
        {
            return true;
        }
        if (list.Count < prefix.Count)
        {
            return false;
        }
        for (int i = 0; i < prefix.Count; i++)
        {
            if (!list[i].Equals(prefix[i]))
            {
                return false;
            }
        }
        return true;
    }

    public bool MatchesIgnoringIndex(RosettaPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_element == null || path._element == null)
        {
            return _element == null && path._element == null;
        }
        if (_element.PathName != path._element.PathName)
        {
            return false;
        }
        if (_parent == null)
        {
            return path._parent == null;
        }
        if (path._parent == null)
        {
            return false;
        }
        return _parent.MatchesIgnoringIndex(path._parent);
    }

    /// <summary>This path with every index removed.</summary>
    public RosettaPath ToIndexless()
    {
        RosettaPath? newParent = _parent?.ToIndexless();
        Element? newElement = null;
        if (_element != null)
        {
            newElement = Element.Create(_element.Uri, _element.PathName, null, _element.Attributes);
        }
        return new RosettaPath(newParent, newElement, _isNullPath);
    }

    public override string ToString() => BuildPath();

    public bool Equals(RosettaPath? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (other is null)
        {
            return false;
        }
        if (_parent != null ? !_parent.Equals(other._parent) : other._parent != null)
        {
            return false;
        }
        return _element != null ? _element.Equals(other._element) : other._element == null;
    }

    public override bool Equals(object? obj) => Equals(obj as RosettaPath);

    public override int GetHashCode()
    {
        int result = _parent != null ? _parent.GetHashCode() : 0;
        return (31 * result) + (_element != null ? _element.GetHashCode() : 0);
    }

    /// <summary>
    /// Orders paths element by element, name then index. Elements with no index sort as index 0.
    /// </summary>
    public int CompareTo(RosettaPath? other)
    {
        if (other == null)
        {
            return 1;
        }
        List<Element> mine = AllElements();
        List<Element> theirs = other.AllElements();
        int i = 0;
        while (i < mine.Count || i < theirs.Count)
        {
            if (i >= mine.Count)
            {
                return -1;
            }
            if (i >= theirs.Count)
            {
                return 1;
            }
            int result = mine[i].CompareTo(theirs[i]);
            if (result != 0)
            {
                return result;
            }
            i++;
        }
        return 0;
    }

    /// <summary>One segment of a <see cref="RosettaPath"/>.</summary>
    public sealed class Element : IComparable<Element>, IEquatable<Element>
    {
        /// <summary>Java's RosettaPath.Element.DEFAULT_URI. Part of identity, never rendered.</summary>
        public const string DefaultUri = "FpML_5_10";

        private static readonly Regex ElementPattern =
            new(@"^(\w+|\*)(?:\(([0-9]+)\))?(?:\[([\w\-]+)=([\w\-]+)])?$", RegexOptions.Compiled);

        private static readonly KeyValuePair<string, string>[] NoAttributes = [];

        private readonly KeyValuePair<string, string>[] _attributes;

        private Element(string uri, string pathName, int? index, KeyValuePair<string, string>[]? attributes)
        {
            Uri = uri;
            PathName = pathName;
            Index = index;
            _attributes = attributes ?? NoAttributes;
        }

        public static Element Create(string pathName, int? index, IEnumerable<KeyValuePair<string, string>>? attributes) =>
            Create(DefaultUri, pathName, index, attributes);

        public static Element Create(
            string uri, string pathName, int? index, IEnumerable<KeyValuePair<string, string>>? attributes)
        {
            ArgumentNullException.ThrowIfNull(pathName);
            return new Element(uri, pathName, index, attributes?.ToArray());
        }

        /// <summary>Parses <c>fieldName(index)[key=value]</c>, where index and attributes are optional.</summary>
        public static Element ValueOf(string element)
        {
            ArgumentNullException.ThrowIfNull(element);
            Match match = ElementPattern.Match(element);
            if (!match.Success)
            {
                throw new ArgumentException($"{element} is not a valid path element", nameof(element));
            }
            int? index = null;
            if (match.Groups[2].Success)
            {
                index = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            }
            KeyValuePair<string, string>[]? attributes = null;
            if (match.Groups[3].Success && match.Groups[4].Success)
            {
                attributes = [new KeyValuePair<string, string>(match.Groups[3].Value, match.Groups[4].Value)];
            }
            return new Element(DefaultUri, match.Groups[1].Value, index, attributes);
        }

        public Element WithIndex(int newIndex) => new Element(Uri, PathName, newIndex, _attributes);

        public string Uri { get; }

        /// <summary>The Rune attribute name. Named PathName because <c>Path</c> would shadow the namespace.</summary>
        public string PathName { get; }

        public int? Index { get; }

        public IReadOnlyList<KeyValuePair<string, string>> Attributes => _attributes;

        internal void AppendTo(StringBuilder sb)
        {
            sb.Append(PathName);
            if (Index.HasValue)
            {
                sb.Append('(').Append(Index.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
            }
            if (_attributes.Length > 0)
            {
                sb.Append('[');
                for (int i = 0; i < _attributes.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(_attributes[i].Key).Append('=').Append(_attributes[i].Value);
                }
                sb.Append(']');
            }
        }

        public string AsPathString()
        {
            var sb = new StringBuilder();
            AppendTo(sb);
            return sb.ToString();
        }

        public override string ToString() =>
            $"Element{{path='{PathName}', index={Index}, uri='{Uri}', attrs={AsPathString()}}}";

        public bool Equals(Element? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other is null)
            {
                return false;
            }
            if (Uri != other.Uri || PathName != other.PathName)
            {
                return false;
            }
            if ((Index ?? 0) != (other.Index ?? 0))
            {
                return false;
            }
            return AttributesEqual(_attributes, other._attributes);
        }

        public override bool Equals(object? obj) => Equals(obj as Element);

        public override int GetHashCode()
        {
            int result = Uri.GetHashCode(StringComparison.Ordinal);
            result = (31 * result) + PathName.GetHashCode(StringComparison.Ordinal);
            result = (31 * result) + (Index ?? 0).GetHashCode();
            int attrHash = 0;
            foreach (KeyValuePair<string, string> attr in _attributes)
            {
                attrHash += attr.Key.GetHashCode(StringComparison.Ordinal)
                    ^ attr.Value.GetHashCode(StringComparison.Ordinal);
            }
            return (31 * result) + attrHash;
        }

        private static bool AttributesEqual(
            KeyValuePair<string, string>[] left, KeyValuePair<string, string>[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }
            foreach (KeyValuePair<string, string> attr in left)
            {
                bool found = false;
                foreach (KeyValuePair<string, string> candidate in right)
                {
                    if (candidate.Key == attr.Key && candidate.Value == attr.Value)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Name, then index; absent index sorts as 0.</summary>
        public int CompareTo(Element? other)
        {
            if (other == null)
            {
                return 1;
            }
            int byName = string.CompareOrdinal(PathName, other.PathName);
            if (byName != 0)
            {
                return byName;
            }
            return (Index ?? 0).CompareTo(other.Index ?? 0);
        }

        [SuppressMessage("Usage", "CA2225", Justification = "Comparison operators are not part of the Java contract.")]
        public static bool operator ==(Element? left, Element? right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(Element? left, Element? right) => !(left == right);

        public static bool operator <(Element? left, Element? right) =>
            left is null ? right is not null : left.CompareTo(right) < 0;

        public static bool operator <=(Element? left, Element? right) =>
            left is null || left.CompareTo(right) <= 0;

        public static bool operator >(Element? left, Element? right) =>
            left is not null && left.CompareTo(right) > 0;

        public static bool operator >=(Element? left, Element? right) =>
            left is null ? right is null : left.CompareTo(right) >= 0;
    }

    public static bool operator ==(RosettaPath? left, RosettaPath? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(RosettaPath? left, RosettaPath? right) => !(left == right);

    public static bool operator <(RosettaPath? left, RosettaPath? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(RosettaPath? left, RosettaPath? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(RosettaPath? left, RosettaPath? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(RosettaPath? left, RosettaPath? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;
}
