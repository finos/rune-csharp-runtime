namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Which kind of check produced a validation result.
/// Mirrors com.rosetta.model.lib.validation.ValidationResult.ValidationType.
/// </summary>
public enum ValidationType
{
    /// <summary>A Rune <c>condition</c> block.</summary>
    DataRule,

    /// <summary>An attribute's declared cardinality.</summary>
    Cardinality,

    /// <summary>A string length/pattern or number digits/range constraint.</summary>
    TypeFormat,

    /// <summary>Key resolution.</summary>
    Key,

    /// <summary>A Rune <c>only exists</c> expression. Never produced by the tree walk.</summary>
    OnlyExists,

    /// <summary>A pre-processing step threw.</summary>
    PreProcessException,

    /// <summary>A post-processing step threw.</summary>
    PostProcessException,
}

/// <summary>Wire names of <see cref="ValidationType"/>, matching the Java enum constants.</summary>
/// <remarks>
/// Explicit rather than reflected, per the repo rule that enum wire names are generated maps and
/// never reflection magic. These strings appear in the cross-language parity report.
/// </remarks>
public static class ValidationTypeNames
{
    /// <summary>The Java enum constant name for <paramref name="type"/>.</summary>
    public static string ToWireName(this ValidationType type) => type switch
    {
        ValidationType.DataRule => "DATA_RULE",
        ValidationType.Cardinality => "CARDINALITY",
        ValidationType.TypeFormat => "TYPE_FORMAT",
        ValidationType.Key => "KEY",
        ValidationType.OnlyExists => "ONLY_EXISTS",
        ValidationType.PreProcessException => "PRE_PROCESS_EXCEPTION",
        ValidationType.PostProcessException => "POST_PROCESS_EXCEPTION",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown validation type."),
    };

    /// <summary>Parses a Java enum constant name.</summary>
    public static ValidationType FromWireName(string name) => name switch
    {
        "DATA_RULE" => ValidationType.DataRule,
        "CARDINALITY" => ValidationType.Cardinality,
        "TYPE_FORMAT" => ValidationType.TypeFormat,
        "KEY" => ValidationType.Key,
        "ONLY_EXISTS" => ValidationType.OnlyExists,
        "PRE_PROCESS_EXCEPTION" => ValidationType.PreProcessException,
        "POST_PROCESS_EXCEPTION" => ValidationType.PostProcessException,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown validation type name."),
    };
}
