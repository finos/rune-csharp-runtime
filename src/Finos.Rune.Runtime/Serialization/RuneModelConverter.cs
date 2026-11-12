using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Converter factory for all generated model types (data types, choice types, and the
/// runtime's MetaFields/Key/Reference). Implements the standardized Rune JSON rules:
/// builder-bound reads, RuneMetaType unwrapping, NON_EMPTY inclusion, conditional
/// "@type" for subtype substitution (trailing on plain objects, leading on choices),
/// and strict or lenient unknown-member handling.
/// </summary>
public sealed class RuneModelConverterFactory : JsonConverterFactory
{
    private readonly bool _strict;

    public RuneModelConverterFactory(bool strict)
    {
        _strict = strict;
    }

    public override bool CanConvert(Type typeToConvert) =>
        typeof(IRosettaModelObject).IsAssignableFrom(typeToConvert)
        && !typeToConvert.IsGenericType
        && !typeof(IRosettaModelObjectBuilder).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(RuneModelConverter<>).MakeGenericType(typeToConvert), new object[] { _strict })!;
}

public sealed class RuneModelConverter<T> : JsonConverter<T> where T : class, IRosettaModelObject
{
    private readonly bool _strict;

    public RuneModelConverter(bool strict)
    {
        _strict = strict;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return (T?)RuneJsonEngine.ReadModel(document.RootElement, typeToConvert, options, _strict);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        RuneJsonEngine.WriteModelBody(writer, value, typeof(T), options);
        writer.WriteEndObject();
    }
}

/// <summary>Shared read/write logic used by the model and wrapper converters.</summary>
internal static class RuneJsonEngine
{
    /// <summary>Writes the properties of a model object inline (no braces), unwrapping
    /// RuneMetaType children, then the trailing "@type" when the runtime type is a
    /// strict subtype of the declared type. Choice objects instead lead with "@type"
    /// and flatten the selected option.</summary>
    internal static void WriteModelBody(Utf8JsonWriter writer, IRosettaModelObject value, Type declaredType, JsonSerializerOptions options)
    {
        Type actualType = value.GetType();
        RunePropertyTable table = RunePropertyTable.For(actualType);

        if (table.IsChoiceType)
        {
            WriteChoiceBody(writer, value, table, options);
            return;
        }

        foreach (RunePropertyTable.Entry entry in table.Entries)
        {
            object? propertyValue = entry.Property.GetValue(value);
            if (IsEmpty(propertyValue))
            {
                continue;
            }
            if (entry.IsMetaType)
            {
                WriteModelBody(writer, (IRosettaModelObject)propertyValue!, entry.Property.PropertyType, options);
                continue;
            }
            writer.WritePropertyName(entry.WireName);
            if (entry.ListElementType != null)
            {
                writer.WriteStartArray();
                foreach (object? item in (System.Collections.IEnumerable)propertyValue!)
                {
                    if (item != null)
                    {
                        JsonSerializer.Serialize(writer, item, entry.ListElementType, options);
                    }
                }
                writer.WriteEndArray();
            }
            else
            {
                JsonSerializer.Serialize(writer, propertyValue, entry.DeclaredType, options);
            }
        }

        if (actualType != declaredType && table.DataType != null
            && declaredType.IsAssignableFrom(actualType))
        {
            writer.WriteString(WireNames.Type, table.DataType.QualifiedName);
        }
    }

