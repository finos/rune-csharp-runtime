namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// Read-only view of a mapped value and the path it came from.
/// Mirrors com.rosetta.model.lib.mapper.Mapper.
/// </summary>
/// <remarks>
/// <para>Only the read side lives here. The mapping API (MapperS/MapperC and their Map/Filter
/// chains) arrives with the expression compiler; the validation tier needs nothing more than this
/// because <see cref="Finos.Rune.Runtime.Expression.ComparisonResult"/> implements it.</para>
/// <para><typeparamref name="T"/> is always the nullable form of the item type
/// (<c>decimal?</c>, <c>Party?</c>) — for an unconstrained <c>T</c>, <c>default(T)</c> of a value
/// type is <c>0</c> rather than null, so a non-nullable <c>T</c> could not represent absence.</para>
/// </remarks>
public interface IMapper<out T>
{
    /// <summary>The single value, or null when absent.</summary>
    T Get();

    /// <summary>All values; empty when absent.</summary>
    IReadOnlyList<T> GetMulti();

    /// <summary>Number of values present.</summary>
    int ResultCount { get; }

    /// <summary>Provenance of the values that were found.</summary>
    IReadOnlyList<IMapperPath> Paths { get; }

    /// <summary>Provenance of the values that were expected but absent.</summary>
    IReadOnlyList<IMapperPath> ErrorPaths { get; }

    /// <summary>Accumulated error messages.</summary>
    IReadOnlyList<string?> Errors { get; }
}

/// <summary>
/// Provenance of a mapped value: the chain of accessors that produced it.
/// Mirrors com.rosetta.model.lib.mapper.Mapper.Path.
/// </summary>
public interface IMapperPath
{
    /// <summary>Rune attribute names, outermost first.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>Accessor names, outermost first.</summary>
    IReadOnlyList<string> Getters { get; }

    /// <summary>The last Rune attribute name in the chain.</summary>
    string LastName { get; }

    /// <summary>The chain rendered with <c>-&gt;</c> separators.</summary>
    string FullPath { get; }
}
