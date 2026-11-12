using System.Globalization;
using System.Text.RegularExpressions;
using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// A validation result produced by a model check.
/// Mirrors com.rosetta.model.lib.validation.ValidationResult.ModelValidationResult.
/// </summary>
public sealed class ValidationResult : IValidationResult
{
    private readonly string? _rawFailureReason;

    private ValidationResult(
        string name,
        ValidationType validationType,
        string modelObjectName,
        RosettaPath? path,
        string definition,
        string? failureReason)
    {
        Name = name;
        ValidationType = validationType;
        ModelObjectName = modelObjectName;
        Path = path;
        Definition = definition;
        _rawFailureReason = failureReason;
    }

    /// <summary>A passing result.</summary>
    public static ValidationResult Success(
        string name, ValidationType validationType, string modelObjectName, RosettaPath? path, string definition) =>
        new(name, validationType, modelObjectName, path, definition, null);

    /// <summary>A failing result carrying the reason.</summary>
    public static ValidationResult Failure(
        string name,
        ValidationType validationType,
        string modelObjectName,
        RosettaPath? path,
        string definition,
        string failureMessage) =>
        new(name, validationType, modelObjectName, path, definition, failureMessage);

    /// <inheritdoc />
    public bool IsSuccess => _rawFailureReason == null;

    /// <inheritdoc />
    public string ModelObjectName { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public ValidationType ValidationType { get; }

    /// <inheritdoc />
    public string Definition { get; }

    /// <inheritdoc />
    public RosettaPath? Path { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Data-rule failures on a type whose name ends in <c>Report</c> get rewritten into a terser
    /// form. Java calls this "an ugly hack" in a TODO and it is reproduced only for parity; on CDM
    /// 7.0.0 it never fires, because the single type ending in <c>Report</c>
    /// (<c>TradePricingReport</c>) has no conditions.
    /// </remarks>
    public string? FailureReason
    {
        get
        {
            if (_rawFailureReason != null
                && ModelObjectName.EndsWith("Report", StringComparison.Ordinal)
                && ValidationType == ValidationType.DataRule)
            {
                return BuildReportFailureReason();
            }
            return _rawFailureReason;
        }
    }

    private string BuildReportFailureReason()
    {
        // Not escaped on purpose: Java treats the type name as a regex too.
        string conditionName = new Regex(ModelObjectName).Replace(Name, string.Empty, 1);
        string failReason = _rawFailureReason!;
        failReason = Regex.Replace(failReason, ModelObjectName, string.Empty);
        failReason = failReason.Replace("->get", " ", StringComparison.Ordinal);
        failReason = Regex.Replace(failReason, "[^\\w-]+", " ");
        failReason = Regex.Replace(failReason, "^\\s+", string.Empty);
        return conditionName + ":- " + failReason;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        string outcome = IsSuccess ? "SUCCESS" : "FAILURE";
        string because = FailureReason == null ? string.Empty : "because [" + FailureReason + "]";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Validation {0} on [{1}] for [{2}] [{3}] {4}",
            outcome,
            Path?.BuildPath(),
            ValidationType.ToWireName(),
            Name,
            because);
    }
}

/// <summary>
/// A failure raised by a processing step rather than by a model check.
/// Mirrors com.rosetta.model.lib.validation.ValidationResult.ProcessValidationResult.
/// </summary>
public sealed class ProcessValidationResult : IValidationResult
{
    /// <summary>Creates a process failure at <paramref name="path"/>.</summary>
    public ProcessValidationResult(string message, string modelObjectName, string processorName, RosettaPath? path)
    {
        FailureReason = message;
        ModelObjectName = modelObjectName;
        Name = processorName;
        Path = path;
    }

    /// <inheritdoc />
    public bool IsSuccess => false;

    /// <inheritdoc />
    public string ModelObjectName { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public ValidationType ValidationType => ValidationType.PostProcessException;

    /// <inheritdoc />
    public string Definition => string.Empty;

    /// <inheritdoc />
    public string? FailureReason { get; }

    /// <inheritdoc />
    public RosettaPath? Path { get; }
}
