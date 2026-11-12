namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Every result produced by validating one object graph, in traversal order.
/// Mirrors com.regnosys.rosetta.common.validation.ValidationReport.
/// </summary>
public sealed class ValidationReport
{
    /// <summary>Creates a report over <paramref name="results"/>, which is taken as-is.</summary>
    public ValidationReport(object? resultObject, IReadOnlyList<IValidationResult> results)
    {
        ResultObject = resultObject;
        Results = results ?? throw new ArgumentNullException(nameof(results));
    }

    /// <summary>The object that was validated.</summary>
    public object? ResultObject { get; }

    /// <summary>All results in traversal order, successes included unless they were suppressed.</summary>
    public IReadOnlyList<IValidationResult> Results { get; }

    /// <summary>True when nothing failed.</summary>
    public bool Success => !Results.Any(r => !r.IsSuccess);

    /// <summary>The failures, in traversal order.</summary>
    public IEnumerable<IValidationResult> Failures => Results.Where(r => !r.IsSuccess);

    /// <summary>One line per failure, for logging or an exception message.</summary>
    public string DescribeFailures() =>
        string.Join(Environment.NewLine, Failures.Select(f => f.ToString()));
}

/// <summary>How a validation run should behave.</summary>
public sealed class RuneValidationOptions
{
    /// <summary>Java-equivalent defaults: real validators, successes materialized.</summary>
    public static RuneValidationOptions Default { get; } = new();

    /// <summary>
    /// When false, only failures are kept. Java always materializes successes, so the
    /// cross-language parity sweep must run with this true; callers who only want to know what is
    /// wrong can turn it off and skip allocating the (much larger) success set.
    /// </summary>
    public bool IncludeSuccesses { get; init; } = true;
}

/// <summary>Thrown when an <see cref="IRuneModelValidator"/> rejects a model graph.</summary>
public sealed class RuneValidationException : Exception
{
    /// <summary>Creates an exception describing <paramref name="report"/>'s failures.</summary>
    public RuneValidationException(ValidationReport report)
        : base(BuildMessage(report))
    {
        Report = report;
    }

    /// <summary>Creates an exception with an explicit message.</summary>
    public RuneValidationException(string message)
        : base(message)
    {
        Report = new ValidationReport(null, []);
    }

    /// <summary>Creates an exception with an explicit message and cause.</summary>
    public RuneValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Report = new ValidationReport(null, []);
    }

    /// <summary>The report that failed.</summary>
    public ValidationReport Report { get; }

    private static string BuildMessage(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        int count = report.Failures.Count();
        return $"Validation failed with {count} error(s):{Environment.NewLine}{report.DescribeFailures()}";
    }
}
