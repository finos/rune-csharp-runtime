namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Supplies validator instances to the generated metadata companions.
/// Mirrors com.rosetta.model.lib.validation.ValidatorFactory.
/// </summary>
/// <remarks>
/// In Java this is a Guice indirection. Generated distributions register a provider-backed
/// implementation so validators can receive replaceable Rune functions.
/// </remarks>
public interface IValidatorFactory
{
    /// <summary>Returns the instance to use for <typeparamref name="TValidator"/>.</summary>
    TValidator Create<TValidator>()
        where TValidator : class;
}

/// <summary>Creates parameterless validators without a service provider.</summary>
public sealed class DefaultValidatorFactory : IValidatorFactory
{
    /// <summary>The shared fallback for container-free runtime models and focused tests.</summary>
    public static DefaultValidatorFactory Instance { get; } = new();

    /// <inheritdoc />
    public TValidator Create<TValidator>()
        where TValidator : class =>
        Activator.CreateInstance<TValidator>()
        ?? throw new InvalidOperationException($"Could not construct validator {typeof(TValidator).FullName}.");
}

/// <summary>Resolves every generated validator from an application-owned service provider.</summary>
public sealed class ServiceProviderValidatorFactory : IValidatorFactory
{
    private readonly IServiceProvider _services;

    /// <summary>Creates a provider-backed validator factory.</summary>
    public ServiceProviderValidatorFactory(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public TValidator Create<TValidator>()
        where TValidator : class
    {
        object? service = _services.GetService(typeof(TValidator));
        if (service is TValidator validator)
        {
            return validator;
        }
        throw new InvalidOperationException(
            $"Validator {typeof(TValidator).FullName} is not registered in the active Rune validation provider.");
    }
}

/// <summary>Returns caller-supplied validator replacements before consulting a fallback factory.</summary>
public sealed class SubstitutingValidatorFactory : IValidatorFactory
{
    private readonly Dictionary<Type, object> _overrides = [];
    private readonly IValidatorFactory _fallback;

    /// <summary>Creates a substituting factory over <paramref name="fallback"/>.</summary>
    public SubstitutingValidatorFactory(IValidatorFactory? fallback = null)
    {
        _fallback = fallback ?? DefaultValidatorFactory.Instance;
    }

    /// <summary>Uses <paramref name="instance"/> whenever <typeparamref name="TValidator"/> is requested.</summary>
    public SubstitutingValidatorFactory Substitute<TValidator>(TValidator instance)
        where TValidator : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _overrides[typeof(TValidator)] = instance;
        return this;
    }

    /// <inheritdoc />
    public TValidator Create<TValidator>()
        where TValidator : class =>
        _overrides.TryGetValue(typeof(TValidator), out object? substitute)
            ? (TValidator)substitute
            : _fallback.Create<TValidator>();
}
