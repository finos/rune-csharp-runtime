using Finos.Rune.Runtime.Functions;
using Xunit;

namespace Finos.Rune.Runtime.Tests.Functions;

public class RuneFunctionTests
{
    [Fact]
    public void Resolve_WithARegisteredFunction_ReturnsTheActiveInstance()
    {
        var dependency = new DependencyFunction(NullServiceProvider.Instance);
        var provider = new SingleServiceProvider(typeof(DependencyFunction), dependency);
        var function = new ResolvingFunction(provider);

        Assert.Same(dependency, function.Resolve());
    }

    [Fact]
    public void Resolve_WithoutARegisteredFunction_ThrowsNamingTheMissingType()
    {
        var function = new ResolvingFunction(NullServiceProvider.Instance);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => function.Resolve());

        Assert.Contains(typeof(DependencyFunction).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WithANullProvider_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new ResolvingFunction(null!));

    private sealed class ResolvingFunction(IServiceProvider services) : RuneFunction(services)
    {
        public DependencyFunction Resolve() => GetRequiredFunction<DependencyFunction>();
    }

    private sealed class DependencyFunction(IServiceProvider services) : RuneFunction(services)
    {
    }

    private sealed class SingleServiceProvider(Type serviceType, object service) : IServiceProvider
    {
        public object? GetService(Type requestedType) => requestedType == serviceType ? service : null;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static NullServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
