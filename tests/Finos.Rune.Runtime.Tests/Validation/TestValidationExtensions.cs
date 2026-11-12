using Finos.Rune.Runtime.Validation;

namespace Finos.Rune.Runtime.Tests.Validation;

internal static class TestValidationExtensions
{
    private static readonly IRuneModelValidator Validator =
        new RuneModelValidator(DefaultValidatorFactory.Instance);

    internal static ValidationReport Validate<T>(this T instance)
        where T : class, IRosettaModelObject =>
        Validator.Validate(instance);

    internal static ValidationReport Validate<T>(this T instance, RuneValidationOptions options)
        where T : class, IRosettaModelObject =>
        Validator.Validate(instance, options);

    internal static void ValidateOrThrow<T>(this T instance)
        where T : class, IRosettaModelObject =>
        Validator.ValidateOrThrow(instance);
}
