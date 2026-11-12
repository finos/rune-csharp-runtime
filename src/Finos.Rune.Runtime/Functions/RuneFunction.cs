namespace Finos.Rune.Runtime.Functions;

/// <summary>Marker implemented by every generated Rune function contract.</summary>
public interface IRuneFunction
{
}

/// <summary>
/// Base for generated Rune functions, retaining the provider that created the active function graph.
/// </summary>
public abstract class RuneFunction : IRuneFunction
{
    /// <summary>Creates a function whose dependencies resolve from <paramref name="services"/>.</summary>
    protected RuneFunction(IServiceProvider services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>The provider that created this function instance.</summary>
    protected IServiceProvider Services { get; }

    /// <summary>Resolves a required Rune function from the active service provider.</summary>
    protected TFunction GetRequiredFunction<TFunction>()
        where TFunction : class, IRuneFunction =>
        GetRequiredService<TFunction>();

    /// <summary>The configured function-condition policy.</summary>
    protected IConditionValidator ConditionValidator =>
        GetRequiredService<IConditionValidator>();

    /// <summary>The configured model-output validation policy.</summary>
    protected IModelObjectValidator ModelObjectValidator =>
        GetRequiredService<IModelObjectValidator>();

    /// <summary>Resolves a required service without taking a dependency on a particular DI container.</summary>
    protected TService GetRequiredService<TService>()
        where TService : class =>
        RuneServiceProvider.GetRequired<TService>(Services);
}
