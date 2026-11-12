using System.Globalization;

namespace Finos.Rune.Runtime.Expression;

/// <summary>Strict lexical conversions used by Rune's <c>to-*</c> operators.</summary>
public static class RuneConvert
{
    /// <summary>Java <c>Integer.parseInt</c> semantics over a decimal string.</summary>
    /// <remarks>
    /// Whitespace is not accepted, and both malformed text and overflow surface as
    /// <see cref="FormatException"/> so a generated mapper can treat either as an absent conversion.
    /// </remarks>
    public static int ParseInt(string value)
    {
        try
        {
            return int.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        catch (OverflowException exception)
        {
            throw new FormatException(exception.Message, exception);
        }
    }
}