    private static void WriteChoiceBody(Utf8JsonWriter writer, IRosettaModelObject choice, RunePropertyTable table, JsonSerializerOptions options)
    {
        RunePropertyTable.Entry? setEntry = null;
        object? optionValue = null;
        foreach (RunePropertyTable.Entry entry in table.Entries)
        {
            object? v = entry.Property.GetValue(choice);
            if (!IsEmpty(v))
            {
                if (setEntry != null)
                {
                    throw new JsonException(
                        $"Choice type {table.Type.Name} has multiple options set ({setEntry.WireName}, {entry.WireName}).");
                }
                setEntry = entry;
                optionValue = v;
            }
        }
        if (setEntry == null)
        {
            return;
        }
        switch (optionValue)
        {
            case IRosettaModelObject model when RunePropertyTable.For(model.GetType()).IsChoiceType:
                WriteChoiceBody(writer, model, RunePropertyTable.For(model.GetType()), options);
                break;
            case Meta.IFieldWithMetaView fieldView:
                writer.WriteString(WireNames.Type, QualifiedNameOf(fieldView.ValueType));
                WriteFieldWithMetaBody(writer, fieldView, options);
                break;
            case Meta.IReferenceWithMetaView referenceView:
                writer.WriteString(WireNames.Type, QualifiedNameOf(referenceView.ValueType));
                WriteReferenceWithMetaBody(writer, referenceView, options);
                break;
            case IRosettaModelObject complexOption:
                writer.WriteString(WireNames.Type, QualifiedNameOf(complexOption.GetType()));
                WriteModelBody(writer, complexOption, complexOption.GetType(), options);
                break;
            default:
                writer.WriteString(WireNames.Type, setEntry.WireName);
                writer.WritePropertyName(WireNames.Data);
                JsonSerializer.Serialize(writer, optionValue, setEntry.DeclaredType, options);
                break;
        }
    }

    private static string QualifiedNameOf(Type type) =>
        RunePropertyTable.For(type).DataType?.QualifiedName
            ?? throw new JsonException($"Type {type} carries no RuneDataType identity.");

    /// <summary>Writes a FieldWithMeta body inline: unwrapped meta keys, then the value
    /// ("@data" for basics and choice values, flattened for other model objects).</summary>
    internal static void WriteFieldWithMetaBody(Utf8JsonWriter writer, Meta.IFieldWithMetaView wrapper, JsonSerializerOptions options)
    {
        if (wrapper.MetaObject != null)
        {
            WriteModelBody(writer, wrapper.MetaObject, typeof(Metafields.MetaFields), options);
        }
        WriteWrapperValue(writer, wrapper.ValueObject, wrapper.ValueType, options);
    }

    /// <summary>Writes a ReferenceWithMeta body inline: the reference keys, then the value.</summary>
    internal static void WriteReferenceWithMetaBody(Utf8JsonWriter writer, Meta.IReferenceWithMetaView wrapper, JsonSerializerOptions options)
    {
        if (wrapper.GlobalReferenceValue != null)
        {
            writer.WriteString(WireNames.Reference, wrapper.GlobalReferenceValue);
        }
        if (wrapper.ExternalReferenceValue != null)
        {
            writer.WriteString(WireNames.ExternalReference, wrapper.ExternalReferenceValue);
        }
        if (wrapper.ScopedReferenceObject?.ScopedReference != null)
        {
            writer.WriteString(WireNames.ScopedReference, wrapper.ScopedReferenceObject.ScopedReference);
        }
        WriteWrapperValue(writer, wrapper.ValueObject, wrapper.ValueType, options);
    }

    private static void WriteWrapperValue(Utf8JsonWriter writer, object? value, Type valueType, JsonSerializerOptions options)
    {
        if (IsEmpty(value))
        {
            return;
        }
        if (value is IRosettaModelObject model)
        {
            if (RunePropertyTable.For(model.GetType()).IsChoiceType)
            {
                writer.WritePropertyName(WireNames.Data);
                JsonSerializer.Serialize(writer, model, valueType, options);
            }
            else
            {
                WriteModelBody(writer, model, valueType, options);
            }
            return;
        }
        writer.WritePropertyName(WireNames.Data);
        JsonSerializer.Serialize(writer, value, valueType, options);
    }

