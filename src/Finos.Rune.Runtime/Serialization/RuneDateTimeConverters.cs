using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finos.Rune.Runtime.Serialization;

/// <summary>Rune 'date': ISO yyyy-MM-dd; reads the legacy {"year","month","day"} object
/// form the Java reference also accepts (RuneDateModule/DateExtended).</summary>
public sealed class RuneDateOnlyConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string text = reader.GetString()!;
            return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly result)
                ? result
                : throw new JsonException($"Invalid date '{text}' (expected yyyy-MM-dd).");
        }
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int? year = null, month = null, day = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string name = reader.GetString()!;
                reader.Read();
                switch (name)
                {
                    case "year": year = reader.GetInt32(); break;
                    case "month": month = reader.GetInt32(); break;
                    case "day": day = reader.GetInt32(); break;
                    default: reader.Skip(); break;
                }
            }
            if (year is int y && month is int m && day is int d)
            {
                return new DateOnly(y, m, d);
            }
            throw new JsonException("Legacy date object requires year, month and day.");
        }
        throw new JsonException($"Cannot read a date from {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}

/// <summary>Rune 'time': ISO HH:mm:ss with optional fractional seconds.</summary>
public sealed class RuneTimeOnlyConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = { "HH:mm:ss", "HH:mm:ss.FFFFFFF", "HH:mm" };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string text = reader.GetString()!;
        return TimeOnly.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly result)
            ? result
            : throw new JsonException($"Invalid time '{text}' (expected HH:mm:ss).");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Millisecond == 0 && value.Microsecond == 0 && value.Nanosecond == 0
            ? value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            : value.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
}

/// <summary>Rune 'dateTime': zone-less ISO local (yyyy-MM-ddTHH:mm:ss), Kind left
/// unspecified — a local datetime must not gain an offset on the wire.</summary>
public sealed class RuneDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] Formats = { "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm" };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string text = reader.GetString()!;
        return DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result)
            ? result
            : throw new JsonException($"Invalid local dateTime '{text}' (expected yyyy-MM-ddTHH:mm:ss).");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(
            value.Millisecond == 0 && value.Microsecond == 0 && value.Nanosecond == 0
                ? "yyyy-MM-ddTHH:mm:ss"
                : "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            CultureInfo.InvariantCulture));
}

/// <summary>Rune 'zonedDateTime': ISO offset form matching java.time's
/// ISO_OFFSET_DATE_TIME — zero offset written as 'Z' (never '+00:00'), fractional
/// seconds only when present, offsets preserved on read.</summary>
public sealed class RuneDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", "yyyy-MM-ddTHH:mmK",
    };

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string text = reader.GetString()!;
        return DateTimeOffset.TryParseExact(text, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset result)
            ? result
            : throw new JsonException($"Invalid zonedDateTime '{text}' (expected ISO offset form).");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        string body = value.ToString(
            value.Millisecond == 0 && value.Microsecond == 0 && value.Nanosecond == 0
                ? "yyyy-MM-ddTHH:mm:ss"
                : "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            CultureInfo.InvariantCulture);
        string offset = value.Offset == TimeSpan.Zero
            ? "Z"
            : (value.Offset < TimeSpan.Zero ? "-" : "+") + value.Offset.Duration().ToString(@"hh\:mm");
        writer.WriteStringValue(body + offset);
    }
}
