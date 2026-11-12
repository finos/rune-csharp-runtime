using System.Runtime.CompilerServices;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Derived facts about one content model that the reader needs on every object of that type: the
/// element names it mentions, whether it has an <c>ANY</c> node, the canonical document-order
/// position of each element name, and which names route to exactly one path. Computed once per
/// content-model instance.
/// </summary>
internal sealed class XmlContentModelIndex
{
    private static readonly ConditionalWeakTable<XmlContentModelNode, XmlContentModelIndex> Cache = new();

    private XmlContentModelIndex(XmlContentModelNode root)
    {
        var routedNames = new HashSet<string>(StringComparer.Ordinal);
        var canonicalPosition = new Dictionary<string, int>(StringComparer.Ordinal);
        var pathsByName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        bool hasAny = false;
        int counter = 0;

        void Walk(XmlContentModelNode node)
        {
            if (node.NodeType == XmlContentModelNodeType.Any)
            {
                hasAny = true;
            }
            if (node.NodeType is XmlContentModelNodeType.Element or XmlContentModelNodeType.Any)
            {
                if (node.XmlName != null)
                {
                    if (node.NodeType == XmlContentModelNodeType.Element)
                    {
                        routedNames.Add(node.XmlName);
                    }
                    if (!canonicalPosition.ContainsKey(node.XmlName))
                    {
                        canonicalPosition[node.XmlName] = counter;
                    }
                    if (node.Path != null)
                    {
                        if (!pathsByName.TryGetValue(node.XmlName, out HashSet<string>? paths))
                        {
                            paths = new HashSet<string>(StringComparer.Ordinal);
                            pathsByName[node.XmlName] = paths;
                        }
                        paths.Add(string.Join('/', node.Path));
                    }
                }
                counter++;
            }
            foreach (XmlContentModelNode child in node.ChildrenOrEmpty)
            {
                Walk(child);
            }
        }

        Walk(root);

        RoutedNames = routedNames;
        HasAny = hasAny;
        CanonicalPosition = canonicalPosition;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, HashSet<string>> entry in pathsByName)
        {
            if (entry.Value.Count == 1)
            {
                unique.Add(entry.Key);
            }
        }
        UniquelyRoutableNames = unique;
    }

    internal IReadOnlySet<string> RoutedNames { get; }

    internal bool HasAny { get; }

    internal IReadOnlyDictionary<string, int> CanonicalPosition { get; }

    internal IReadOnlySet<string> UniquelyRoutableNames { get; }

    internal static XmlContentModelIndex For(XmlContentModelNode root) =>
        Cache.GetValue(root, node => new XmlContentModelIndex(node));
}

/// <summary>
/// Applies the strict matcher and, when it fails, the same three-step lenient recovery the Java
/// reference performs: stably reorder into canonical content-model order and re-match; otherwise
/// keep only uniquely-routable elements and re-match; otherwise skip content-model routing
/// altogether. Nothing here throws — a document that cannot be routed is still read best-effort.
/// </summary>
internal static class XmlContentModelRouter
{
    internal static XmlRoutingResult Route(
        XmlContentModelNode model,
        XmlContentModelIndex index,
        IReadOnlyList<XmlRoutingInput> inputs,
        XmlReadContext context,
        string owner)
    {
        XmlRoutingResult strict = XmlContentModelMatcher.Route(model, inputs);
        if (strict.Status == XmlRoutingStatus.Success)
        {
            return strict;
        }

        XmlRoutingResult? reordered = RouteReordered(model, index, inputs, inputs);
        if (reordered != null)
        {
            context.NoteReordered(owner, Describe(inputs));
            return reordered;
        }

        var keep = new List<XmlRoutingInput>();
        var dropped = new List<XmlRoutingInput>();
        foreach (XmlRoutingInput input in inputs)
        {
            if (index.UniquelyRoutableNames.Contains(input.XmlName))
            {
                keep.Add(input);
            }
            else
            {
                dropped.Add(input);
            }
        }
        if (dropped.Count > 0 && keep.Count > 0)
        {
            XmlRoutingResult? partial = RouteReordered(model, index, keep, inputs);
            if (partial != null)
            {
                context.NoteUnrouted(owner, dropped.Count, Describe(dropped));
                return partial;
            }
        }

        context.NoteAbandonedRouting(owner, Describe(inputs));
        return XmlRoutingResult.Success(Array.Empty<XmlRoutingAssignment>());
    }

    /// <summary>
    /// Stably reorders <paramref name="subset"/> into canonical content-model order, runs the strict
    /// matcher, and remaps a success back to indices in <paramref name="originalInputs"/>. Returns
    /// null when the reordered subset still does not match uniquely.
    /// </summary>
    private static XmlRoutingResult? RouteReordered(
        XmlContentModelNode model,
        XmlContentModelIndex index,
        IReadOnlyList<XmlRoutingInput> subset,
        IReadOnlyList<XmlRoutingInput> originalInputs)
    {
        List<XmlRoutingInput> sorted = subset
            .OrderBy(input => index.CanonicalPosition.TryGetValue(input.XmlName, out int position) ? position : int.MaxValue)
            .ToList();

        XmlRoutingResult result = XmlContentModelMatcher.Route(model, sorted);
        if (result.Status != XmlRoutingStatus.Success)
        {
            return null;
        }

        var originalIndex = new Dictionary<XmlRoutingInput, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < originalInputs.Count; i++)
        {
            originalIndex[originalInputs[i]] = i;
        }
        var remapped = new List<XmlRoutingAssignment>(result.Assignments.Count);
        foreach (XmlRoutingAssignment assignment in result.Assignments)
        {
            XmlRoutingInput input = sorted[assignment.InputIndex];
            remapped.Add(new XmlRoutingAssignment(originalIndex[input], assignment.Path, assignment.Occurrence));
        }
        return XmlRoutingResult.Success(remapped);
    }

    private static string Describe(IReadOnlyList<XmlRoutingInput> inputs) =>
        string.Join(", ", inputs.Select(input => input.XmlName));

    private sealed class ReferenceEqualityComparer : IEqualityComparer<XmlRoutingInput>
    {
        internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public bool Equals(XmlRoutingInput? x, XmlRoutingInput? y) => ReferenceEquals(x, y);

        public int GetHashCode(XmlRoutingInput obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
