using System.Globalization;
using Finos.Rune.Runtime.Expression;

namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// A mapper over a list of values. Mirrors com.rosetta.model.lib.mapper.MapperC.
/// </summary>
/// <remarks>
/// <para>Two rules run through the whole class and are easy to lose. Attribute navigation
/// (<see cref="Map"/>, <see cref="MapC"/>) carries error items through so a broken chain keeps
/// reporting; every other operator starts from the non-error items. And roughly half the list
/// operators rebuild from raw values, discarding provenance and re-rooting each item — that is
/// Java's behaviour and messages depend on it, so it must not be "improved".</para>
/// </remarks>
public sealed class MapperC<T> : IMapper<T>, IMapperCollection
{
    private readonly List<MapperItem<T>> _items;

    internal MapperC(List<MapperItem<T>> items)
    {
        _items = items;
    }

    /// <summary>
    /// An absent list, which is simply empty — unlike <see cref="MapperS{T}.OfNull"/>, which is one
    /// error item at path <c>Null</c>. The asymmetry is observable through
    /// <see cref="ErrorPaths"/>.
    /// </summary>
    public static MapperC<T> OfNull() => new([]);

    /// <summary>
    /// Roots a mapper at a list of values. Each element is rooted independently at its own Java
    /// simple name; a null element becomes an error item at path <c>Null</c>.
    /// </summary>
    public static MapperC<T> Of(IReadOnlyList<T>? values)
    {
        var items = new List<MapperItem<T>>();
        if (values != null)
        {
            foreach (T value in values)
            {
                items.Add(value is null
                    ? new MapperItem<T>(value, MapperPath.Builder().AddNull(), true, null)
                    : new MapperItem<T>(value, MapperPath.Builder().AddFunctionName(JavaTypeNames.Of(value)), false, null));
            }
        }
        return new MapperC<T>(items);
    }

    /// <summary>
    /// Re-roots raw values while retaining whether each came from the enclosing function's output builder.
    /// Paths and parents are deliberately discarded, as for <see cref="Of(IReadOnlyList{T}?)"/>.
    /// </summary>
    internal static MapperC<T> OfWithBuilderFlags(IEnumerable<(T Value, bool FromOutputBuilder)> values)
    {
        var items = new List<MapperItem<T>>();
        foreach ((T value, bool fromOutputBuilder) in values)
        {
            items.Add(value is null
                ? new MapperItem<T>(
                    value, MapperPath.Builder().AddNull(), true, null, fromOutputBuilder)
                : new MapperItem<T>(
                    value,
                    MapperPath.Builder().AddFunctionName(JavaTypeNames.Of(value)),
                    false,
                    null,
                    fromOutputBuilder));
        }
        return new MapperC<T>(items);
    }

    /// <summary>
    /// Concatenates the items of several mappers, preserving their provenance.
    /// </summary>
    /// <remarks>
    /// Java overloads <c>of</c> for this and distinguishes it from the list form by varargs. C#
    /// cannot: <c>Of([])</c> would be ambiguous, so this one is named.
    /// </remarks>
    public static MapperC<T> OfMappers(params IMapper<T>[] mappers)
    {
        var items = new List<MapperItem<T>>();
        foreach (IMapper<T> mapper in mappers)
        {
            if (mapper is IMapperShape shape)
            {
                foreach (IMapperItem item in shape.Items)
                {
                    items.Add((MapperItem<T>)item);
                }
            }
        }
        return new MapperC<T>(items);
    }

    /// <summary>
    /// Roots a mapper at a list read out of the enclosing function's own output — see
    /// <see cref="IMapperItem.FromOutputBuilder"/>.
    /// </summary>
    public static MapperC<T> OfOutputBuilder(IReadOnlyList<T>? values)
    {
        var items = new List<MapperItem<T>>();
        if (values != null)
        {
            foreach (T value in values)
            {
                items.Add(value is null
                    ? new MapperItem<T>(value, MapperPath.Builder().AddNull(), true, null, true)
                    : new MapperItem<T>(
                        value, MapperPath.Builder().AddFunctionName(JavaTypeNames.Of(value)), false, null, true));
            }
        }
        return new MapperC<T>(items);
    }

