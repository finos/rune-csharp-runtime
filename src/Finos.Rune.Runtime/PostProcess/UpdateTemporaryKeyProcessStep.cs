using System.Text.RegularExpressions;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Renumbers temporary scoped keys into stable ones, and rewrites the references that name them —
/// Java's <c>UpdateTemporaryKeyProcessStep</c>.
/// </summary>
/// <remarks>
/// <para>
/// FpML ingestion mints scoped keys from a hash, so they arrive looking like <c>quantity-$4173</c>: unique,
/// but arbitrary and unstable. This step replaces them with <c>quantity-1</c>, <c>quantity-2</c>, … in the
/// order they are first encountered, so that two ingests of the same document produce the same names. It
/// runs before reference resolution, because resolution matches on those names.
/// </para>
/// <para>
/// <b>Numbering is per prefix and per scope</b>, counting from 1, in first-encounter order. Three
/// consequences are easy to get wrong and all three are exercised by CDM's own expectations:
/// </para>
/// <list type="bullet">
/// <item>
/// A temporary value that appears only as a <em>reference</em> still claims a number, because keys and
/// references share one ordered map. So the lowest number in a document need not belong to a key at all.
/// </item>
/// <item>
/// Several nodes carrying the <em>same</em> temporary value collapse to one new value — the map is keyed by
/// the value, not by the node. Numbering per node instead would hand out as many numbers as there are
/// nodes.
/// </item>
/// <item>
/// Encounter order is <em>walk</em> order, which is not path order once references are in play: a
/// reference deep in one branch can claim a number before a key that sits earlier in the tree.
/// </item>
/// </list>
/// <para>
/// The step is idempotent, because its own output no longer matches the temporary pattern.
/// </para>
/// </remarks>
public static class UpdateTemporaryKeyProcessStep
{
    /// <summary>
    /// A temporary scoped key: letters, a hyphen, a literal dollar, digits.
    /// </summary>
    /// <remarks>
    /// Anchored, because Java applies it with <c>String.matches</c>, which is a full-string match where
    /// .NET's <c>IsMatch</c> is a search — unanchored, this would also fire on <c>foo-$1-bar</c> and, worse,
    /// on any string merely containing the pattern. Both quantifiers are zero-or-more, so
    /// <c>quantity-$</c> and even <c>-$</c> match; only the <c>-$</c> itself is required.
    /// </remarks>
    private static readonly Regex TemporaryKey =
        new("^[a-zA-Z]*-\\$[0-9]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The scope value Java stamps on every key and reference it rewrites.</summary>
    private const string DocumentScope = "DOCUMENT";

    /// <summary>Ordered temporary values within one scope, and their assigned replacements.</summary>
    private sealed class Renumbering
    {
        private readonly Dictionary<string, string> _replacement = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _nextIndexByPrefix = new(StringComparer.Ordinal);

        /// <summary>Records a first encounter and assigns its replacement immediately.</summary>
        internal void Encounter(string temporary)
        {
            if (_replacement.ContainsKey(temporary))
            {
                return;
            }

            string prefix = temporary.Split('-')[0];
            int index = _nextIndexByPrefix.TryGetValue(prefix, out int next) ? next : 1;
            _nextIndexByPrefix[prefix] = index + 1;

            _replacement[temporary] = $"{prefix}-{index}";
        }

        internal string? Replacement(string temporary) =>
            _replacement.TryGetValue(temporary, out string? value) ? value : null;
    }

    /// <summary>
    /// <paramref name="instance"/> with every temporary scoped key and matching reference renumbered.
    /// </summary>
    public static T Renumber<T>(T instance, ReferenceConfig config)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(config);

        var scopes = new ScopeBuckets<Renumbering>();
        Collect(instance, config, scopes);

        return (T)RuneTreeRewriter.Rewrite(
            instance, entry: null, (rebuilt, _, path) => Rewrite(rebuilt, path, scopes));
    }

    /// <summary>Pass 1: record every temporary value in first-encounter order, per scope.</summary>
    /// <remarks>
    /// Only metadata <em>wrappers</em> are considered. Java gates the whole collector on the node being a
    /// wrapper, so a temporary value sitting on a plain <c>[metadata key]</c> type is invisible to this step
    /// — a port that walked every key-carrying node would renumber keys Java leaves alone. The reference and
    /// key branches are mutually exclusive for the same reason Java's are: a reference wrapper is only ever
    /// examined for its reference.
    /// </remarks>
    private static void Collect(IRosettaModelObject root, ReferenceConfig config, ScopeBuckets<Renumbering> scopes)
    {
        // Skip(1) drops the root, which Java never collects.
        foreach (GlobalKeyNode node in GlobalKeyWalk.Enumerate(root).Skip(1))
        {
            if (config.ScopeType != null && config.ScopeType.IsAssignableFrom(node.Instance.GetType()))
            {
                scopes.Open(node.Path);
            }

            string? temporary = TemporaryValueOf(node.Instance);
            if (temporary != null)
            {
                scopes.For(node.Path).Encounter(temporary);
            }
        }
    }

    /// <summary>Pass 2: substitute the assigned replacement, stamping the document scope.</summary>
    private static IRosettaModelObject Rewrite(
        IRosettaModelObject rebuilt, RosettaPath path, ScopeBuckets<Renumbering> scopes)
    {
        string? temporary = TemporaryValueOf(rebuilt);
        if (temporary == null)
        {
            return rebuilt;
        }

        string? replacement = scopes.For(path).Replacement(temporary);
        if (replacement == null)
        {
            return rebuilt;
        }

        RunePropertyTable table = RunePropertyTable.For(rebuilt.GetType());
        IRosettaModelObjectBuilder? builder = null;

        if (rebuilt is IReferenceWithMetaView)
        {
            RunePropertyTable.Entry reference = table.FindByWireName(WireNames.ScopedReference)
                ?? throw new InvalidOperationException(
                    $"{rebuilt.GetType().Name} carries a scoped reference with no `@ref:scoped` attribute.");

            // Replaced rather than edited, which drops any pointsTo, as in Java.
            Reference replaced = Reference.CreateBuilder()
                .SetScopedReference(replacement)
                .SetScope(DocumentScope)
                .Build();
            RuneTreeRewriter.Set(ref builder, rebuilt, reference, replaced);
            return (IRosettaModelObject)builder!.Build();
        }

        RunePropertyTable.Entry meta = table.FindByWireName(WireNames.Meta)
            ?? throw new InvalidOperationException(
                $"{rebuilt.GetType().Name} carries a scoped key with no `meta` attribute.");
        MetaFields existing = ((IFieldWithMetaView)rebuilt).MetaObject!;
        RuneTreeRewriter.Set(
            ref builder, rebuilt, meta, ((MetaFields.Builder)existing.ToBuilder()).SetScopedKey(replacement).Build());
        return (IRosettaModelObject)builder!.Build();
    }

    /// <summary>
    /// The temporary value this wrapper carries, or null — a reference's scoped reference, else a field's
    /// scoped key.
    /// </summary>
    private static string? TemporaryValueOf(IRosettaModelObject instance)
    {
        string? value = instance switch
        {
            IReferenceWithMetaView reference => reference.ScopedReferenceObject?.ScopedReference,
            IFieldWithMetaView wrapper => wrapper.MetaObject?.ScopedKey,
            _ => null,
        };

        return value != null && TemporaryKey.IsMatch(value) ? value : null;
    }
}
