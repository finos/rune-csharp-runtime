namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// A mapper over a list of lists, produced by <see cref="MapperC{T}.MapItemToList"/>.
/// Mirrors com.rosetta.model.lib.mapper.MapperListOfLists.
/// </summary>
/// <remarks>
/// Java wraps each inner list in a MapperListItem carrying a path, an error flag and a parent, but
/// exposes no accessor for any of them, so none can be read. Paths are therefore discarded here.
/// Per-value builder provenance is retained because Java carries that in the object itself and
/// <c>distinct</c> can observe it after a later flatten.
/// </remarks>
public sealed class MapperListOfLists<T>
{
    private readonly List<IReadOnlyList<(T Value, bool FromOutputBuilder)>> _lists;

    private MapperListOfLists(List<IReadOnlyList<(T Value, bool FromOutputBuilder)>> lists)
    {
        _lists = lists;
    }

    /// <summary>Wraps the lists, skipping any that are null.</summary>
    public static MapperListOfLists<T> Of(IReadOnlyList<IReadOnlyList<T>>? lists)
    {
        var kept = new List<IReadOnlyList<(T Value, bool FromOutputBuilder)>>();
        if (lists != null)
        {
            foreach (IReadOnlyList<T> list in lists)
            {
                if (list != null)
                {
                    kept.Add(list.Select(value => (value, false)).ToList());
                }
            }
        }
        return new MapperListOfLists<T>(kept);
    }

    internal static MapperListOfLists<T> OfWithBuilderFlags(
        IEnumerable<IReadOnlyList<(T Value, bool FromOutputBuilder)>> lists) =>
        new(lists.Select(list => (IReadOnlyList<(T, bool)>)[.. list]).ToList());

    /// <summary>Keeps the inner lists the predicate holds for; a null result drops the list.</summary>
    public MapperListOfLists<T> FilterListNullSafe(Func<MapperC<T>, bool?> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var kept = new List<IReadOnlyList<(T Value, bool FromOutputBuilder)>>();
        foreach (IReadOnlyList<(T Value, bool FromOutputBuilder)> list in _lists)
        {
            if (predicate(MapperC<T>.OfWithBuilderFlags(list)) == true)
            {
                kept.Add(list);
            }
        }
        return new MapperListOfLists<T>(kept);
    }

    /// <summary>Maps each inner list to a single value.</summary>
    public MapperC<TF> MapListToItem<TF>(Func<MapperC<T>, MapperS<TF>> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var values = new List<(TF Value, bool FromOutputBuilder)>();
        foreach (IReadOnlyList<(T Value, bool FromOutputBuilder)> list in _lists)
        {
            MapperS<TF> mapped = mappingFunc(MapperC<T>.OfWithBuilderFlags(list));
            values.Add((mapped.Get(), mapped.Item.FromOutputBuilder));
        }
        return MapperC<TF>.OfWithBuilderFlags(values);
    }

    /// <summary>Maps each inner list to another list.</summary>
    public MapperListOfLists<TF> MapListToList<TF>(Func<MapperC<T>, MapperC<TF>> mappingFunc)
    {
        ArgumentNullException.ThrowIfNull(mappingFunc);
        var lists = new List<IReadOnlyList<(TF Value, bool FromOutputBuilder)>>();
        foreach (IReadOnlyList<(T Value, bool FromOutputBuilder)> list in _lists)
        {
            lists.Add(mappingFunc(MapperC<T>.OfWithBuilderFlags(list)).ValuesWithBuilderFlags());
        }
        return MapperListOfLists<TF>.OfWithBuilderFlags(lists);
    }

    /// <summary>Concatenates the inner lists into one.</summary>
    public MapperC<T> FlattenList()
    {
        var values = new List<(T Value, bool FromOutputBuilder)>();
        foreach (IReadOnlyList<(T Value, bool FromOutputBuilder)> list in _lists)
        {
            values.AddRange(list);
        }
        return MapperC<T>.OfWithBuilderFlags(values);
    }

    /// <summary>The inner lists.</summary>
    public IReadOnlyList<IReadOnlyList<T>> GetMulti() =>
        _lists.Select(list => (IReadOnlyList<T>)[.. list.Select(item => item.Value)]).ToList();
}
