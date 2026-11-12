using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// A check over one model object. Generated cardinality, type-format and data-rule validators all
/// implement this. Mirrors com.rosetta.model.lib.validation.Validator.
/// </summary>
/// <remarks>
/// Contravariant so a validator for a base type satisfies a slot typed on a subtype — the C#
/// equivalent of Java's <c>Validator&lt;? super T&gt;</c>, which the generated metadata companions
/// use throughout.
/// </remarks>
public interface IValidator<in T>
{
    /// <summary>Runs the check and returns one result per sub-check.</summary>
    IReadOnlyList<IValidationResult> GetValidationResults(RosettaPath? path, T? objectToBeValidated);
}

/// <summary>
/// A check taking an extra argument. Only the generated only-exists validators use this, with the
/// argument being the set of attribute names required to be the only ones set.
/// Mirrors com.rosetta.model.lib.validation.ValidatorWithArg.
/// </summary>
public interface IValidatorWithArg<in T, in TArg>
{
    /// <summary>Runs the check against <paramref name="arg"/>.</summary>
    IValidationResult Validate(RosettaPath? path, T? objectToBeValidated, TArg arg);
}
