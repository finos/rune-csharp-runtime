namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Pure, recursive matcher of an XML content-model tree against an ordered list of
/// <see cref="XmlRoutingInput"/> items — the C# port of rune-common's
/// <c>XMLContentModelMatcher</c>. It decides where each ambiguous XML child element is routed.
/// </summary>
/// <remarks>
/// <para>Supports <c>ELEMENT</c>, <c>SEQUENCE</c>, <c>CHOICE</c>, <c>ALL</c> and a minimal form of
/// <c>ANY</c>, honouring <c>minOccurs</c>/<c>maxOccurs</c> throughout.</para>
/// <para>Top-level tie-breaking lives in <see cref="Route"/>: only matches that consume the whole
/// input survive, and the result is a success only when exactly one distinct assignment does.</para>
/// </remarks>
public static class XmlContentModelMatcher
{
    /// <summary>
    /// Matches <paramref name="root"/> against <paramref name="inputs"/> and returns the unique
    /// routing assignment that consumes the entire input.
    /// </summary>
    public static XmlRoutingResult Route(XmlContentModelNode root, IReadOnlyList<XmlRoutingInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(inputs);

        List<MatchResult> all = Match(root, inputs, 0, OccurrenceStack.Empty);
        var complete = new List<MatchResult>();
        foreach (MatchResult result in all)
        {
            if (result.NextIndex == inputs.Count)
            {
                complete.Add(result);
            }
        }
        if (complete.Count == 0)
        {
            return XmlRoutingResult.NoMatch();
        }

        var unique = new List<MatchResult>();
        foreach (MatchResult candidate in complete)
        {
            bool duplicate = false;
            foreach (MatchResult existing in unique)
            {
                if (AssignmentsEqual(existing.Assignments, candidate.Assignments))
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
            {
                unique.Add(candidate);
            }
        }
        if (unique.Count > 1)
        {
            var candidates = new List<IReadOnlyList<IReadOnlyList<string>>>(unique.Count);
            foreach (MatchResult result in unique)
            {
                var paths = new List<IReadOnlyList<string>>(result.Assignments.Count);
                foreach (XmlRoutingAssignment assignment in result.Assignments)
                {
                    paths.Add(assignment.Path);
                }
                candidates.Add(paths);
            }
            return XmlRoutingResult.Ambiguous(candidates);
        }
        return XmlRoutingResult.Success(unique[0].Assignments);
    }

    private static bool AssignmentsEqual(
        IReadOnlyList<XmlRoutingAssignment> left, IReadOnlyList<XmlRoutingAssignment> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        for (int i = 0; i < left.Count; i++)
        {
            XmlRoutingAssignment a = left[i];
            XmlRoutingAssignment b = right[i];
            if (a.InputIndex != b.InputIndex
                || !a.Path.SequenceEqual(b.Path, StringComparer.Ordinal)
                || !a.Occurrence.Equals(b.Occurrence))
            {
                return false;
            }
        }
        return true;
    }

    private static List<MatchResult> Match(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack)
    {
        int remaining = inputs.Count - inputIndex;
        int min = node.MinOccursOrDefault;
        int max = node.MaxOccursOrDefault(remaining);
        var results = new List<MatchResult>();
        if (max < min)
        {
            return results;
        }
        Repeat(node, inputs, inputIndex, 0, min, max, Array.Empty<XmlRoutingAssignment>(), stack, results);
        return results;
    }

    private static void Repeat(
        XmlContentModelNode node,
        IReadOnlyList<XmlRoutingInput> inputs,
        int inputIndex,
        int count,
        int min,
        int max,
        IReadOnlyList<XmlRoutingAssignment> accumulated,
        OccurrenceStack stack,
        List<MatchResult> output)
    {
        if (count >= min)
        {
            output.Add(new MatchResult(inputIndex, accumulated));
        }
        if (count >= max)
        {
            return;
        }

        OccurrenceStack pushed = node.NodeType is XmlContentModelNodeType.Element or XmlContentModelNodeType.Any
            ? stack
            : stack.Push(node, count);

        foreach (MatchResult one in MatchOnce(node, inputs, inputIndex, pushed))
        {
            // Once min is met, a zero-width iteration would recurse forever without advancing.
            if (one.NextIndex == inputIndex && one.Assignments.Count == 0 && count >= min)
            {
                continue;
            }
            Repeat(node, inputs, one.NextIndex, count + 1, min, max, Concat(accumulated, one.Assignments), stack, output);
        }
    }

    private static List<MatchResult> MatchOnce(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack) =>
        node.NodeType switch
        {
            XmlContentModelNodeType.Element => MatchElement(node, inputs, inputIndex, stack),
            XmlContentModelNodeType.Sequence => MatchSequence(node, inputs, inputIndex, stack),
            XmlContentModelNodeType.Choice => MatchChoice(node, inputs, inputIndex, stack),
            XmlContentModelNodeType.All => MatchAll(node, inputs, inputIndex, stack),
            XmlContentModelNodeType.Any => MatchAny(node, inputs, inputIndex, stack),
            _ => throw new ArgumentException("Unsupported XML content-model node type: " + node.NodeType, nameof(node)),
        };

    private static List<MatchResult> MatchElement(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack)
    {
        var results = new List<MatchResult>();
        if (inputIndex >= inputs.Count)
        {
            return results;
        }
        XmlRoutingInput input = inputs[inputIndex];
        if (node.XmlName == null || !string.Equals(node.XmlName, input.XmlName, StringComparison.Ordinal))
        {
            return results;
        }
        if (!NamespaceMatches(node, input))
        {
            return results;
        }
        results.Add(new MatchResult(inputIndex + 1, AssignmentFor(node, inputIndex, stack)));
        return results;
    }

    private static List<MatchResult> MatchSequence(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack)
    {
        var current = new List<MatchResult>
        {
            new MatchResult(inputIndex, Array.Empty<XmlRoutingAssignment>()),
        };
        foreach (XmlContentModelNode child in node.ChildrenOrEmpty)
        {
            var next = new List<MatchResult>();
            foreach (MatchResult prefix in current)
            {
                foreach (MatchResult suffix in Match(child, inputs, prefix.NextIndex, stack))
                {
                    next.Add(new MatchResult(suffix.NextIndex, Concat(prefix.Assignments, suffix.Assignments)));
                }
            }
            current = next;
            if (current.Count == 0)
            {
                return current;
            }
        }
        return current;
    }

    private static List<MatchResult> MatchChoice(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack)
    {
        var results = new List<MatchResult>();
        foreach (XmlContentModelNode child in node.ChildrenOrEmpty)
        {
            results.AddRange(Match(child, inputs, inputIndex, stack));
        }
        return results;
    }

    private static List<MatchResult> MatchAll(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack)
    {
        IReadOnlyList<XmlContentModelNode> children = node.ChildrenOrEmpty;
        var results = new List<MatchResult>();
        if (children.Count == 0)
        {
            results.Add(new MatchResult(inputIndex, Array.Empty<XmlRoutingAssignment>()));
            return results;
        }
        int[] counts = new int[children.Count];
        AllRecurse(children, counts, inputs, inputIndex, Array.Empty<XmlRoutingAssignment>(), stack, results);
        return results;
    }

    private static void AllRecurse(
        IReadOnlyList<XmlContentModelNode> children,
        int[] counts,
        IReadOnlyList<XmlRoutingInput> inputs,
        int inputIndex,
        IReadOnlyList<XmlRoutingAssignment> accumulated,
        OccurrenceStack stack,
        List<MatchResult> output)
    {
        bool allMinSatisfied = true;
        for (int i = 0; i < children.Count; i++)
        {
            if (counts[i] < children[i].MinOccursOrDefault)
            {
                allMinSatisfied = false;
                break;
            }
        }
        if (allMinSatisfied)
        {
            output.Add(new MatchResult(inputIndex, accumulated));
        }
        if (inputIndex >= inputs.Count)
        {
            return;
        }
        for (int i = 0; i < children.Count; i++)
        {
            XmlContentModelNode child = children[i];
            int remaining = inputs.Count - inputIndex;
            if (counts[i] >= child.MaxOccursOrDefault(remaining))
            {
                continue;
            }
            foreach (MatchResult one in MatchOnce(child, inputs, inputIndex, stack))
            {
                if (one.NextIndex == inputIndex && one.Assignments.Count == 0)
                {
                    continue;
                }
                counts[i]++;
                AllRecurse(
                    children, counts, inputs, one.NextIndex, Concat(accumulated, one.Assignments), stack, output);
                counts[i]--;
            }
        }
    }

    private static List<MatchResult> MatchAny(
        XmlContentModelNode node, IReadOnlyList<XmlRoutingInput> inputs, int inputIndex, OccurrenceStack stack)
    {
        var results = new List<MatchResult>();
        if (inputIndex >= inputs.Count)
        {
            return results;
        }
        if (!NamespaceMatches(node, inputs[inputIndex]))
        {
            return results;
        }
        results.Add(new MatchResult(inputIndex + 1, AssignmentFor(node, inputIndex, stack)));
        return results;
    }

    private static IReadOnlyList<XmlRoutingAssignment> AssignmentFor(
        XmlContentModelNode node, int inputIndex, OccurrenceStack stack) =>
        node.Path == null
            ? Array.Empty<XmlRoutingAssignment>()
            : new[] { new XmlRoutingAssignment(inputIndex, node.Path, stack.CurrentOccurrence()) };

    private static bool NamespaceMatches(XmlContentModelNode node, XmlRoutingInput input)
    {
        string? required = node.Namespace;
        if (required == null)
        {
            return true;
        }
        if (input.Namespace.IsUnknown)
        {
            // Replayed content has no namespace context, so an unknown namespace matches by local name.
            return true;
        }
        return string.Equals(required, input.Namespace.Value, StringComparison.Ordinal);
    }

    private static IReadOnlyList<T> Concat<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
    {
        if (left.Count == 0)
        {
            return right;
        }
        if (right.Count == 0)
        {
            return left;
        }
        var combined = new List<T>(left.Count + right.Count);
        combined.AddRange(left);
        combined.AddRange(right);
        return combined;
    }

    private sealed class MatchResult
    {
        internal MatchResult(int nextIndex, IReadOnlyList<XmlRoutingAssignment> assignments)
        {
            NextIndex = nextIndex;
            Assignments = assignments;
        }

        internal int NextIndex { get; }

        internal IReadOnlyList<XmlRoutingAssignment> Assignments { get; }
    }

    private sealed class OccurrenceStack
    {
        internal static readonly OccurrenceStack Empty = new OccurrenceStack(Array.Empty<XmlOccurrenceKey.Frame>());

        private readonly XmlOccurrenceKey.Frame[] _frames;

        private OccurrenceStack(XmlOccurrenceKey.Frame[] frames)
        {
            _frames = frames;
        }

        internal OccurrenceStack Push(XmlContentModelNode node, int iteration)
        {
            var next = new XmlOccurrenceKey.Frame[_frames.Length + 1];
            Array.Copy(_frames, next, _frames.Length);
            next[^1] = new XmlOccurrenceKey.Frame(node, iteration);
            return new OccurrenceStack(next);
        }

        internal XmlOccurrenceKey CurrentOccurrence() =>
            _frames.Length == 0 ? XmlOccurrenceKey.Root : new XmlOccurrenceKey((XmlOccurrenceKey.Frame[])_frames.Clone());
    }
}
