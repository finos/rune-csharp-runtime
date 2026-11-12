using System.Globalization;

namespace Finos.Rune.Runtime.Hashing;

/// <summary>
/// Java's <c>hashCode()</c> for every basic type a Rune model can hold, reproduced exactly.
/// </summary>
/// <remarks>
/// <para>
/// Global keys (<c>meta -&gt; key</c>) are computed, not read, so a C# distribution that creates
/// objects has to derive them itself — and the CDM function samples and ingest expectations carry
/// Java-computed values, so "deterministic" is not enough: the numbers have to agree.
/// </para>
/// <para>
/// This mirrors <c>com.regnosys.rosetta.common.hashing.IntegerHashGenerator</c> (rune-common
/// 12.3.1), which dispatches on the runtime type in <c>RosettaBasicTypesHashGenerator.generate</c>
/// and delegates to a <c>handle</c> overload per type. Two of those overloads reduce a hard case to
/// an easy one: an enum hashes its <c>name()</c>, and a <c>BigDecimal</c> hashes its
/// <c>stripTrailingZeros().toPlainString()</c> — both end up in the string hash, so neither Java's
/// identity-based <c>Enum.hashCode()</c> nor its <c>BigInteger.hashCode()</c> is involved.
/// </para>
/// <para>
/// Every accumulation is deliberately <c>unchecked</c>: Java <c>int</c> arithmetic wraps, and the
/// wrap-around is load-bearing rather than incidental — most real hashes overflow many times over.
/// </para>
/// </remarks>
public static class JavaHash
{
    /// <summary>Java's <c>String.hashCode()</c>: <c>h = 31h + c</c> over the UTF-16 code units.</summary>
    public static int Of(string value)
    {
        int hash = 0;
        foreach (char c in value)
        {
            unchecked { hash = hash * 31 + c; }
        }

        return hash;
    }

    /// <summary>Java's <c>Integer.hashCode()</c>, which is the value itself.</summary>
    public static int Of(int value) => value;

    /// <summary>Java's <c>Boolean.hashCode()</c>, the two constants it has returned since 1.0.</summary>
    public static int Of(bool value) => value ? 1231 : 1237;

    /// <summary>Java's <c>LocalDate.hashCode()</c>.</summary>
    public static int Of(DateOnly value)
    {
        int year = value.Year;
        int month = value.Month;
        int day = value.Day;
        unchecked
        {
            return (year & (int)0xFFFFF800) ^ ((year << 11) + (month << 6) + day);
        }
    }

    /// <summary>Java's <c>LocalTime.hashCode()</c>, folding the nano-of-day onto 32 bits.</summary>
    public static int Of(TimeOnly value)
    {
        long nanoOfDay = value.Ticks * 100L;
        unchecked
        {
            return (int)(nanoOfDay ^ (long)((ulong)nanoOfDay >> 32));
        }
    }

    /// <summary>Java's <c>LocalDateTime.hashCode()</c> — the date and time halves XOR-ed.</summary>
    public static int Of(DateTime value) =>
        Of(DateOnly.FromDateTime(value)) ^ Of(TimeOnly.FromDateTime(value));

    /// <summary>
    /// rune-common's integer hash for a <c>ZonedDateTime</c>: the local date-time, the offset in
    /// seconds, and the hash of the zone <em>id</em>.
    /// </summary>
    /// <remarks>
    /// This deliberately is not <c>ZonedDateTime.hashCode()</c>. Rune's
    /// <c>IntegerHashGenerator</c> hashes <c>getZone().getId()</c> without the JDK hash code's rotate.
    /// A plain <c>DateTimeOffset</c> corresponds to a fixed <c>ZoneOffset</c>, whose id is <c>Z</c> or
    /// a canonical signed offset. XML-origin values can carry a different synthetic zone id; the
    /// global-key walker supplies that provenance separately.
    /// </remarks>
    public static int Of(DateTimeOffset value)
    {
        unchecked
        {
            return Of(value, ZoneOffsetId(value.Offset));
        }
    }

    /// <summary>rune-common's integer hash for a zoned value with an explicit Java zone id.</summary>
    public static int Of(DateTimeOffset value, string zoneId)
    {
        ArgumentNullException.ThrowIfNull(zoneId);
        return Of(value.DateTime) ^ (int)value.Offset.TotalSeconds ^ Of(zoneId);
    }

    /// <summary>Java's <c>BigDecimal.hashCode()</c> as rune-common redefines it: over the plain string.</summary>
    public static int Of(decimal value) => Of(PlainString(value));

    /// <summary>
    /// An enum value's hash, which is the hash of its Java <c>name()</c> — the constant as declared
    /// in the Rune model, not the wire name and not the PascalCase C# member name.
    /// </summary>
    public static int OfEnumName(string javaConstantName) => Of(javaConstantName);

    /// <summary>
    /// The hash of a boxed basic value, dispatched on its runtime type.
    /// </summary>
    /// <remarks>
    /// This mirrors <c>RosettaBasicTypesHashGenerator.generate</c>, which is a chain of
    /// <c>instanceof</c> tests. An unrecognised type throws: Rune's basic types are a closed set, so
    /// anything else means the walker reached a property it should not have, and hashing it on a
    /// guess would corrupt the key rather than report the mistake.
    /// </remarks>
    public static int OfValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            string s => Of(s),
            int i => Of(i),
            DateOnly d => Of(d),
            TimeOnly t => Of(t),
            DateTime dt => Of(dt),
            DateTimeOffset dto => Of(dto),
            decimal m => Of(m),
            bool b => Of(b),
            Enum e => OfEnumName(RuneEnumNames.JavaNameOf(e)),
            _ => throw new InvalidOperationException(
                $"No Java hash for {value.GetType()} — not a Rune basic type."),
        };
    }

    /// <summary>
    /// <c>ZoneOffset.getId()</c> for a fixed offset.
    /// </summary>
    public static string ZoneOffsetId(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
        {
            return "Z";
        }

        string sign = offset < TimeSpan.Zero ? "-" : "+";
        TimeSpan magnitude = offset.Duration();
        string id = $"{sign}{magnitude.Hours:00}:{magnitude.Minutes:00}";
        return magnitude.Seconds == 0 ? id : $"{id}:{magnitude.Seconds:00}";
    }

    /// <summary>
    /// <c>BigDecimal.stripTrailingZeros().toPlainString()</c> for a <c>decimal</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The custom format does both jobs at once: <c>#</c> placeholders drop trailing zeros, and a
    /// non-scientific format never emits an exponent, which is what <c>toPlainString</c> guarantees
    /// and what plain <c>ToString()</c> would not (it keeps the scale, so <c>1.50m</c> stays
    /// <c>"1.50"</c> where Java gives <c>"1.5"</c>). 28 <c>#</c> covers <c>decimal</c>'s full scale.
    /// </para>
    /// <para>
    /// Zero is special-cased because <c>decimal</c> has a sign bit that survives formatting, so
    /// <c>-0.0m</c> would render <c>"-0"</c>; a <c>BigDecimal</c> has no signed zero and gives
    /// <c>"0"</c>.
    /// </para>
    /// </remarks>
    public static string PlainString(decimal value) =>
        value == 0m ? "0" : value.ToString("0.############################", CultureInfo.InvariantCulture);
}
