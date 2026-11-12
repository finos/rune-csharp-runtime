using System.Globalization;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// Constructs the DSL's record types from their parts.
/// </summary>
/// <remarks>
/// <para>The counterpart of reading a record feature (<c>startDate -&gt; year</c>): the DSL lets a record be
/// built as well as navigated, with <c>zonedDateTime { date: …, time: …, timezone: … }</c>. Only that one
/// appears in the corpus, exactly once, in <c>ToDateTime</c> — so <c>date { … }</c> and
/// <c>dateTime { … }</c> are absent rather than guessed at, and the generator refuses them by name.</para>
/// <para>Absent in, absent out, as elsewhere in the expression tier.</para>
/// </remarks>
public static class RuneRecord
{
    /// <summary>Builds a zoned date-time from a date, a time and a timezone designator.</summary>
    /// <remarks>
    /// <para>A Rune <c>zonedDateTime</c> maps to <see cref="DateTimeOffset"/>, so the timezone has to reduce
    /// to a fixed offset. <c>Z</c> and an explicit <c>±hh:mm</c> both do. A named zone (<c>Europe/London</c>)
    /// does not — its offset depends on the instant — and is refused rather than resolved, because picking a
    /// zone database here would produce a defensible-looking answer that the reference, which keeps the zone
    /// itself in a <c>ZonedDateTime</c>, need never agree with.</para>
    /// <para>An absent time means midnight, which is what the only caller passes explicitly anyway.</para>
    /// </remarks>
    public static DateTimeOffset? ZonedDateTime(DateOnly? date, TimeOnly? time, string? timezone)
    {
        if (date == null)
        {
            return null;
        }
        return new DateTimeOffset(
            date.Value.ToDateTime(time ?? TimeOnly.MinValue), ParseOffset(timezone));
    }

    private static TimeSpan ParseOffset(string? timezone)
    {
        if (string.IsNullOrEmpty(timezone) || timezone is "Z" or "z")
        {
            return TimeSpan.Zero;
        }
        string text = timezone;
        int sign = text[0] switch
        {
            '+' => 1,
            '-' => -1,
            _ => throw new NotSupportedException(
                $"Timezone '{timezone}' is not a fixed offset. A Rune zonedDateTime maps to a "
                + "DateTimeOffset, which cannot hold a named zone whose offset varies by instant."),
        };
        text = text[1..].Replace(":", string.Empty);
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int digits)
            || text.Length is not (2 or 4))
        {
            throw new FormatException("Not a timezone offset: " + timezone);
        }
        int hours = text.Length == 2 ? digits : digits / 100;
        int minutes = text.Length == 2 ? 0 : digits % 100;
        return new TimeSpan(sign * hours, sign * minutes, 0);
    }
}
