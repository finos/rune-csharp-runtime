namespace Finos.Rune.Runtime.Functions;

/// <summary>Validates model objects returned by Rune functions after their builders are built.</summary>
public interface IModelObjectValidator
{
    /// <summary>Validates one built model object.</summary>
    void Validate<T>(T value)
        where T : class, IRosettaModelObject;

    /// <summary>Validates a list of built model objects.</summary>
    void Validate<T>(IReadOnlyList<T> values)
        where T : class, IRosettaModelObject;
}

/// <summary>Java's default function-output policy, which performs no validation.</summary>
public sealed class NoOpModelObjectValidator : IModelObjectValidator
{
    /// <inheritdoc />
    public void Validate<T>(T value)
        where T : class, IRosettaModelObject
    {
    }

    /// <inheritdoc />
    public void Validate<T>(IReadOnlyList<T> values)
        where T : class, IRosettaModelObject
    {
    }
}

/// <summary>Thrown by a configured model-output validator when a function result is invalid.</summary>
public sealed class ModelObjectValidationException : Exception
{
    /// <summary>Creates an exception carrying the validation error text.</summary>
    public ModelObjectValidationException(string errors)
        : base(errors)
    {
        Errors = errors;
    }

    /// <summary>The validation errors reported by the policy.</summary>
    public string Errors { get; }
}
