using System.Globalization;

namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Lexical conversion between Rune scalar values and their XSD spellings. Everything here is
/// culture-invariant, and every number is written in plain notation — never exponential — because
/// XSD lexical space and the Java reference both write decimals that way.
/// </summary>
public static class XmlLexicalValue
{
    private static readonly string[] DateTimeFormats =
    {
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm",
    };

    private static readonly string[] DateFormats = { "yyyy-MM-dd" };

    private static readonly string[] TimeFormats = { "HH:mm:ss", "HH:mm:ss.FFFFFFF", "HH:mm" };

    /// <summary>Writes a decimal in plain invariant notation.</summary>
    public static string FormatDecimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static decimal ParseDecimal(string text) =>
        decimal.Parse(Require(text), NumberStyles.Float, CultureInfo.InvariantCulture);

    public static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static int ParseInt(string text) =>
        int.Parse(Require(text), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static string FormatLong(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static long ParseLong(string text) =>
        long.Parse(Require(text), NumberStyles.Integer, CultureInfo.InvariantCulture);

    /// <summary>XSD booleans are written <c>true</c>/<c>false</c>.</summary>
    public static string FormatBoolean(bool value) => value ? "true" : "false";

    public static bool ParseBoolean(string text) => Require(text).Trim() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new FormatException("Invalid xsd:boolean '" + text + "'."),
    };

    public static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Parses <c>xsd:date</c>, tolerating and discarding an optional timezone suffix.</summary>
    public static DateOnly ParseDate(string text)
    {
        string body = SplitZone(Require(text).Trim(), out _);
        return DateOnly.TryParseExact(body, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value)
            ? value
            : throw new FormatException("Invalid xsd:date '" + text + "'.");
    }

    /// <summary>
    /// Writes <c>xsd:time</c> with a trailing <c>Z</c>, which is the spelling the Java reference
    /// produces for a zone-less <c>LocalTime</c>.
    /// </summary>
    public static string FormatTime(TimeOnly value) => FormatTimeBody(value) + "Z";

    /// <summary>
    /// Parses <c>xsd:time</c>. A trailing offset is normalised away, so <c>03:23:45+02:00</c> and
    /// <c>01:23:45Z</c> both read as 01:23:45 — matching the Java reference.
    /// </summary>
    public static TimeOnly ParseTime(string text)
    {
        string body = SplitZone(Require(text).Trim(), out XmlZoneMetadata zone);
        if (!TimeOnly.TryParseExact(body, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly value))
        {
            throw new FormatException("Invalid xsd:time '" + text + "'.");
        }
        return zone.Kind == XmlTimeZoneKind.Offset ? value.Add(-zone.Offset) : value;
    }

    /// <summary>Writes a zone-less <c>xsd:dateTime</c>.</summary>
    public static string FormatDateTime(DateTime value) => FormatDateTimeBody(value);

    public static DateTime ParseDateTime(string text)
    {
        string body = SplitZone(Require(text).Trim(), out _);
        if (DateTime.TryParseExact(body, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value))
        {
            return value;
        }
        if (DateOnly.TryParseExact(body, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return date.ToDateTime(TimeOnly.MinValue);
        }
        throw new FormatException("Invalid xsd:dateTime '" + text + "'.");
    }

    /// <summary>
    /// Parses an <c>xsd:dateTime</c> whose timezone is optional, returning both the instant and the
    /// exact lexical timezone that was written so a write can reproduce it.
    /// </summary>
    public static DateTimeOffset ParseZonedDateTime(string text, out XmlZoneMetadata zone)
    {
        string body = SplitZone(Require(text).Trim(), out zone);
        // Java normalizes either zero offset, including -00:00, to Z.
        if (zone.Kind == XmlTimeZoneKind.Offset && zone.Offset == TimeSpan.Zero)
        {
            zone = XmlZoneMetadata.Utc;
        }
        DateTime local;
        if (DateTime.TryParseExact(body, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            local = parsed;
        }
        else if (DateOnly.TryParseExact(body, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            local = date.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            throw new FormatException("Invalid xsd:dateTime '" + text + "'.");
        }
        return new DateTimeOffset(local, zone.Offset);
    }

    /// <summary>
    /// Writes an <c>xsd:dateTime</c> back in the lexical timezone form it was read with. A zone-less
    /// value round-trips without gaining a <c>Z</c>, and <c>+01</c> does not become <c>+01:00</c>.
    /// </summary>
    public static string FormatZonedDateTime(DateTimeOffset value, XmlZoneMetadata zone)
    {
        string body = FormatDateTimeBody(value.DateTime);
        return zone.Kind switch
        {
            XmlTimeZoneKind.Absent => body,
            XmlTimeZoneKind.Utc => body + "Z",
            _ => body + (zone.Lexical.Length == 0 ? XmlZoneMetadata.FormatOffset(value.Offset) : zone.Lexical),
        };
    }

    /// <summary>
    /// Writes an <c>xsd:dateTime</c> with no recorded lexical zone: <c>Z</c> for a zero offset,
    /// otherwise the canonical offset spelling.
    /// </summary>
    public static string FormatZonedDateTime(DateTimeOffset value) => FormatZonedDateTime(
        value,
        value.Offset == TimeSpan.Zero ? XmlZoneMetadata.Utc : XmlZoneMetadata.OfOffset(value.Offset));

    /// <summary>
    /// Splits a lexical value into its body and its timezone suffix. Recognises <c>Z</c>,
    /// <c>+HH:mm</c>, <c>+HHmm</c> and <c>+HH</c> in both signs.
    /// </summary>
    /// <remarks>
    /// The scan starts after the date and time body rather than from the end of the string,
    /// because a date's own hyphens are indistinguishable from a negative offset when read
    /// backwards: <c>2006-04-02</c> would otherwise appear to end in the offset <c>-02</c>.
    /// </remarks>
    public static string SplitZone(string text, out XmlZoneMetadata zone)
    {
        ArgumentNullException.ThrowIfNull(text);
        zone = XmlZoneMetadata.Absent;
        if (text.Length == 0)
        {
            return text;
        }

        int bodyEnd;
        if (text.Length >= 10 && text[4] == '-' && text[7] == '-')
        {
            bodyEnd = text.Length > 10 && (text[10] == 'T' || text[10] == 't')
                ? ZoneStart(text, 11)
                : 10;
        }
        else
        {
            bodyEnd = ZoneStart(text, 0);
        }
        if (bodyEnd >= text.Length)
        {
            return text;
        }

        string suffix = text[bodyEnd..];
        if (suffix is "Z" or "z")
        {
            zone = XmlZoneMetadata.Utc;
            return text[..bodyEnd];
        }
        if (TryParseOffset(suffix, out TimeSpan offset))
        {
            zone = XmlZoneMetadata.OfOffset(offset, suffix);
            return text[..bodyEnd];
        }
        return text;
    }

    private static int ZoneStart(string text, int from)
    {
        for (int i = from; i < text.Length; i++)
        {
            if (text[i] is '+' or '-' or 'Z' or 'z')
            {
                return i;
            }
        }
        return text.Length;
    }

    private static bool TryParseOffset(string suffix, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (suffix.Length < 3)
        {
            return false;
        }
        int sign = suffix[0] == '-' ? -1 : 1;
        string digits = suffix[1..].Replace(":", string.Empty, StringComparison.Ordinal);
        if (digits.Length != 2 && digits.Length != 4)
        {
            return false;
        }
        foreach (char c in digits)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }
        int hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        int minutes = digits.Length == 4 ? int.Parse(digits[2..], CultureInfo.InvariantCulture) : 0;
        if (hours > 14 || minutes > 59)
        {
            return false;
        }
        offset = new TimeSpan(sign * hours, sign * minutes, 0);
        return true;
    }

    private static string FormatDateTimeBody(DateTime value) => value.ToString(
        HasSubSecond(value) ? "yyyy-MM-ddTHH:mm:ss.FFFFFFF" : "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatTimeBody(TimeOnly value) => value.ToString(
        value.Millisecond == 0 && value.Microsecond == 0 && value.Nanosecond == 0
            ? "HH:mm:ss"
            : "HH:mm:ss.FFFFFFF",
        CultureInfo.InvariantCulture);

    private static bool HasSubSecond(DateTime value) => value.Ticks % TimeSpan.TicksPerSecond != 0;

    private static string Require(string text) => text ?? throw new ArgumentNullException(nameof(text));
}
