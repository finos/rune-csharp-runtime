using System.Collections;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Whether a field counts as set, for <c>exists</c> and only-exists checks.
/// Mirrors com.rosetta.model.lib.validation.ExistenceChecker.
/// </summary>
public static class ExistenceChecker
{
    /// <summary>
    /// True when the field holds something.
    /// </summary>
    /// <remarks>
    /// A list counts as set only when it has at least one non-null element, so an empty list and a
    /// list of nulls are both absent. A builder counts as set only when
    /// <c>HasData()</c> — a builder whose every field is empty is absent, which is what makes
    /// <c>Prune()</c> and <c>exists</c> agree. Everything else non-null is set.
    /// </remarks>
    public static bool IsSet(object? field)
    {
        if (field == null)
        {
            return false;
        }
        if (field is IRosettaModelObjectBuilder builder)
        {
            return builder.HasData();
        }
        if (field is string)
        {
            // Guard before IEnumerable: a string is a char sequence, not a Rune list.
            return true;
        }
        if (field is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item != null)
                {
                    return true;
                }
            }
            return false;
        }
        return true;
    }
}