    /// <summary>Maps every item, error items included, so a broken chain keeps reporting.</summary>
    public MapperC<TF> Map<TF>(string name, Func<T, TF> mappingFunc) =>
        new(_items.ConvertAll(i => MapperItem<T>.Map(i, name, mappingFunc)));

    /// <summary>Maps every item to a list and flattens, error items included.</summary>
    public MapperC<TF> MapC<TF>(string name, Func<T, IReadOnlyList<TF>?> mappingFunc)
    {
        var items = new List<MapperItem<TF>>();
        foreach (MapperItem<T> item in _items)
        {
            items.AddRange(MapperItem<T>.MapMulti(item, name, mappingFunc));
        }
        return new MapperC<TF>(items);
    }

    /// <summary>Keeps the items the predicate holds for; a null result drops the item.</summary>
    public MapperC<T> FilterItemNullSafe(Func<MapperS<T>, bool?> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var kept = new List<MapperItem<T>>();
        foreach (MapperItem<T> item in NonErrorItems())
        {
            if (predicate(new MapperS<T>(item)) == true)
            {
                kept.Add(item);
            }
        }
        return new MapperC<T>(kept);
    }

    /// <summary>
    /// Maps each item through a mapper-to-mapper function. Path provenance is discarded: the result is
    /// re-rooted from raw values, so an absent result becomes an error item at <c>Null</c>. Builder provenance
    /// remains because Java carries it in the returned object itself, not in its mapper path.
    /// </summary>
    public MapperC<TF> MapItem<TF>(Func<MapperS<T>, MapperS<TF>> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var values = new List<(TF Value, bool FromOutputBuilder)>();
        foreach (MapperItem<T> item in NonErrorItems())
        {
            MapperS<TF> mapped = mappingFunc(new MapperS<T>(item));
            values.Add((mapped.Get(), mapped.Item.FromOutputBuilder));
        }
        return MapperC<TF>.OfWithBuilderFlags(values);
    }

    /// <summary>Maps each item to a list, producing a list of lists.</summary>
    public MapperListOfLists<TF> MapItemToList<TF>(Func<MapperS<T>, MapperC<TF>> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var lists = new List<IReadOnlyList<(TF Value, bool FromOutputBuilder)>>();
        foreach (MapperItem<T> item in NonErrorItems())
        {
            lists.Add(mappingFunc(new MapperS<T>(item)).ValuesWithBuilderFlags());
        }
        return MapperListOfLists<TF>.OfWithBuilderFlags(lists);
    }

    /// <summary>
    /// Folds the items. The seed is an identity placeholder that the fold skips, so a single-item
    /// list reduces to that item without the function ever running.
    /// </summary>
    public MapperS<TF> Reduce<TF>(Func<MapperS<TF>, MapperS<TF>, MapperS<TF>> reduceFunc) =>
        Reduce(MapperS<TF>.Identity(), reduceFunc);

