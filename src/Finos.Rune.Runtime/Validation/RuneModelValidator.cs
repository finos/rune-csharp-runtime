namespace Finos.Rune.Runtime.Validation;

/// <summary>Validates complete Rune model object graphs.</summary>
public interface IRuneModelValidator
{
    /// <summary>Validates <paramref name="instance"/> with the supplied options.</summary>
    ValidationReport Validate<T>(T instance, RuneValidationOptions? options = null)
        where T : class, IRosettaModelObject;

    /// <summary>Validates and throws when anything failed.</summary>
    void ValidateOrThrow<T>(T instance, RuneValidationOptions? options = null)
        where T : class, IRosettaModelObject;
}

/// <summary>Default graph validator backed by an injected generated-validator factory.</summary>
public sealed class RuneModelValidator : IRuneModelValidator
{
    private readonly IValidatorFactory _factory;

    /// <summary>Creates a graph validator using <paramref name="factory"/>.</summary>
    public RuneModelValidator(IValidatorFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public ValidationReport Validate<T>(T instance, RuneValidationOptions? options = null)
        where T : class, IRosettaModelObject =>
        RuneValidation.Validate(instance, _factory, options ?? RuneValidationOptions.Default);

    /// <inheritdoc />
    public void ValidateOrThrow<T>(T instance, RuneValidationOptions? options = null)
        where T : class, IRosettaModelObject
    {
        ValidationReport report = Validate(instance, options);
        if (!report.Success)
        {
            throw new RuneValidationException(report);
        }
    }
}
