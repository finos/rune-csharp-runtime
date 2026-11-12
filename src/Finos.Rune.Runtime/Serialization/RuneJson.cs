using System.Reflection;
using System.Text;
using System.Text.Json;
using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Entry point for the standardized Rune JSON format: configured options, root-level
/// "@model"/"@type"/"@version" headers on write, and post-read pruning for round-trip
/// idempotence — mirroring rune-common's RuneJsonObjectMapper.
/// </summary>
public static class RuneJson
{
    /// <summary>Nesting limit for every parse of a Rune document — the deserializer,
    /// root-type resolution, and tree comparisons must all share it (Jackson's default
    /// is ~1000; STJ's default 64 is too shallow for deep CDM documents).</summary>
    public const int MaxDepth = 512;

    /// <summary>Options for the standardized format. Strict mode fails on unknown
    /// members (the M2 conformance setting); the Java reference itself is lenient.</summary>
    public static JsonSerializerOptions CreateOptions(bool strict = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            // Jackson does not escape '+', '<', '&' or non-ASCII; match its output.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            MaxDepth = RuneJson.MaxDepth,
        };
        options.Converters.Add(new FieldWithMetaConverterFactory(strict));
        options.Converters.Add(new ReferenceWithMetaConverterFactory(strict));
        options.Converters.Add(new RuneModelConverterFactory(strict));
        options.Converters.Add(new RuneDateOnlyConverter());
        options.Converters.Add(new RuneTimeOnlyConverter());
        options.Converters.Add(new RuneDateTimeConverter());
        options.Converters.Add(new RuneDateTimeOffsetConverter());
        return options;
    }

    /// <summary>Serializes a root model object with the "@model"/"@type"/"@version"
    /// headers, pruning empty subtrees first (Java runs its pre-serialization processor
    /// on every root write).</summary>
    /// <remarks>
    /// The pre-serialization work happens on a copy, never on the caller's graph — which is what lets a
    /// resolved reference keep its value in memory while serializing as a bare reference. Java's
    /// pre-processor has the same shape: it takes <c>toBuilder()</c> and mutates that.
    /// </remarks>
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null) where T : class, IRosettaModelObject
    {
        options ??= CreateOptions();
        IRosettaModelObjectBuilder pruned = PostProcess.ReferencePruning.PruneForSerialization(value).ToBuilder();
        pruned.Prune();
        IRosettaModelObject root = pruned.Build();

        RuneDataTypeAttribute? identity = root.GetType().GetCustomAttribute<RuneDataTypeAttribute>();
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = options.WriteIndented,
            Encoder = options.Encoder,
        }))
        {
            writer.WriteStartObject();
            if (identity != null)
            {
                writer.WriteString(WireNames.Model, identity.Model);
                writer.WriteString(WireNames.Type, identity.QualifiedName);
                writer.WriteString(WireNames.Version, identity.Version);
            }
            RuneJsonEngine.WriteModelBody(writer, root, root.GetType(), options);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Deserializes a root model object and prunes it (the Java reference
    /// prunes after reading, which is what makes round trips idempotent).</summary>
    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null) where T : class, IRosettaModelObject
        => (T?)Deserialize(json, typeof(T), options);

    /// <summary>Non-generic variant for callers resolving the root type from "@type".</summary>
    public static IRosettaModelObject? Deserialize(string json, Type type, JsonSerializerOptions? options = null)
    {
        options ??= CreateOptions();
        var raw = (IRosettaModelObject?)JsonSerializer.Deserialize(json, type, options);
        if (raw == null)
        {
            return null;
        }
        IRosettaModelObjectBuilder builder = raw.ToBuilder();
        builder.Prune();
        return builder.Build();
    }

    /// <summary>Resolves the root CLR type of a Rune JSON document from its "@type"
    /// header, scanning <paramref name="modelAssembly"/> for [RuneDataType] types.</summary>
    public static Type? ResolveRootType(string json, System.Reflection.Assembly modelAssembly)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxDepth });
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(WireNames.Type, out JsonElement typeElement))
        {
            return RuneTypeRegistry.Resolve(typeElement.GetString()!, modelAssembly);
        }
        return null;
    }
}
