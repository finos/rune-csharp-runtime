using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Metadata companion of <see cref="Key"/>. Unlike every other companion this one is hand-written
/// in the Java runtime too (com.rosetta.model.lib.meta.Key's anonymous validator), and it is the
/// only source of <see cref="ValidationType.Key"/> results.
/// </summary>
public sealed class KeyMeta : RosettaMetaData<Key>
{
    /// <inheritdoc />
    public override IValidator<Key>? Validator(IValidatorFactory factory) => KeyValidator.Instance;
}

/// <summary>
/// Requires a key to carry both a value and a scope. Mirrors the anonymous Validator inside
/// com.rosetta.model.lib.meta.Key.KeyMeta, including its result names.
/// </summary>
public sealed class KeyValidator : IValidator<Key>
{
    /// <summary>Stateless, so one instance serves every call.</summary>
    public static KeyValidator Instance { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<IValidationResult> GetValidationResults(RosettaPath? path, Key? o)
    {
        return [GetResult(path, o)];
    }

    private static IValidationResult GetResult(RosettaPath? path, Key? key)
    {
        if (key?.KeyValue == null)
        {
            return ValidationResult.Failure(
                "Key.value", ValidationType.Key, "Key", path, string.Empty, "Key value must be set");
        }
        if (key.Scope == null)
        {
            return ValidationResult.Failure(
                "Key.scope", ValidationType.Key, "Key", path, string.Empty, "Key scope must be set");
        }
        return ValidationResult.Success("Key", ValidationType.Key, "Key", path, string.Empty);
    }
}
