using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;
using Finos.Rune.Runtime.Xml;

namespace Finos.Rune.Runtime.Hashing;

/// <summary>
/// Computes an object's global key by folding a hash over its attributes, reproducing Java's
/// <c>NonNullHashCollector</c> driven by a generated <c>process(RosettaPath, Processor)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The traversal is the whole difficulty. Because <see cref="HashAccumulator"/> multiplies by 31 at
/// every step, visiting the right values in the wrong order, or visiting one value too many, changes
/// the key completely — and a wrong key is indistinguishable from a wrong walk. Six rules decide what
/// is visited, and five of them exclude rather than include:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>The whole <c>meta</c> block is excluded</b>, <c>@key:external</c> included. Java tags every
/// <c>MetaFields</c> basic with <c>AttributeMeta.META</c>, and the <c>EXTERNAL_KEY</c> escape that
/// would let one back in is referenced by zero CDM 7 classes. The meta-ish values that *are* hashed
/// on a reference wrapper are <c>externalReference</c> and the wrapper's own <c>value</c>; only
/// <c>globalReference</c> is excluded there.
/// </item>
/// <item>
/// <b>Hashing does not stop at nested keyed objects.</b> Java's stop rule keys off the
/// <em>attribute's</em> metadata, not the target type's <c>[metadata key]</c>, and the bare
/// <c>GLOBAL_KEY</c> flag is emitted nowhere — so a <c>Party</c> hash descends into its
/// <c>PartyIdentifier</c> and includes everything in it. What actually stops the walk is
/// <c>[metadata id]</c> (<see cref="Serialization.RunePropertyTable.Entry.IsGlobalKeyField"/>).
/// </item>
/// <item>
/// <b>A metadata wrapper is a participant, not a pass-through.</b> A <c>FieldWithMeta</c> contributes
/// one structural <c>×31</c> and then its value contributes a real hash — so whether a string
/// attribute carries <c>[metadata scheme]</c> changes the enclosing key, because the unannotated one
/// goes straight through with no structural step.
/// </item>
/// <item>
/// <b>A reference wrapper has two behaviours</b> depending on whether it holds a <em>scoped</em>
/// reference. Holding one, it is excluded and its subtree is pruned; holding none — an inline value —
/// it is an ordinary object. Note that this turns on the scoped reference, not on <c>@ref</c>: a
/// wrapper carrying only a global reference is treated as ordinary.
/// </item>
/// <item>
/// <b><c>MetaFields</c> itself is skipped but descended into</b>, because Java's <c>MetaFields</c>
/// implements <c>GlobalKeyFields</c> — the interface meaning "I am the metadata", as distinct from
/// <c>GlobalKey</c>, "I have metadata", which the wrappers implement and which excludes nothing.
/// </item>
/// <item>
/// <b>Null and no-data nodes contribute nothing at all</b> — not even the multiply. "NonNull" in the
/// Java name is literal, and the test is <c>hasData()</c>, which itself ignores <c>meta</c>.
/// </item>
/// </list>
/// <para>
/// The walk runs over built objects rather than builders, matching Java: its collector is typed as
/// the read-side <c>Processor</c>, so <c>builder.process(path, hasher)</c> binds the value-side
/// overload. That matters for lists — see <see cref="ProcessRosettaList"/>.
/// </para>
/// </remarks>
public sealed class GlobalKeyHasher
{
    /// <summary>
    /// The one path element the collector inspects: a reference wrapper's external reference is the
    /// sole survivor of Java's <c>isExternalKeyOrReference</c> escape hatch.
    /// </summary>
    private static readonly RosettaPath ExternalReferencePath = RosettaPath.ValueOf("externalReference");

    /// <summary>The one basic a reference wrapper genuinely excludes.</summary>
    private static readonly RosettaPath GlobalReferencePath = RosettaPath.ValueOf("globalReference");

    private readonly HashAccumulator _report = new();

    private GlobalKeyHasher()
    {
    }

    /// <summary>
    /// The global key of <paramref name="instance"/>, as Java's <c>Integer.toHexString</c> renders it
    /// — lower-case and unpadded, so keys shorter than eight characters are normal (a quarter of the
    /// keys in CDM's function resources).
    /// </summary>
    public static string Hash(IRosettaModelObject instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var hasher = new GlobalKeyHasher();
        hasher.Descend(RosettaPath.ValueOf(RuneTypeName.Simple(instance.GetType())), instance);
        return hasher._report.ToString();
    }

    private void Descend(RosettaPath path, IRosettaModelObject instance)
    {
        RunePropertyTable table = RunePropertyTable.For(instance.GetType());
        foreach (RunePropertyTable.Entry entry in table.DeclarationOrderEntries)
        {
            RosettaPath sub = path.NewSubPath(entry.PathName);
            object? value = entry.Property.GetValue(instance);
            bool isModelObject = typeof(IRosettaModelObject).IsAssignableFrom(entry.DeclaredType);

            if (entry.ListElementType != null)
            {
                if (value is not System.Collections.IEnumerable items)
                {
                    continue;
                }
                var elements = items.Cast<object?>().ToList();
                if (isModelObject)
                {
                    ProcessRosettaList(sub, elements, instance, entry);
                }
                else
                {
                    for (int i = 0; i < elements.Count; i++)
                    {
                        ProcessBasic(sub.WithIndex(i), elements[i], instance);
                    }
                }
            }
            else if (isModelObject)
            {
                ProcessRosettaSingle(sub, (IRosettaModelObject?)value, instance, entry);
            }
            else
            {
                ProcessBasic(sub, value, instance);
            }
        }

        if (instance is MetaFields metaFields)
        {
            ProcessSynthesizedKey(metaFields);
        }
    }

