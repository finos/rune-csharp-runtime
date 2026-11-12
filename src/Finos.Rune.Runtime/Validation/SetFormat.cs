namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Renders a collection the way Java's <c>AbstractCollection.toString()</c> does, for the
/// only-exists failure message.
/// </summary>
/// <remarks>
/// <para>The message is built with <c>String.format("[%s] should only be set.  Set fields: %s",
/// fields, setFields)</c>, so the first placeholder ends up doubly bracketed — the format string
/// supplies one pair and the set's own <c>toString</c> the other. Two spaces after the full stop
/// are Java's too. Both are reproduced rather than tidied.</para>
/// <para>Element order is whatever the underlying set iterates in, which neither runtime
/// specifies. Only-exists results never reach the validation walk, so this text is not on the
/// parity path; it becomes observable when the expression tier starts emitting
/// <c>only exists</c>.</para>
/// </remarks>
public static class SetFormat
{
    /// <summary>Renders as <c>[a, b]</c>.</summary>
    public static string Describe(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return "[" + string.Join(", ", items) + "]";
    }
}
