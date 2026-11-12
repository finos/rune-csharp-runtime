using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Finos.Rune.Runtime.Meta;
using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Converter factory for FieldWithMeta&lt;T&gt;: meta unwrapped ("@scheme"/"@key"/...),
/// basic values under "@data", model-object values flattened into the wrapper object.
/// </summary>
public sealed class FieldWithMetaConverterFactory : JsonConverterFactory
{
    private readonly bool _strict;

    public FieldWithMetaConverterFactory(bool strict)
    {
        _strict = strict;
    }

    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(FieldWithMeta<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(FieldWithMetaConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]), new object[] { _strict })!;
}

public sealed class FieldWithMetaConverter<T> : JsonConverter<FieldWithMeta<T>>
{
    private readonly bool _strict;

    public FieldWithMetaConverter(bool strict)
    {
        _strict = strict;
    }

    public override FieldWithMeta<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var builder = FieldWithMeta<T>.CreateBuilder();

        if (element.ValueKind != JsonValueKind.Object)
        {
            if (_strict)
            {
                throw new JsonException(
                    $"Expected an object for FieldWithMeta<{typeof(T).Name}> but found {element.ValueKind}.");
            }
            builder.SetValue(JsonSerializer.Deserialize<T>(element, options));
            return builder.Build();
        }

        MetaFields.Builder? meta = null;
        JsonObject? remaining = null;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case WireNames.Scheme:
                    (meta ??= MetaFields.CreateBuilder()).SetScheme(property.Value.GetString());
                    break;
                case WireNames.Key:
                    (meta ??= MetaFields.CreateBuilder()).SetGlobalKey(property.Value.GetString());
                    break;
                case WireNames.ExternalKey:
                    (meta ??= MetaFields.CreateBuilder()).SetExternalKey(property.Value.GetString());
                    break;
                case WireNames.ScopedKey:
                    (meta ??= MetaFields.CreateBuilder()).SetScopedKey(property.Value.GetString());
                    break;
                case WireNames.Data:
                    builder.SetValue(JsonSerializer.Deserialize<T>(property.Value, options));
                    break;
                default:
                    (remaining ??= new JsonObject())[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                    break;
            }
        }
        if (remaining != null)
        {
            if (!typeof(IRosettaModelObject).IsAssignableFrom(UnwrapNullable(typeof(T))))
            {
                if (_strict)
                {
                    throw new JsonException(
                        $"Unknown members on FieldWithMeta<{typeof(T).Name}>: {string.Join(", ", remaining.Select(p => p.Key))}.");
                }
            }
            else
            {
                builder.SetValue((T?)(object?)JsonSerializer.Deserialize(remaining, UnwrapNullable(typeof(T)), options));
            }
        }
        if (meta != null)
        {
            builder.SetMeta(meta);
        }
        return builder.Build();
    }

    public override void Write(Utf8JsonWriter writer, FieldWithMeta<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        RuneJsonEngine.WriteFieldWithMetaBody(writer, value, options);
        writer.WriteEndObject();
    }

    private static Type UnwrapNullable(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}

/// <summary>
/// Converter factory for ReferenceWithMeta&lt;T&gt;: "@ref" / "@ref:external" /
/// "@ref:scoped" plus the resolved value ("@data" for basics, flattened for model
/// objects).
/// </summary>
public sealed class ReferenceWithMetaConverterFactory : JsonConverterFactory
{
    private readonly bool _strict;

    public ReferenceWithMetaConverterFactory(bool strict)
    {
        _strict = strict;
    }

    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ReferenceWithMeta<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(ReferenceWithMetaConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]), new object[] { _strict })!;
}

public sealed class ReferenceWithMetaConverter<T> : JsonConverter<ReferenceWithMeta<T>>
{
    private readonly bool _strict;

    public ReferenceWithMetaConverter(bool strict)
    {
        _strict = strict;
    }

    public override ReferenceWithMeta<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var builder = ReferenceWithMeta<T>.CreateBuilder();
        JsonObject? remaining = null;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case WireNames.Reference:
                    builder.SetGlobalReference(property.Value.GetString());
                    break;
                case WireNames.ExternalReference:
                    builder.SetExternalReference(property.Value.GetString());
                    break;
                case WireNames.ScopedReference:
                    builder.GetOrCreateScopedReference().SetScopedReference(property.Value.GetString());
                    break;
                case WireNames.Data:
                    builder.SetValue(JsonSerializer.Deserialize<T>(property.Value, options));
                    break;
                default:
                    (remaining ??= new JsonObject())[property.Name] = JsonNode.Parse(property.Value.GetRawText());
                    break;
            }
        }
        if (remaining != null)
        {
            Type inner = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (!typeof(IRosettaModelObject).IsAssignableFrom(inner))
            {
                if (_strict)
                {
                    throw new JsonException(
                        $"Unknown members on ReferenceWithMeta<{typeof(T).Name}>: {string.Join(", ", remaining.Select(p => p.Key))}.");
                }
            }
            else
            {
                builder.SetValue((T?)(object?)JsonSerializer.Deserialize(remaining, inner, options));
            }
        }
        return builder.Build();
    }

    public override void Write(Utf8JsonWriter writer, ReferenceWithMeta<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        RuneJsonEngine.WriteReferenceWithMetaBody(writer, value, options);
        writer.WriteEndObject();
    }
}
