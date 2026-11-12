using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Per-scope storage for the two post-processing steps that treat scoped keys as local rather than
/// document-wide — Java's <c>ScopeReferenceHelper</c>.
/// </summary>
/// <remarks>
/// <para>
/// A scoped key is only unique inside the region its scope type opens, so two trade states in one
/// workflow can each carry their own <c>quantity-1</c>. Both reference resolution and temporary-key
/// renumbering therefore need the same bucketing, and they share it here rather than each carrying a copy:
/// the prefix match and the first-match rule below are subtle enough that two implementations would
/// eventually disagree, and a disagreement would show up as references resolving to the wrong object with
/// no error anywhere.
/// </para>
/// </remarks>
/// <typeparam name="T">What each scope accumulates.</typeparam>
internal sealed class ScopeBuckets<T>
    where T : new()
{
    /// <summary>Where anything outside every opened scope goes.</summary>
    private static readonly RosettaPath EmptyScope = RosettaPath.ValueOf("emptyScope");

    private readonly Dictionary<RosettaPath, T> _buckets = new();

    /// <summary>Opens a scope rooted at <paramref name="path"/>, if one is not open there already.</summary>
    internal void Open(RosettaPath path) => _buckets.TryAdd(path, new T());

    /// <summary>
    /// The bucket <paramref name="path"/> belongs to, created on demand.
    /// </summary>
    /// <remarks>
    /// First match, deliberately — not longest prefix, and not innermost enclosing scope. Java iterates an
    /// unordered key set and takes the first hit, which is well defined only because scopes do not nest in
    /// CDM. An unmatched path gets the document-wide bucket rather than a null, so a caller never has to
    /// handle "no scope".
    /// </remarks>
    internal T For(RosettaPath path)
    {
        foreach (KeyValuePair<RosettaPath, T> bucket in _buckets)
        {
            if (FullStartMatches(bucket.Key, path))
            {
                return bucket.Value;
            }
        }

        if (!_buckets.TryGetValue(EmptyScope, out T? empty))
        {
            empty = new T();
            _buckets[EmptyScope] = empty;
        }

        return empty;
    }

    /// <summary>
    /// Whether <paramref name="prefix"/> is a leading run of <paramref name="path"/>, comparing name and
    /// index element by element with an absent index equal to index 0.
    /// </summary>
    /// <remarks>
    /// Written out rather than delegated to <c>RosettaPath.StartsWith</c>, which additionally compares the
    /// element URI and attributes — neither of which Java's scope path has a counterpart for. Neither
    /// difference is reachable with walker-built paths; spelling it out keeps it that way.
    /// </remarks>
    private static bool FullStartMatches(RosettaPath prefix, RosettaPath path)
    {
        List<RosettaPath.Element> mine = prefix.AllElements();
        List<RosettaPath.Element> theirs = path.AllElements();
        if (mine.Count == 0 || mine.Count > theirs.Count)
        {
            return false;
        }

        for (int i = 0; i < mine.Count; i++)
        {
            if (!string.Equals(mine[i].PathName, theirs[i].PathName, StringComparison.Ordinal)
                || (mine[i].Index ?? 0) != (theirs[i].Index ?? 0))
            {
                return false;
            }
        }

        return true;
    }
}