    internal static IRosettaModelObject? ReadModel(JsonElement element, Type declaredType, JsonSerializerOptions options, bool strict)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Expected an object for {declaredType.Name} but found {element.ValueKind}.");
        }

        Type actualType = declaredType;
        if (element.TryGetProperty(WireNames.Type, out JsonElement typeElement)
            && !RunePropertyTable.For(declaredType).IsChoiceType)
        {
            string typeName = typeElement.GetString()!;
            Type? resolved = RuneTypeRegistry.Resolve(typeName, declaredType.Assembly);
            if (resolved != null && declaredType.IsAssignableFrom(resolved))
            {
                actualType = resolved;
            }
            else if (strict)
            {
                throw new JsonException(
                    $"'@type' value '{typeName}' does not resolve to a subtype of {declaredType.Name}.");
            }
        }

        RunePropertyTable table = RunePropertyTable.For(actualType);
        if (table.IsChoiceType)
        {
            return ReadChoice(element, table, options, strict);
        }

        IRosettaModelObjectBuilder builder = table.CreateBuilder();
        MetaFields.Builder? meta = null;
        RunePropertyTable.Entry? metaEntry = table.Entries.FirstOrDefault(
            e => e.IsMetaType && e.Property.PropertyType == typeof(MetaFields));

        foreach (JsonProperty jsonProperty in element.EnumerateObject())
        {
            string name = jsonProperty.Name;
            if (name is WireNames.Type or WireNames.Model or WireNames.Version)
            {
                continue;
            }
            if (metaEntry != null && TryReadMetaKey(name, jsonProperty.Value, ref meta))
            {
                continue;
            }
            RunePropertyTable.Entry? entry = table.FindByWireName(name);
            if (entry == null || entry.Setter == null || entry.IsMetaType)
            {
                if (strict)
                {
                    throw new JsonException($"Unknown member '{name}' on {actualType.Name}.");
                }
                continue;
            }
            object? readValue = ReadEntryValue(jsonProperty.Value, entry, options);
            entry.Setter.Invoke(builder, new[] { readValue });
        }

        if (meta != null && metaEntry != null)
        {
            MethodInfoCache.SetMeta(builder, meta.Build());
        }
        return builder.Build();
    }

    private static bool TryReadMetaKey(string name, JsonElement value, ref MetaFields.Builder? meta)
    {
        switch (name)
        {
            case WireNames.Key:
                (meta ??= MetaFields.CreateBuilder()).SetGlobalKey(value.GetString());
                return true;
            case WireNames.ExternalKey:
                (meta ??= MetaFields.CreateBuilder()).SetExternalKey(value.GetString());
                return true;
            case WireNames.ScopedKey:
                (meta ??= MetaFields.CreateBuilder()).SetScopedKey(value.GetString());
                return true;
            case WireNames.Scheme:
                (meta ??= MetaFields.CreateBuilder()).SetScheme(value.GetString());
                return true;
            default:
                return false;
        }
    }

    private static IRosettaModelObject ReadChoice(JsonElement element, RunePropertyTable table, JsonSerializerOptions options, bool strict)
    {
        if (!element.TryGetProperty(WireNames.Type, out JsonElement typeElement))
        {
            throw new JsonException($"Choice type {table.Type.Name} requires '@type'.");
        }
        string typeName = typeElement.GetString()!;

        Type? resolved = RuneTypeRegistry.Resolve(typeName, table.Type.Assembly);
        RunePropertyTable.Entry? option = resolved == null
            ? table.Entries.FirstOrDefault(e => e.WireName == typeName)
            : FindChoiceOption(table, resolved);
        if (option == null || option.Setter == null)
        {
            throw new JsonException($"'@type' value '{typeName}' matches no option of choice {table.Type.Name}.");
        }

        IRosettaModelObjectBuilder builder = table.CreateBuilder();
        object? optionValue;
        Type optionValueType = UnwrapWrapper(option.DeclaredType, out bool isWrapper);
        if (resolved != null && optionValueType != resolved
            && typeof(IRosettaModelObject).IsAssignableFrom(optionValueType)
            && RunePropertyTable.For(optionValueType).IsChoiceType)
        {
            optionValue = ReadChoice(element, RunePropertyTable.For(optionValueType), options, strict);
        }
        else if (resolved != null)
        {
            var remaining = new JsonObject();
            foreach (JsonProperty p in element.EnumerateObject())
            {
                if (p.Name != WireNames.Type)
                {
                    remaining[p.Name] = JsonNode.Parse(p.Value.GetRawText());
                }
            }
            Type target = isWrapper ? option.DeclaredType : optionValueType;
            optionValue = JsonSerializer.Deserialize(remaining, target, options);
        }
        else
        {
            if (strict)
            {
                foreach (JsonProperty p in element.EnumerateObject())
                {
                    if (p.Name is not WireNames.Type and not WireNames.Data)
                    {
                        throw new JsonException(
                            $"Unknown member '{p.Name}' on basic-option choice {table.Type.Name}.");
                    }
                }
            }
            optionValue = element.TryGetProperty(WireNames.Data, out JsonElement data)
                ? JsonSerializer.Deserialize(data, option.DeclaredType, options)
                : null;
        }
        option.Setter.Invoke(builder, new[] { optionValue });
        return builder.Build();
    }

    /// <summary>Unwraps FieldWithMeta&lt;X&gt;/ReferenceWithMeta&lt;X&gt; (and a nullable
    /// X) to X; returns the input type unchanged otherwise.</summary>
    private static Type UnwrapWrapper(Type type, out bool isWrapper)
    {
        isWrapper = false;
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Meta.FieldWithMeta<>) || definition == typeof(Meta.ReferenceWithMeta<>))
            {
                isWrapper = true;
                Type argument = type.GetGenericArguments()[0];
                return Nullable.GetUnderlyingType(argument) ?? argument;
            }
        }
        return type;
    }

    private static RunePropertyTable.Entry? FindChoiceOption(RunePropertyTable table, Type resolved) =>
        FindChoiceOption(table, resolved, new HashSet<Type>());

    private static RunePropertyTable.Entry? FindChoiceOption(RunePropertyTable table, Type resolved, HashSet<Type> visited)
    {
        if (!visited.Add(table.Type))
        {
            return null;
        }
        // Exact match only, as in Java: assignability would bind subtypes to base-typed options.
        RunePropertyTable.Entry? direct = table.Entries.FirstOrDefault(
            e => UnwrapWrapper(e.DeclaredType, out _) == resolved);
        if (direct != null)
        {
            return direct;
        }
        return table.Entries.FirstOrDefault(e =>
        {
            Type inner = UnwrapWrapper(e.DeclaredType, out _);
            return typeof(IRosettaModelObject).IsAssignableFrom(inner)
                && RunePropertyTable.For(inner).IsChoiceType
                && FindChoiceOption(RunePropertyTable.For(inner), resolved, visited) != null;
        });
    }

    private static object? ReadEntryValue(JsonElement value, RunePropertyTable.Entry entry, JsonSerializerOptions options)
    {
        if (entry.ListElementType != null)
        {
            Type listType = typeof(List<>).MakeGenericType(entry.ListElementType);
            return JsonSerializer.Deserialize(value, listType, options);
        }
        return JsonSerializer.Deserialize(value, entry.DeclaredType, options);
    }

    internal static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string s => s.Length == 0,
        System.Collections.ICollection c => c.Count == 0,
        _ => false,
    };

    /// <summary>Reflection helper for routing inline meta keys into a builder's SetMeta.</summary>
    private static class MethodInfoCache
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.MethodInfo?> SetMetaCache = new();

        internal static void SetMeta(IRosettaModelObjectBuilder builder, MetaFields meta)
        {
            System.Reflection.MethodInfo? setter = SetMetaCache.GetOrAdd(builder.GetType(), t =>
                t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetMeta"
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(MetaFields)));
            if (setter == null)
            {
                throw new JsonException($"Builder {builder.GetType()} has no SetMeta(MetaFields).");
            }
            setter.Invoke(builder, new object[] { meta });
        }
    }
}
