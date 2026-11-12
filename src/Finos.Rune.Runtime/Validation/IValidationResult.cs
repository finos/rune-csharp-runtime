using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// One check's outcome at one location in the object tree.
/// Mirrors com.rosetta.model.lib.validation.ValidationResult.
/// </summary>
public interface IValidationResult
{
    /// <summary>True when the check passed.</summary>
    bool IsSuccess { get; }

    /// <summary>Rune name of the type the check ran on.</summary>
    string ModelObjectName { get; }

    /// <summary>Name of the check: the condition name, or the type name for structural checks.</summary>
    string Name { get; }

    /// <summary>Which kind of check this was.</summary>
    ValidationType ValidationType { get; }

    /// <summary>Source text of the condition, or empty for structural checks.</summary>
    string Definition { get; }

    /// <summary>Why the check failed, or null when it passed.</summary>
    string? FailureReason { get; }

    /// <summary>Where in the object tree the check ran.</summary>
    RosettaPath? Path { get; }
}
