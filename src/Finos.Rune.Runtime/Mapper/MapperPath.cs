using System.Globalization;
using System.Text;

namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// Provenance of a mapped value: the chain of accessors that produced it.
/// Mirrors com.rosetta.model.lib.mapper.MapperPath.
/// </summary>
/// <remarks>
/// <para>Not an internal detail — <see cref="FullPath"/> is interpolated verbatim into data-rule
/// failure messages, e.g. <c>[PriceSchedule-&gt;getValue] does not exist</c>. The emitted C# must
/// therefore pass Java-style accessor names (<c>"getValue"</c>, derived from the Rune attribute
/// name) rather than C# property names; see harness/NOTES.md.</para>
/// <para>Distinct from <see cref="Path.RosettaPath"/>, which locates a node in the object tree for
/// validation results. This one records how an expression reached a value.</para>
/// </remarks>
public sealed class MapperPath : IMapperPath, IComparable<MapperPath>, IEquatable<MapperPath>
{
    private readonly PathElement[] _elements;

    private MapperPath(PathElement[] elements)
    {
        _elements = elements;
    }

    /// <summary>An empty path to append to.</summary>
    public static MapperPathBuilder Builder() => new([]);

    /// <summary>This path, ready to be appended to.</summary>
    public MapperPathBuilder ToBuilder() => new(_elements);

    /// <inheritdoc />
    public IReadOnlyList<string> Names => Array.ConvertAll(_elements, e => e.Name);

    /// <inheritdoc />
    public IReadOnlyList<string> Getters => Array.ConvertAll(_elements, e => e.Getter);

    /// <inheritdoc />
    public string LastName => _elements[^1].Name;

    /// <inheritdoc />
    public string FullPath
    {
        get
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _elements.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append("->");
                }
                sb.Append(_elements[i].GetterAndContext);
            }
            return sb.ToString();
        }
    }

    /// <inheritdoc />
    public override string ToString() => FullPath;

    /// <inheritdoc />
    public bool Equals(MapperPath? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (other is null || _elements.Length != other._elements.Length)
        {
            return false;
        }
        for (int i = 0; i < _elements.Length; i++)
        {
            if (!_elements[i].Equals(other._elements[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MapperPath);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int result = 1;
        foreach (PathElement element in _elements)
        {
            result = (31 * result) + element.GetHashCode();
        }
        return result;
    }

    /// <summary>Element by element; a shorter path sorts before a longer one that shares its prefix.</summary>
    public int CompareTo(MapperPath? other)
    {
        if (other == null)
        {
            return 1;
        }
        int shared = Math.Min(_elements.Length, other._elements.Length);
        for (int i = 0; i < shared; i++)
        {
            int result = _elements[i].CompareTo(other._elements[i]);
            if (result != 0)
            {
                return result;
            }
        }
        return _elements.Length.CompareTo(other._elements.Length);
    }

    /// <summary>Appends elements to a path. Each Add returns the completed path.</summary>
    public sealed class MapperPathBuilder
    {
        private readonly List<PathElement> _elements;

        internal MapperPathBuilder(IReadOnlyCollection<PathElement> elements)
        {
            _elements = new List<PathElement>(elements);
        }

        /// <summary>Records that the chain reached a null. Renders as <c>Null</c>.</summary>
        public MapperPath AddNull() => Add(new PathElement("Null"));

        /// <summary>Roots the path at a type's simple name.</summary>
        public MapperPath AddRoot(Type type) => Add(new PathElement(SimpleName(type)));

        /// <summary>Appends an accessor, e.g. <c>getValue</c>.</summary>
        public MapperPath AddFunctionName(string name) => Add(new PathElement(name));

        /// <summary>Appends an accessor reached at a list index, rendering as <c>getFoo[1]</c>.</summary>
        public MapperPath AddListFunctionName(string name, int listIndex) =>
            Add(new PathElement(name, listIndex));

        private MapperPath Add(PathElement element)
        {
            _elements.Add(element);
            return new MapperPath([.. _elements]);
        }

        private static string SimpleName(Type type)
        {
            string name = type.Name;
            int arity = name.IndexOf('`', StringComparison.Ordinal);
            return arity < 0 ? name : name[..arity];
        }
    }

    internal sealed class PathElement : IComparable<PathElement>, IEquatable<PathElement>
    {
        private readonly int? _listIndex;

        internal PathElement(string getter)
            : this(getter, null)
        {
        }

        internal PathElement(string getter, int? listIndex)
        {
            Getter = getter;
            Name = ToAttributeName(getter);
            _listIndex = listIndex;
            GetterAndContext = listIndex.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", getter, listIndex.Value)
                : getter;
        }

        internal string Name { get; }

        internal string Getter { get; }

        internal string GetterAndContext { get; }

        /// <summary>
        /// <c>getFoo</c> becomes <c>foo</c>; anything not starting with <c>get</c> is left alone.
        /// Java uses Guava's UPPER_CAMEL to LOWER_CAMEL, which only lower-cases the leading
        /// character of the remainder.
        /// </summary>
        private static string ToAttributeName(string getter)
        {
            if (!getter.StartsWith("get", StringComparison.Ordinal) || getter.Length == 3)
            {
                return getter;
            }
            string remainder = getter[3..];
            return char.ToLowerInvariant(remainder[0]) + remainder[1..];
        }

        public bool Equals(PathElement? other) =>
            other is not null
            && Getter == other.Getter
            && Name == other.Name
            && _listIndex == other._listIndex;

        public override bool Equals(object? obj) => Equals(obj as PathElement);

        public override int GetHashCode() => HashCode.Combine(Getter, Name, _listIndex);

        public int CompareTo(PathElement? other)
        {
            if (other == null)
            {
                return 1;
            }
            int byName = string.CompareOrdinal(Name, other.Name);
            if (byName != 0)
            {
                return byName;
            }
            if (_listIndex.HasValue && other._listIndex.HasValue)
            {
                return _listIndex.Value.CompareTo(other._listIndex.Value);
            }
            if (_listIndex.HasValue)
            {
                return -1;
            }
            return other._listIndex.HasValue ? 1 : 0;
        }
    }
}
