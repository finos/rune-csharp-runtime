namespace Finos.Rune.Runtime.Functions;

/// <summary>Container-neutral required-service lookup used by generated expression owners.</summary>
public static class RuneServiceProvider
{
    /// <summary>Resolves <typeparamref name="TService"/> or throws a precise registration error.</summary>
    public static TService GetRequired<TService>(IServiceProvider services)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        object? service = services.GetService(typeof(TService));
        if (service is TService typed)
        {
            return typed;
        }
        throw new InvalidOperationException(
            $"Required service {typeof(TService).FullName} is not registered in the active Rune provider.");
    }
}
