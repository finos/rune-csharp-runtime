using System.Collections.Concurrent;
using System.Reflection;
using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Cached per-type binding table for the Rune JSON converters: the [RuneAttribute]
/// properties of a model type in serialization order, their wire names, declared types,
/// and the matching builder setters. This is the C# analogue of the Jackson
/// introspector in rune-common — reflection happens once per type.
/// </summary>
public sealed class RunePropertyTable
{
    private static readonly ConcurrentDictionary<Type, RunePropertyTable> Cache = new();

    private static readonly string[] SpecialOrder =
    {
        WireNames.Meta, WireNames.Reference, WireNames.ExternalReference,
        WireNames.ScopedReference, WireNames.Data,
    };

    public sealed class Entry
    {
        public required string WireName { get; init; }

        public required PropertyInfo Property { get; init; }

        /// <summary>Unwrapped into the parent object (RuneMetaType), e.g. MetaFields.</summary>
        public required bool IsMetaType { get; init; }

        /// <summary>Element type for lists (IReadOnlyList&lt;T&gt;), else null.</summary>
        public required Type? ListElementType { get; init; }

        /// <summary>Builder method accepting the read value (built form).</summary>
        public required MethodInfo? Setter { get; init; }

        /// <summary>
        /// Declared <c>[metadata id]</c> — Java's <c>AttributeMeta.GLOBAL_KEY_FIELD</c>. Excluded
        /// from the enclosing object's global-key hash, and the only thing that earns a metadata
        /// wrapper a key of its own. See <see cref="Attributes.RuneGlobalKeyFieldAttribute"/>.
        /// </summary>
        public required bool IsGlobalKeyField { get; init; }

        /// <summary>
        /// Name of this property in a <see cref="Path.RosettaPath"/>, which is the Rune attribute
        /// name and therefore usually the wire name too. The two diverge only on the runtime's own
        /// meta wrappers, where Java names the members <c>value</c>/<c>globalReference</c>/… while
        /// the wire names them <c>@data</c>/<c>@ref</c>/…; those carry a
        /// <see cref="Attributes.RunePathNameAttribute"/>.
        /// </summary>
        public required string PathName { get; init; }

        public Type DeclaredType => ListElementType ?? Property.PropertyType;
    }

    public Type Type { get; }

    public Type BuilderType { get; }

    public RuneDataTypeAttribute? DataType { get; }

    public bool IsChoiceType { get; }

    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>
    /// The same entries in declaration order — base type first, then this type's own properties in
    /// the order they are declared — without the serialization reordering that
    /// <see cref="Entries"/> applies.
    /// </summary>
    /// <remarks>
    /// This is the order Java's generated <c>process(RosettaPath, Processor)</c> visits attributes
    /// in, so it is the order the validation walk must use. It matters because Java appends the
    /// <c>meta</c> property when a type declares <c>[metadata key]</c> and then inherits it into
    /// subtypes at that position, whereas <see cref="Entries"/> hoists <c>meta</c> and the
    /// reference members to the front.
    /// </remarks>
    public IReadOnlyList<Entry> DeclarationOrderEntries { get; }

    private readonly MethodInfo _createBuilder;

    public static RunePropertyTable For(Type type) => Cache.GetOrAdd(type, t => new RunePropertyTable(t));

    private RunePropertyTable(Type type)
    {
        Type = type;
        DataType = type.GetCustomAttribute<RuneDataTypeAttribute>();
        IsChoiceType = type.GetCustomAttribute<RuneChoiceTypeAttribute>() != null;

        Type builderType = type.GetNestedType("Builder", BindingFlags.Public)
            ?? FindInherited(type, t => t.GetNestedType("Builder", BindingFlags.Public))
            ?? throw new InvalidOperationException($"No nested Builder type on {type}.");
        BuilderType = CloseOverDeclaringArguments(builderType, type);
        _createBuilder = type.GetMethod("CreateBuilder", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            ?? FindInherited(type, t => t.GetMethod("CreateBuilder", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            ?? throw new InvalidOperationException($"No CreateBuilder on {type}.");

        var chain = new List<Type>();
        for (Type? level = type; level != null && level != typeof(object); level = level.BaseType)
        {
            chain.Add(level);
        }
        chain.Reverse();

        var declarationOrder = new List<Entry>();
        var positionByWireName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Type level in chain)
        {
            // GetProperties order is undocumented, so declaration order comes from the metadata token.
            IEnumerable<PropertyInfo> properties = level
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.MetadataToken);
            foreach (PropertyInfo property in properties)
            {
                var wire = property.GetCustomAttribute<RuneAttributeAttribute>();
                if (wire == null || property.GetCustomAttribute<RuneIgnoreAttribute>() != null)
                {
                    continue;
                }
                Type? listElement = GetListElementType(property.PropertyType);
                var entry = new Entry
                {
                    WireName = wire.Name,
                    PathName = property.GetCustomAttribute<RunePathNameAttribute>()?.Name ?? wire.Name,
                    Property = property,
                    IsMetaType = property.GetCustomAttribute<RuneMetaTypeAttribute>() != null,
                    ListElementType = listElement,
                    Setter = FindSetter(BuilderType, property, listElement),
                    IsGlobalKeyField = property.GetCustomAttribute<RuneGlobalKeyFieldAttribute>() != null,
                };
                if (positionByWireName.TryGetValue(wire.Name, out int existing))
                {
                    declarationOrder[existing] = entry;
                }
                else
                {
                    positionByWireName[wire.Name] = declarationOrder.Count;
                    declarationOrder.Add(entry);
                }
            }
        }

        DeclarationOrderEntries = declarationOrder;
        Entries = declarationOrder
            .OrderBy(e => { int i = Array.IndexOf(SpecialOrder, e.WireName); return i < 0 ? SpecialOrder.Length : i; })
            .ToList();
    }

    public IRosettaModelObjectBuilder CreateBuilder() =>
        (IRosettaModelObjectBuilder)_createBuilder.Invoke(null, null)!;

    public Entry? FindByWireName(string name) =>
        Entries.FirstOrDefault(e => string.Equals(e.WireName, name, StringComparison.Ordinal));

    /// <summary>
    /// A nested type of a constructed generic comes back open from GetNestedType, so
    /// <c>FieldWithMeta&lt;string&gt;.Builder</c> resolves to <c>FieldWithMeta&lt;T&gt;.Builder</c>
    /// and every setter lookup against it fails to match. Close it over the outer type's arguments.
    /// </summary>
    private static Type CloseOverDeclaringArguments(Type nested, Type declaring)
    {
        if (!nested.ContainsGenericParameters || !declaring.IsConstructedGenericType)
        {
            return nested;
        }
        return nested.MakeGenericType(declaring.GetGenericArguments());
    }

    private static T? FindInherited<T>(Type type, Func<Type, T?> probe) where T : class
    {
        for (Type? t = type.BaseType; t != null; t = t.BaseType)
        {
            T? result = probe(t);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private static Type? GetListElementType(Type propertyType)
    {
        if (propertyType.IsGenericType &&
            propertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return propertyType.GetGenericArguments()[0];
        }
        return null;
    }

    private static MethodInfo? FindSetter(Type builderType, PropertyInfo property, Type? listElement)
    {
        string name = "Set" + property.Name;
        Type wanted = listElement != null
            ? typeof(IEnumerable<>).MakeGenericType(listElement)
            : property.PropertyType;
        return builderType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == name
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == wanted);
    }
}
