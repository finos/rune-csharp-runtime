using System.Collections.Concurrent;
using System.Reflection;
using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Resolves a model type to its metadata companion instance, cached per type.
/// The C# stand-in for Java's <c>RosettaModelObject.metaData()</c>.
/// </summary>
public static class RuneMetaRegistry
{
    private static readonly ConcurrentDictionary<Type, IRosettaMetaData?> Cache = new();

    /// <summary>
    /// The companion for <paramref name="modelType"/>, or null when it has none — which is the
    /// case for the runtime's own wrapper types, and for any model generated before the
    /// validation tier existed. A null companion means the node is still traversed but
    /// contributes no results, matching Java's BasicRosettaMetaData.
    /// </summary>
    public static IRosettaMetaData? For(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        return Cache.GetOrAdd(modelType, static t =>
        {
            var attribute = t.GetCustomAttribute<RuneMetaAttribute>(inherit: false);
            if (attribute == null)
            {
                return null;
            }
            Type metaType = attribute.MetaType;
            if (metaType.ContainsGenericParameters && t.IsConstructedGenericType)
            {
                metaType = metaType.MakeGenericType(t.GetGenericArguments());
            }
            object instance = Activator.CreateInstance(metaType)
                ?? throw new InvalidOperationException($"Could not construct {metaType}.");
            if (instance is not IRosettaMetaData metaData)
            {
                throw new InvalidOperationException(
                    $"{metaType} is declared as the metadata companion of {t} but does not implement "
                    + $"{nameof(IRosettaMetaData)}.");
            }
            return metaData;
        });
    }
}
