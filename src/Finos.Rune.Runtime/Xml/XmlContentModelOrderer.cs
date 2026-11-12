namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Computes the order in which a Rune type's child elements must be written so that the resulting
/// XML follows the configured XSD content model — the C# port of rune-common's
/// <c>XMLContentModelOrderer</c>, and the write-time dual of <see cref="XmlContentModelMatcher"/>.
/// </summary>
/// <remarks>
/// <para>Ordering works at the granularity of Rune properties (the first segment of each
/// content-model element path). That is sound because every property maps to a single contiguous
/// block of XML elements: a direct element is one block, and a VIRTUAL group's leaves are a
/// contiguous subtree. Working per property is also what lets the orderer pick a data-dependent
/// branch order (<c>x,a,b</c> versus <c>y,b,a</c>) that a static property reordering could never
/// express.</para>
/// <para>When the present set cannot be consumed by the model, <see cref="Order"/> returns null and
/// the writer keeps its default order rather than throwing. The result is therefore never worse
/// than not consulting the content model at all.</para>
/// </remarks>
public sealed class XmlContentModelOrderer
{
    /// <summary>Safety bound on the search; exceeding it falls back to the default order.</summary>
    private const int MaxCandidates = 256;

    private readonly Node? _root;

    public XmlContentModelOrderer(XmlContentModelNode contentModel)
    {
        ArgumentNullException.ThrowIfNull(contentModel);
        var properties = new List<string>();
        CollectLeafProperties(contentModel, properties);
        ContentModelProperties = properties;
        _root = Reduce(contentModel);
    }

    /// <summary>
    /// The Rune property names participating in this content model, in first-appearance order.
    /// Only these are reordered; every other property keeps its position.
    /// </summary>
    public IReadOnlyList<string> ContentModelProperties { get; }

    /// <summary>
    /// Computes a valid write order for the supplied present properties, or null when no ordering
    /// consuming exactly that set could be derived.
    /// </summary>
    public IReadOnlyList<string>? Order(IReadOnlyCollection<string> presentProperties)
    {
        ArgumentNullException.ThrowIfNull(presentProperties);
        if (presentProperties.Count == 0)
        {
            return Array.Empty<string>();
        }
        if (_root == null)
        {
            return null;
        }
        var present = new HashSet<string>(presentProperties, StringComparer.Ordinal);
        var candidates = new List<List<string>>();
        Enumerate(_root, present, candidates);
        foreach (List<string> candidate in candidates)
        {
            if (candidate.Count == present.Count && present.SetEquals(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static Node? Reduce(XmlContentModelNode model)
    {
        var properties = new List<string>();
        CollectLeafProperties(model, properties);
        bool required = model.MinOccursOrDefault >= 1;
        if (properties.Count <= 1)
        {
            return properties.Count == 0 ? null : new Node(properties[0], required);
        }
        var children = new List<Node>();
        foreach (XmlContentModelNode child in model.ChildrenOrEmpty)
        {
            Node? reduced = Reduce(child);
            if (reduced != null)
            {
                children.Add(reduced);
            }
        }
        return new Node(model.NodeType, children, required);
    }

    private static void CollectLeafProperties(XmlContentModelNode node, List<string> accumulator)
    {
        if (node.NodeType is XmlContentModelNodeType.Element or XmlContentModelNodeType.Any)
        {
            IReadOnlyList<string> path = node.PathOrEmpty;
            if (path.Count > 0 && !accumulator.Contains(path[0], StringComparer.Ordinal))
            {
                accumulator.Add(path[0]);
            }
        }
        foreach (XmlContentModelNode child in node.ChildrenOrEmpty)
        {
            CollectLeafProperties(child, accumulator);
        }
    }

    private void Enumerate(Node node, HashSet<string> present, List<List<string>> output)
    {
        if (node.Property != null)
        {
            if (present.Contains(node.Property))
            {
                output.Add(new List<string> { node.Property });
            }
            else if (!node.Required)
            {
                output.Add(new List<string>());
            }
            return;
        }
        if (node.NodeType == XmlContentModelNodeType.Choice)
        {
            EnumerateChoice(node, present, output);
        }
        else
        {
            EnumerateSequence(node, present, output);
        }
    }

    private void EnumerateChoice(Node node, HashSet<string> present, List<List<string>> output)
    {
        foreach (Node child in node.Children)
        {
            Enumerate(child, present, output);
            if (output.Count > MaxCandidates)
            {
                return;
            }
        }
        if (output.Count == 0 && !node.Required)
        {
            output.Add(new List<string>());
        }
    }

    private void EnumerateSequence(Node node, HashSet<string> present, List<List<string>> output)
    {
        var accumulated = new List<List<string>> { new List<string>() };
        foreach (Node child in node.Children)
        {
            var childOptions = new List<List<string>>();
            Enumerate(child, present, childOptions);
            if (childOptions.Count == 0)
            {
                return;
            }
            var next = new List<List<string>>();
            foreach (List<string> prefix in accumulated)
            {
                foreach (List<string> suffix in childOptions)
                {
                    if (!Disjoint(prefix, suffix))
                    {
                        continue;
                    }
                    var combined = new List<string>(prefix);
                    combined.AddRange(suffix);
                    next.Add(combined);
                    if (next.Count > MaxCandidates)
                    {
                        output.AddRange(next);
                        return;
                    }
                }
            }
            accumulated = next;
        }
        output.AddRange(accumulated);
    }

    private static bool Disjoint(List<string> left, List<string> right)
    {
        foreach (string value in right)
        {
            if (left.Contains(value, StringComparer.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// A reduced content-model node: either a single property or a compound group.
    /// </summary>
    private sealed class Node
    {
        internal Node(string property, bool required)
        {
            Property = property;
            NodeType = XmlContentModelNodeType.Element;
            Children = Array.Empty<Node>();
            Required = required;
        }

        internal Node(XmlContentModelNodeType nodeType, IReadOnlyList<Node> children, bool required)
        {
            Property = null;
            NodeType = nodeType;
            Children = children;
            Required = required;
        }

        internal string? Property { get; }

        internal XmlContentModelNodeType NodeType { get; }

        internal IReadOnlyList<Node> Children { get; }

        internal bool Required { get; }
    }
}