    /// <summary>Folds the items from an explicit seed.</summary>
    public MapperS<TF> Reduce<TF>(MapperS<TF> initial, Func<MapperS<TF>, MapperS<TF>, MapperS<TF>> reduceFunc)
    {
        ArgumentNullException.ThrowIfNull(reduceFunc);
        MapperS<TF> accumulated = initial;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            var next = new MapperS<TF>(item.Cast<TF>());
            if (accumulated.IsIdentity)
            {
                accumulated = next;
            }
            else if (next.IsIdentity)
            {
                continue;
            }
            else
            {
                accumulated = reduceFunc(accumulated, next);
            }
        }
        return accumulated;
    }

    /// <summary>Sum of an <c>int</c> list; an empty list sums to 0, not to absent.</summary>
    public MapperS<int?> SumInt()
    {
        int total = 0;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            total += Convert.ToInt32(item.MappedObject, CultureInfo.InvariantCulture);
        }
        return MapperS<int?>.Of(total);
    }

    /// <summary>Sum of a <c>long</c> list; an empty list sums to 0.</summary>
    public MapperS<long?> SumLong()
    {
        long total = 0;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            total += Convert.ToInt64(item.MappedObject, CultureInfo.InvariantCulture);
        }
        return MapperS<long?>.Of(total);
    }

    /// <summary>Sum of a <c>number</c> list; an empty list sums to 0.</summary>
    public MapperS<decimal?> SumDecimal()
    {
        decimal total = 0m;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            total += (decimal)(object)item.MappedObject!;
        }
        return MapperS<decimal?>.Of(total);
    }

    /// <summary>Concatenates the items with a delimiter; an absent delimiter joins with nothing.</summary>
    public MapperS<string?> Join(MapperS<string?> delimiter)
    {
        ArgumentNullException.ThrowIfNull(delimiter);
        string separator = delimiter.Get() ?? string.Empty;
        var parts = new List<string>();
        foreach (MapperItem<T> item in NonErrorItems())
        {
            parts.Add((string)(object)item.MappedObject!);
        }
        return MapperS<string?>.Of(string.Join(separator, parts));
    }

    /// <summary>The item whose key is smallest, keeping the first on a tie.</summary>
    public MapperS<T> Min<TKey>(Func<MapperS<T>, MapperS<TKey>> keyGetter) =>
        Extreme(keyGetter, wantSmaller: true);

    /// <summary>The item whose key is largest, keeping the first on a tie.</summary>
    public MapperS<T> Max<TKey>(Func<MapperS<T>, MapperS<TKey>> keyGetter) =>
        Extreme(keyGetter, wantSmaller: false);

    /// <summary>The smallest item, comparing the items themselves.</summary>
    /// <remarks>
    /// The identity key getter, exactly as Java's <c>min()</c> delegates to
    /// <c>min(x -&gt; x)</c>. It therefore inherits the same asymmetry with <c>Sort()</c>: an absent
    /// item is dropped before the comparison here and ordered last there.
    /// </remarks>
    public MapperS<T> Min() => Extreme<T>(x => x, wantSmaller: true);

    /// <summary>The largest item, comparing the items themselves.</summary>
    public MapperS<T> Max() => Extreme<T>(x => x, wantSmaller: false);

    private MapperS<T> Extreme<TKey>(Func<MapperS<T>, MapperS<TKey>> keyGetter, bool wantSmaller)
    {
        ArgumentNullException.ThrowIfNull(keyGetter);
        MapperItem<T>? best = null;
        TKey? bestKey = default;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            // Unlike Sort, items with an absent key are dropped first; do not share the comparer.
            TKey key = keyGetter(new MapperS<T>(item)).Get();
            if (key is null)
            {
                continue;
            }
            if (best == null)
            {
                best = item;
                bestKey = key;
                continue;
            }
            int comparison = RuneComparer.Compare(key, bestKey);
            if (wantSmaller ? comparison < 0 : comparison > 0)
            {
                best = item;
                bestKey = key;
            }
        }
        return best == null ? MapperS<T>.OfNull() : new MapperS<T>(best);
    }

    /// <summary>Sorted by a key, absent keys last. Provenance is preserved.</summary>
    public MapperC<T> Sort<TKey>(Func<MapperS<T>, MapperS<TKey>> keyGetter)
    {
        ArgumentNullException.ThrowIfNull(keyGetter);
        List<MapperItem<T>> items = [.. NonErrorItems()];
        return new MapperC<T>([.. items
            .Select(i => (Item: i, Key: keyGetter(new MapperS<T>(i)).Get()))
            .OrderBy(pair => pair.Key, NullsLastComparer<TKey>.Instance)
            .Select(pair => pair.Item)]);
    }

    /// <summary>
    /// Sorted by natural order. Paths are discarded and values re-rooted; builder provenance is retained.
    /// </summary>
    public MapperC<T> Sort()
    {
        List<(T Value, bool FromOutputBuilder)> values =
            [.. NonErrorItems().Select(i => (i.MappedObject, i.FromOutputBuilder))];
        values.Sort((a, b) => RuneComparer.Compare(a.Value, b.Value));
        return OfWithBuilderFlags(values);
    }

    /// <summary>Items in reverse order, provenance preserved.</summary>
    public MapperC<T> Reverse()
    {
        List<MapperItem<T>> items = [.. NonErrorItems()];
        items.Reverse();
        return new MapperC<T>(items);
    }

    /// <summary>The first item, or absent when there is none.</summary>
    public MapperS<T> First()
    {
        foreach (MapperItem<T> item in NonErrorItems())
        {
            return new MapperS<T>(item);
        }
        return MapperS<T>.OfNull();
    }

    /// <summary>The last item, or absent when there is none.</summary>
    public MapperS<T> Last()
    {
        MapperItem<T>? last = null;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            last = item;
        }
        return last == null ? MapperS<T>.OfNull() : new MapperS<T>(last);
    }

    /// <summary>The item at an index, or absent when the index is missing or out of range.</summary>
    public MapperS<T> GetItem(MapperS<int?> indexGetter)
    {
        ArgumentNullException.ThrowIfNull(indexGetter);
        List<MapperItem<T>> items = [.. NonErrorItems()];
        int? index = indexGetter.Get();
        if (index.HasValue && index.Value >= 0 && index.Value < items.Count)
        {
            return new MapperS<T>(items[index.Value]);
        }
        return MapperS<T>.OfNull();
    }

    /// <summary>The list without the item at an index.</summary>
    public MapperC<T> RemoveItem(MapperS<int?> indexGetter)
    {
        ArgumentNullException.ThrowIfNull(indexGetter);
        List<MapperItem<T>> items = [.. NonErrorItems()];
        int? index = indexGetter.Get();
        if (index.HasValue && index.Value >= 0 && index.Value < items.Count)
        {
            items.RemoveAt(index.Value);
        }
        return new MapperC<T>(items);
    }

    /// <summary>
    /// The single value, or absent. Absent covers BOTH zero results and more than one — this never
    /// throws, so it is neither <c>Single()</c> nor <c>FirstOrDefault()</c>.
    /// </summary>
    public T Get()
    {
        T? found = default;
        int count = 0;
        foreach (MapperItem<T> item in NonErrorItems())
        {
            if (++count > 1)
            {
                return default!;
            }
            found = item.MappedObject;
        }
        return count == 1 ? found! : default!;
    }

    /// <summary>The single value, or <paramref name="defaultValue"/> when absent or ambiguous.</summary>
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
    public IReadOnlyList<T> GetMulti() => [.. NonErrorItems().Select(i => i.MappedObject)];

    /// <inheritdoc />
    public int ResultCount => NonErrorItems().Count();

    /// <inheritdoc />
    public IReadOnlyList<IMapperPath> Paths => [.. NonErrorItems().Select(i => (IMapperPath)i.Path)];

    /// <inheritdoc />
    public IReadOnlyList<IMapperPath> ErrorPaths => [.. ErrorItems().Select(i => (IMapperPath)i.Path)];

    /// <summary>
    /// One message per error item. Note the wording differs from <see cref="MapperS{T}.Errors"/>,
    /// which says <c>"&lt;path&gt; was null"</c>.
    /// </summary>
    public IReadOnlyList<string?> Errors =>
        [.. ErrorItems().Select(i => $"[{i.Path.FullPath}] is not set")];

    /// <inheritdoc />
    IEnumerable<IMapperItem> IMapperShape.Items => _items;

    private IEnumerable<MapperItem<T>> NonErrorItems() => _items.Where(i => !i.IsError);

    private IEnumerable<MapperItem<T>> ErrorItems() => _items.Where(i => i.IsError);

    internal IReadOnlyList<(T Value, bool FromOutputBuilder)> ValuesWithBuilderFlags() =>
        [.. NonErrorItems().Select(i => (i.MappedObject, i.FromOutputBuilder))];

    /// <inheritdoc />
    public override string ToString() => string.Join(", ", _items.Select(i => i.Path.ToString()));

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is MapperC<T> other && _items.SequenceEqual(other._items);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (MapperItem<T> item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <summary>Orders by natural order with absent keys last, as Java's nullsLast comparator.</summary>
    private sealed class NullsLastComparer<TKey> : IComparer<TKey>
    {
        internal static NullsLastComparer<TKey> Instance { get; } = new();

        public int Compare(TKey? x, TKey? y)
        {
            if (x is null)
            {
                return y is null ? 0 : 1;
            }
            return y is null ? -1 : RuneComparer.Compare(x, y);
        }
    }
}