    /// <summary>
    /// Java's <c>MetaFields</c> ends with a <c>List&lt;Key&gt;</c>; the C# port flattens that to a
    /// single <c>ScopedKey</c> string.
    /// </summary>
    /// <remarks>
    /// A <c>Key</c> reaches the collector with no attribute metadata, so it is included as an
    /// ordinary object — but <c>Key.process()</c> in Java has an empty body, so neither its
    /// <c>value</c> nor its <c>scope</c> is ever hashed. The entire contribution of a key is one bare
    /// structural multiply, and only when <c>hasData()</c> holds, which for <c>Key</c> means its value
    /// is set. So the scope-only keys that <c>GetOrCreate…()</c> seeds for <c>[metadata location]</c>
    /// attributes contribute nothing, while a populated one shifts the enclosing hash.
    /// </remarks>
    private void ProcessSynthesizedKey(MetaFields metaFields)
    {
        if (metaFields.ScopedKey != null)
        {
            _report.Accumulate();
        }
    }

    private void ProcessRosettaSingle(
        RosettaPath path, IRosettaModelObject? child, IRosettaModelObject parent, RunePropertyTable.Entry entry)
    {
        (bool include, bool descend) = ShouldIncludeInHash(child, parent, entry);
        if (include)
        {
            _report.Accumulate();
        }

        if (child != null && descend)
        {
            Descend(path, child);
        }
    }

    /// <summary>
    /// A list, visited element by element — the list itself is never a hash participant.
    /// </summary>
    /// <remarks>
    /// The descent gate is all-or-nothing over the whole list, not per element: Java ANDs every
    /// element's <c>continueProcessing</c> and the read-side traversal descends only if the
    /// conjunction holds. So one element with no data, or one reference wrapper holding a reference,
    /// suppresses the descent into <em>every</em> element. (The builder-side traversal discards that
    /// boolean and always descends, which is why this walk must follow the read side.)
    /// <para>
    /// The two passes also index differently — the visit uses the raw loop index while the descent
    /// counts only non-null elements. That is reproduced rather than tidied up, matching the
    /// validation walker; it can only shift a path, never a hash value, since the collector reads the
    /// path for one exact-match test that no index takes part in.
    /// </para>
    /// </remarks>
    private void ProcessRosettaList(
        RosettaPath path, List<object?> children, IRosettaModelObject parent, RunePropertyTable.Entry entry)
    {
        bool descend = true;
        for (int i = 0; i < children.Count; i++)
        {
            (bool include, bool continueProcessing) =
                ShouldIncludeInHash((IRosettaModelObject?)children[i], parent, entry);
            if (include)
            {
                _report.Accumulate();
            }
            descend &= continueProcessing;
        }

        if (!descend)
        {
            return;
        }

        int index = 0;
        foreach (object? child in children)
        {
            if (child != null)
            {
                Descend(path.WithIndex(index++), (IRosettaModelObject)child);
            }
        }
    }

    private void ProcessBasic(RosettaPath path, object? value, IRosettaModelObject parent)
    {
        if (value == null || IsExcludedMetaValue(path, parent))
        {
            return;
        }

        int valueHash = value is DateTimeOffset dateTime
            ? JavaHash.Of(dateTime, JavaZoneId(parent, path, dateTime))
            : JavaHash.OfValue(value);
        _report.Accumulate(valueHash);
    }

    private static string JavaZoneId(
        IRosettaModelObject parent, RosettaPath path, DateTimeOffset value)
    {
        RosettaPath.Element? element = path.PathElement;
        if (parent is IXmlTemporalMetadataHolder { XmlTemporalMetadata: not null } holder
            && element != null)
        {
            string property = element.PathName
                + (element.Index.HasValue ? $"[{element.Index.Value}]" : string.Empty);
            if (holder.XmlTemporalMetadata.TryGet(property, out XmlZoneMetadata zone))
            {
                return zone.Kind switch
                {
                    XmlTimeZoneKind.Absent => "Unknown",
                    XmlTimeZoneKind.Utc => "Z",
                    _ => JavaHash.ZoneOffsetId(zone.Offset),
                };
            }
        }

        return JavaHash.ZoneOffsetId(value.Offset);
    }

    /// <summary>
    /// Whether a basic value is one of the metadata fields Java tags <c>AttributeMeta.META</c> and
    /// therefore skips.
    /// </summary>
    private static bool IsExcludedMetaValue(RosettaPath path, IRosettaModelObject parent)
    {
        if (parent is MetaFields)
        {
            return true;
        }

        // Of the wrapper's own strings, Java excludes only the global reference.
        if (parent is IReferenceWithMetaView)
        {
            return path.EndsWith(GlobalReferencePath);
        }

        return false;
    }

    /// <summary>
    /// Java's <c>shouldIncludeInHash</c>: whether this node contributes a structural step, and
    /// whether the walk continues into it.
    /// </summary>
    /// <remarks>
    /// The order of these tests is load-bearing — the no-data check comes before everything, and the
    /// wrapper checks before the <c>[metadata id]</c> check.
    /// </remarks>
    private static (bool Include, bool Descend) ShouldIncludeInHash(
        IRosettaModelObject? instance, IRosettaModelObject parent, RunePropertyTable.Entry entry)
    {
        if (instance == null || !instance.ToBuilder().HasData())
        {
            return (false, false);
        }

        if (instance is MetaFields)
        {
            return (false, true);
        }

        if (instance is IReferenceWithMetaView { ScopedReferenceObject: not null })
        {
            return (false, true);
        }

        if (parent is IReferenceWithMetaView { ScopedReferenceObject: not null })
        {
            return (false, false);
        }

        if (entry.IsGlobalKeyField)
        {
            return (false, false);
        }

        return (true, true);
    }
}
