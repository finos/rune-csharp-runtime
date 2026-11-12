using System.Collections.Concurrent;
using System.Reflection;
using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Maps fully-qualified Rune type names ("cdm.event.common.TradeState", the "@type"
/// wire values) to CLR types. Populated lazily per assembly from [RuneDataType]
/// annotations the first time a type from that assembly is serialized.
/// </summary>
public static class RuneTypeRegistry
{
    private static readonly ConcurrentDictionary<string, Type> Types = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<Assembly, Lazy<bool>> ScannedAssemblies = new();

    /// <summary>Resolves a "@type" value, scanning <paramref name="hintAssembly"/> (and
    /// the runtime assembly) on first use. Returns null when unknown.</summary>
    public static Type? Resolve(string qualifiedName, Assembly hintAssembly)
    {
        EnsureScanned(hintAssembly);
        EnsureScanned(typeof(RuneTypeRegistry).Assembly);
        return Types.TryGetValue(qualifiedName, out Type? type) ? type : null;
    }

    private static void EnsureScanned(Assembly assembly)
    {
        _ = ScannedAssemblies.GetOrAdd(assembly, a => new Lazy<bool>(() =>
        {
            foreach (Type type in a.GetTypes())
            {
                var attr = type.GetCustomAttribute<RuneDataTypeAttribute>();
                if (attr != null)
                {
                    Types.TryAdd(attr.QualifiedName, type);
                }
            }
            return true;
        })).Value;
    }
}
