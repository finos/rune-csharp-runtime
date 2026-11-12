using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Non-generic face of a type's metadata companion, so the walker can collect results without
/// knowing the model type.
/// </summary>
public interface IRosettaMetaData
{
    /// <summary>
    /// Appends this type's results for <paramref name="instance"/> at <paramref name="path"/>.
    /// </summary>
    void CollectValidationResults(
        RosettaPath? path, object instance, IValidatorFactory factory, List<IValidationResult> sink);
}

/// <summary>
/// A type's validators, as its generated <c>&lt;T&gt;Meta</c> companion supplies them.
/// Mirrors com.rosetta.model.lib.meta.RosettaMetaData.
/// </summary>
/// <remarks>
/// Java's <c>getQualifyFunctions</c> is not part of this contract. Qualification is registered
/// separately in the C# emission, because only two CDM types have any qualify functions and
/// carrying the hook on all 775 companions would be dead weight.
/// </remarks>
public interface IRosettaMetaData<T> : IRosettaMetaData
    where T : class
{
    /// <summary>Data rules from this type and every super type, top-most first.</summary>
    IReadOnlyList<IValidator<T>> DataRules(IValidatorFactory factory);

    /// <summary>The cardinality validator, or null when the type has none.</summary>
    IValidator<T>? Validator(IValidatorFactory factory);

    /// <summary>The type-format validator, or null when the type has none.</summary>
    IValidator<T>? TypeFormatValidator(IValidatorFactory factory);

    /// <summary>
    /// The only-exists validator. Never invoked by the tree walk — generated
    /// <c>only exists</c> expressions call it directly.
    /// </summary>
    IValidatorWithArg<T, ISet<string>>? OnlyExistsValidator(IValidatorFactory factory);
}

/// <summary>
/// Base for generated metadata companions, defaulting every hook to "nothing".
/// Mirrors com.rosetta.model.lib.meta.BasicRosettaMetaData, which is what the runtime's own
/// wrapper types use — they are walked but contribute no results.
/// </summary>
public abstract class RosettaMetaData<T> : IRosettaMetaData<T>
    where T : class
{
    /// <inheritdoc />
    public virtual IReadOnlyList<IValidator<T>> DataRules(IValidatorFactory factory) => [];

    /// <inheritdoc />
    public virtual IValidator<T>? Validator(IValidatorFactory factory) => null;

    /// <inheritdoc />
    public virtual IValidator<T>? TypeFormatValidator(IValidatorFactory factory) => null;

    /// <inheritdoc />
    public virtual IValidatorWithArg<T, ISet<string>>? OnlyExistsValidator(IValidatorFactory factory) => null;

    /// <inheritdoc />
    /// <remarks>
    /// The order is load-bearing: data rules, then the cardinality validator, then the type-format
    /// validator. It is what RosettaTypeValidator$RosettaTypeProcessor.processRosetta concatenates,
    /// and the parity sweep compares result sequences positionally.
    /// </remarks>
    public void CollectValidationResults(
        RosettaPath? path, object instance, IValidatorFactory factory, List<IValidationResult> sink)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(sink);

        if (instance is not T typed)
        {
            throw new ArgumentException(
                $"Metadata for {typeof(T)} cannot validate a {instance.GetType()}.", nameof(instance));
        }

        foreach (IValidator<T> rule in DataRules(factory))
        {
            sink.AddRange(rule.GetValidationResults(path, typed));
        }

        IValidator<T>? cardinality = Validator(factory);
        if (cardinality != null)
        {
            sink.AddRange(cardinality.GetValidationResults(path, typed));
        }

        IValidator<T>? typeFormat = TypeFormatValidator(factory);
        if (typeFormat != null)
        {
            sink.AddRange(typeFormat.GetValidationResults(path, typed));
        }
    }
}
