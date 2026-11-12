namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// A mapper over a single value. Mirrors com.rosetta.model.lib.mapper.MapperS.
/// </summary>
/// <remarks>
/// <typeparamref name="T"/> is always the nullable form of the item type — <c>MapperS&lt;decimal?&gt;</c>,
/// <c>MapperS&lt;Party?&gt;</c> — because for an unconstrained <c>T</c>, <c>default(T)</c> of a
/// value type is <c>0</c> and could not represent absence.
/// </remarks>
public sealed class MapperS<T> : IMapper<T>, IMapperSingle
{
    private static readonly IReadOnlyList<IMapperPath> NoPaths = [];

    private readonly MapperItem<T> _item;

    internal MapperS(MapperItem<T> item, bool identity = false)
    {
        _item = item;
        IsIdentity = identity;
    }

    /// <summary>The placeholder the generator uses for an implicit variable not yet bound.</summary>
    public static MapperS<T> Identity() =>
        new(MapperItem<T>.Error<T>(MapperPath.Builder().AddNull()), identity: true);

    /// <summary>An absent value, at path <c>Null</c>.</summary>
    public static MapperS<T> OfNull() => new(MapperItem<T>.Error<T>(MapperPath.Builder().AddNull()));

    /// <summary>
    /// Roots a mapper at a value. A null roots at path <c>Null</c> as an error item; otherwise the
    /// root element is the value's Java simple name, which failure messages quote.
    /// </summary>
    public static MapperS<T> Of(T value)
    {
        if (value is null)
        {
            return new MapperS<T>(new MapperItem<T>(value, MapperPath.Builder().AddNull(), true, null));
        }
        MapperPath path = MapperPath.Builder().AddFunctionName(JavaTypeNames.Of(value));
        return new MapperS<T>(new MapperItem<T>(value, path, false, null));
    }

    /// <summary>Roots a mapper at a value that was reached by an existing path.</summary>
    public static MapperS<T> Of(T value, MapperPath path, IMapperItem? parent) =>
        new(new MapperItem<T>(value, path, value is null, parent));

    /// <summary>
    /// Roots a mapper at a read of the enclosing function's own output.
    /// </summary>
    /// <remarks>
    /// Identical to <see cref="Of(T)"/> except that every value reached from here is marked
    /// <see cref="IMapperItem.FromOutputBuilder"/> — see that property for why the reference
    /// distinguishes them.
    /// </remarks>
    public static MapperS<T> OfOutputBuilder(T value)
    {
        if (value is null)
        {
            return new MapperS<T>(new MapperItem<T>(value, MapperPath.Builder().AddNull(), true, null, true));
        }
        MapperPath path = MapperPath.Builder().AddFunctionName(JavaTypeNames.Of(value));
        return new MapperS<T>(new MapperItem<T>(value, path, false, null, true));
    }

    /// <summary>True for the placeholder produced by <see cref="Identity"/>.</summary>
    public bool IsIdentity { get; }

    internal MapperItem<T> Item => _item;

    /// <summary>Keeps the value when the predicate holds; a null predicate result drops it.</summary>
    public MapperS<T> FilterSingleNullSafe(Func<MapperS<T>, bool?> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (_item.IsError)
        {
            return this;
        }
        return predicate(this) == true ? this : OfNull();
    }

    /// <summary>Maps to a single child, extending the path with <paramref name="name"/>.</summary>
    public MapperS<TF> Map<TF>(string name, Func<T, TF> mappingFunc) =>
        new(MapperItem<T>.Map(_item, name, mappingFunc));

    /// <summary>
    /// Maps to a single child, treating <typeparamref name="TException"/> as absence rather than
    /// letting it escape. Backs the <c>to-string</c>/<c>to-number</c>/<c>to-enum</c> conversions.
    /// </summary>
    public MapperS<TF> CheckedMap<TF, TException>(string name, Func<T, TF> mappingFunc)
        where TException : Exception =>
        new(MapperItem<T>.CheckedMap(_item, name, mappingFunc, typeof(TException)));

    /// <summary>Maps to a list of children, each indexed in its path.</summary>
    public MapperC<TF> MapC<TF>(string name, Func<T, IReadOnlyList<TF>?> mappingFunc) =>
        new(MapperItem<T>.MapMulti(_item, name, mappingFunc));

    /// <summary>Applies a mapper-to-mapper function, short-circuiting on absence.</summary>
    public MapperS<TF> MapSingleToItem<TF>(Func<MapperS<T>, MapperS<TF>> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return _item.MappedObject is null ? MapperS<TF>.OfNull() : mappingFunc(this);
    }

    /// <summary>Applies a mapper-to-list function, short-circuiting on absence.</summary>
    public MapperC<TF> MapSingleToList<TF>(Func<MapperS<T>, MapperC<TF>> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        return _item.MappedObject is null ? MapperC<TF>.OfNull() : mappingFunc(this);
    }

    /// <inheritdoc />
    public T Get() => _item.MappedObject;

    /// <summary>The value, or <paramref name="defaultValue"/> when absent.</summary>
    /// <remarks>
    /// Declared here rather than on <see cref="IMapper{T}"/>: <c>T</c> is covariant there, so it
    /// cannot appear in an input position. Every call site knows the concrete mapper type.
    /// </remarks>
    public T GetOrDefault(T defaultValue)
    {
        T value = Get();
        return value is null ? defaultValue : value;
    }

    /// <inheritdoc />
    public IReadOnlyList<T> GetMulti() => _item.MappedObject is null ? [] : [_item.MappedObject];

    /// <inheritdoc />
    public int ResultCount => _item.MappedObject is null ? 0 : 1;

    /// <inheritdoc />
    public IReadOnlyList<IMapperPath> Paths => _item.IsError ? NoPaths : [_item.Path];

    /// <inheritdoc />
    public IReadOnlyList<IMapperPath> ErrorPaths => _item.IsError ? [_item.Path] : NoPaths;

    /// <inheritdoc />
    public IReadOnlyList<string?> Errors => _item.IsError ? [_item.Path + " was null"] : [];

    /// <inheritdoc />
    IEnumerable<IMapperItem> IMapperShape.Items => [_item];

    /// <inheritdoc />
    public override string ToString() => _item.Path.ToString();

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MapperS<T> other && _item.Equals(other._item);

    /// <inheritdoc />
    public override int GetHashCode() => _item.GetHashCode();
}

/// <summary>Non-generic shape markers, so operators can branch on single-vs-collection.</summary>
/// <remarks>
/// Java dispatches on <c>m1.getClass() == m2.getClass()</c>, which erasure makes work across
/// element types. C# generics are reified, so the same test would compare
/// <c>MapperS&lt;int?&gt;</c> with <c>MapperS&lt;decimal?&gt;</c> and take the wrong branch;
/// downcasting to <c>MapperS&lt;T&gt;</c> fails outright because class generics are invariant.
/// These markers carry the shape without the element type.
/// </remarks>
public interface IMapperShape
{
    /// <summary>Every item, error items included.</summary>
    IEnumerable<IMapperItem> Items { get; }
}

/// <summary>Marks a mapper holding at most one value.</summary>
public interface IMapperSingle : IMapperShape
{
}

/// <summary>Marks a mapper holding a list.</summary>
public interface IMapperCollection : IMapperShape
{
}
