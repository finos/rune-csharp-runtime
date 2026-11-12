namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// Type-agnostic face of a <see cref="MapperItem{T}"/>, so an item can reference its parent without
/// the parent's element type leaking into its own.
/// </summary>
/// <remarks>
/// Java carries the parent type as a second type parameter (<c>MapperItem&lt;T, P&gt;</c>) and then
/// only ever uses it to walk upwards. Collapsing it to a non-generic parent reference keeps the
/// element type where it belongs and removes the wildcard soup that comes with it.
/// </remarks>
public interface IMapperItem
{
    /// <summary>The mapped value, boxed. Null when absent or when this is an error item.</summary>
    object? Value { get; }

    /// <summary>How this value was reached.</summary>
    MapperPath Path { get; }

    /// <summary>True when the chain broke here — a null link, or a checked conversion that failed.</summary>
    bool IsError { get; }

    /// <summary>The item this one was mapped from, or null at the root or on an error item.</summary>
    IMapperItem? ParentItem { get; }

    /// <summary>
    /// Whether this value was read out of a function's own output — which in the reference is a
    /// <em>builder</em>, not a built object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference passes the output builder as the output parameter and reads straight off it, so
    /// everything reached from it is a <c>XBuilder</c>. That is observable in exactly one place: Java's
    /// generated <c>hashCode()</c> folds an unset list attribute as <c>0</c> on a built object (the field
    /// is null) and as <c>1</c> on a builder (the field is an empty <c>ArrayList</c>), so a builder and a
    /// structurally identical built object never land in the same <c>HashSet</c> bucket — while
    /// <c>equals</c> considers them equal, because <c>ListEquals.listEquals</c> treats null and empty as
    /// the same. <c>distinct</c> is a <c>HashSet</c>, so it does not deduplicate across the two.
    /// </para>
    /// <para>
    /// The C# port reads a built snapshot of the output instead — a nested <c>Builder</c> is a separate
    /// class here, so the reference's text would not compile — and this flag is what keeps the
    /// distinction the reference gets for free. It propagates down a mapper chain exactly as Java's
    /// builder-ness does: from the root the chain was created at.
    /// </para>
    /// </remarks>
    bool FromOutputBuilder { get; }
}

/// <summary>
/// One value in a mapper, with the path it was reached by.
/// Mirrors com.rosetta.model.lib.mapper.MapperItem.
/// </summary>
/// <remarks>
/// An error item is how absence propagates: mapping off a null yields an error item, and mapping
/// off an error item yields another one without invoking the function, so a chain short-circuits
/// instead of throwing. The path is still extended, which is what makes the failure message name
/// the attribute that was missing.
/// </remarks>
public sealed class MapperItem<T> : IMapperItem, IComparable<MapperItem<T>>
{
    internal MapperItem(T mappedObject, MapperPath path, bool error, IMapperItem? parentItem,
        bool fromOutputBuilder = false)
    {
        MappedObject = mappedObject;
        Path = path;
        IsError = error;
        ParentItem = parentItem;
        FromOutputBuilder = fromOutputBuilder;
    }

    /// <summary>The mapped value.</summary>
    public T MappedObject { get; }

    /// <inheritdoc />
    public MapperPath Path { get; }

    /// <inheritdoc />
    public bool IsError { get; }

    /// <inheritdoc />
    public IMapperItem? ParentItem { get; }

    /// <inheritdoc />
    public bool FromOutputBuilder { get; }

    /// <inheritdoc />
    object? IMapperItem.Value => MappedObject;

    /// <summary>An item holding nothing, marking a broken chain at <paramref name="path"/>.</summary>
    internal static MapperItem<TChild> Error<TChild>(MapperPath path) =>
        new(default!, path, error: true, parentItem: null);

    /// <summary>
    /// Maps one item to one child. A null result is an error item, so the next link short-circuits.
    /// </summary>
    internal static MapperItem<TChild> Map<TParent, TChild>(
        MapperItem<TParent> parent, string name, Func<TParent, TChild> mappingFunc)
    {
        if (parent.IsError)
        {
            return Error<TChild>(parent.Path);
        }
        TChild child = mappingFunc(parent.MappedObject);
        MapperPath path = parent.Path.ToBuilder().AddFunctionName(name);
        return new MapperItem<TChild>(child, path, child == null, parent, parent.FromOutputBuilder);
    }

    /// <summary>
    /// Maps one item to one child, treating a chosen exception as absence. Backs the
    /// <c>to-string</c>/<c>to-number</c>/<c>to-enum</c> conversions, where a malformed value is
    /// empty rather than fatal. Any other exception still propagates.
    /// </summary>
    internal static MapperItem<TChild> CheckedMap<TParent, TChild>(
        MapperItem<TParent> parent, string name, Func<TParent, TChild> mappingFunc, Type errorType)
    {
        if (parent.IsError)
        {
            return Error<TChild>(parent.Path);
        }
        TChild child;
        try
        {
            child = mappingFunc(parent.MappedObject);
        }
        catch (Exception e) when (errorType.IsInstanceOfType(e))
        {
            child = default!;
        }
        MapperPath path = parent.Path.ToBuilder().AddFunctionName(name);
        return new MapperItem<TChild>(child, path, child == null, parent, parent.FromOutputBuilder);
    }

    /// <summary>
    /// Maps one item to many children, each indexed in its path.
    /// </summary>
    /// <remarks>
    /// A null or empty result collapses to a SINGLE error item at the un-indexed path, not to zero
    /// items — that is what gives an absent list one error to report rather than silently nothing.
    /// </remarks>
    internal static List<MapperItem<TChild>> MapMulti<TParent, TChild>(
        MapperItem<TParent> parent, string name, Func<TParent, IReadOnlyList<TChild>?> mappingFunc)
    {
        if (parent.IsError)
        {
            return [Error<TChild>(parent.Path)];
        }
        IReadOnlyList<TChild>? children = mappingFunc(parent.MappedObject);
        if (children == null || children.Count == 0)
        {
            return [Error<TChild>(parent.Path.ToBuilder().AddFunctionName(name))];
        }
        var items = new List<MapperItem<TChild>>(children.Count);
        for (int j = 0; j < children.Count; j++)
        {
            TChild child = children[j];
            MapperPath path = parent.Path.ToBuilder().AddListFunctionName(name, j);
            items.Add(new MapperItem<TChild>(child, path, child == null, parent, parent.FromOutputBuilder));
        }
        return items;
    }

    /// <summary>Reinterprets the element type, keeping path, error state and parent.</summary>
    internal MapperItem<TOther> Cast<TOther>() =>
        new((TOther)(object)MappedObject!, Path, IsError, ParentItem, FromOutputBuilder);

    /// <inheritdoc />
    public int CompareTo(MapperItem<T>? other) => other == null ? 1 : Path.CompareTo(other.Path);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is MapperItem<T> other && Equals(MappedObject, other.MappedObject);

    /// <inheritdoc />
    public override int GetHashCode() => MappedObject?.GetHashCode() ?? 0;

    /// <inheritdoc />
    public override string ToString() => $"MapperItem [mappedObject={MappedObject}]";
}
