namespace Finos.Rune.Runtime.Serialization;

/// <summary>
/// Serialized property names of the standardized Rune JSON format.
/// Mirrors com.rosetta.model.lib.annotations.SerializedNameConstants (rune-dsl).
/// </summary>
public static class WireNames
{
    public const string Data = "@data";
    public const string Reference = "@ref";
    public const string ExternalReference = "@ref:external";
    public const string ScopedReference = "@ref:scoped";
    public const string Meta = "meta";
    public const string Scheme = "@scheme";
    public const string Key = "@key";
    public const string ExternalKey = "@key:external";
    public const string ScopedKey = "@key:scoped";
    public const string Type = "@type";
    public const string Model = "@model";
    public const string Version = "@version";
}
