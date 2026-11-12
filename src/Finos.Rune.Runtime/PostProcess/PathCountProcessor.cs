using System.Collections;
using System.Collections.ObjectModel;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// Counts populated basic-value paths using rune-common's <c>PathCountProcessor</c> rules.
/// </summary>
public static class PathCountProcessor
{
    private static readonly RosettaPath GlobalReferencePath = RosettaPath.ValueOf("globalReference");
    private static readonly RosettaPath GlobalKeyPath = RosettaPath.ValueOf("globalKey");

    /// <summary>Collects every counted path and its value.</summary>
    public static PathCountReport Process(IRosettaModelObject instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var collectedPaths = new Dictionary<RosettaPath, object>();
        ProcessObject(instance, parent: null, RosettaPath.Null, collectedPaths);
        return new PathCountReport(collectedPaths);
    }

    /// <summary>Returns the number of paths collected from <paramref name="instance"/>.</summary>
    public static int Count(IRosettaModelObject instance) => Process(instance).Count;

    private static void ProcessObject(
        IRosettaModelObject instance,
        IRosettaModelObject? parent,
        RosettaPath path,
        Dictionary<RosettaPath, object> collectedPaths)
    {
        if (IsResolvedValueWithReference(instance, parent))
        {
            return;
        }

        foreach (RunePropertyTable.Entry entry in RunePropertyTable.For(instance.GetType()).DeclarationOrderEntries)
        {
            object? value = entry.Property.GetValue(instance);
            if (value == null)
            {
                continue;
            }

            RosettaPath valuePath = path.NewSubPath(entry.PathName);
            if (entry.ListElementType == null)
            {
                ProcessValue(value, instance, valuePath, collectedPaths);
                continue;
            }

            int index = 0;
            foreach (object? item in (IEnumerable)value)
            {
                RosettaPath itemPath = valuePath.WithIndex(index++);
                if (item != null)
                {
                    ProcessValue(item, instance, itemPath, collectedPaths);
                }
            }
        }
    }

    private static void ProcessValue(
        object value,
        IRosettaModelObject parent,
        RosettaPath path,
        Dictionary<RosettaPath, object> collectedPaths)
    {
        if (value is IRosettaModelObject modelObject)
        {
            ProcessObject(modelObject, parent, path, collectedPaths);
            return;
        }

        if (parent is MetaFields && path.EndsWith(GlobalKeyPath))
        {
            return;
        }

        if (parent is IReferenceWithMetaView && path.EndsWith(GlobalReferencePath))
        {
            return;
        }

        collectedPaths[path] = value;
    }

    private static bool IsResolvedValueWithReference(
        IRosettaModelObject instance,
        IRosettaModelObject? parent)
    {
        if (parent is not IReferenceWithMetaView reference || instance is Reference)
        {
            return false;
        }

        return reference.GlobalReferenceValue != null
            || reference.ExternalReferenceValue != null
            || reference.ScopedReferenceObject?.ScopedReference != null;
    }
}

/// <summary>A read-only snapshot of paths collected by <see cref="PathCountProcessor"/>.</summary>
public sealed class PathCountReport
{
    internal PathCountReport(Dictionary<RosettaPath, object> collectedPaths)
    {
        CollectedPaths = new ReadOnlyDictionary<RosettaPath, object>(
            new Dictionary<RosettaPath, object>(collectedPaths));
    }

    public IReadOnlyDictionary<RosettaPath, object> CollectedPaths { get; }

    public int Count => CollectedPaths.Count;
}
