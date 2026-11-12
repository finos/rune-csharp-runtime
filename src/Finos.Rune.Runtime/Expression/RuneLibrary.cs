namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// The Rune DSL's built-in library functions.
/// </summary>
/// <remarks>
/// <para>The language declares six — <c>Min</c>, <c>Max</c>, <c>IsLeapYear</c>, and the argument-less
/// <c>DateRanges</c>, <c>Adjust</c> and <c>Within</c>. Only the first three are implemented, because only
/// those three are ever called: CDM uses <c>Max</c> 10 times, <c>Min</c> 5 and <c>IsLeapYear</c> 3, and the
/// other three not at all. They take no arguments and return nothing usable, so there is nothing to port
/// even if a caller appeared; the generator refuses by name instead, which would say so.</para>
/// <para>Absent in, absent out, as everywhere else in the expression tier. The reference's generated
/// wrappers would throw on an absent argument; propagating instead keeps a library call from being the one
/// place in an expression where absence stops being contagious, and no CDM call site can reach it — every
/// one passes a date part or a literal.</para>
/// </remarks>
public static class RuneLibrary
{
    /// <summary>The smaller of two numbers, or absent if either is.</summary>
    public static decimal? Min(decimal? x, decimal? y) =>
        x == null || y == null ? null : RuneDecimal.Min(x.Value, y.Value);

    /// <summary>The larger of two numbers, or absent if either is.</summary>
    public static decimal? Max(decimal? x, decimal? y) =>
        x == null || y == null ? null : RuneDecimal.Max(x.Value, y.Value);

    /// <summary>Whether a year is a leap year, or absent if the year is.</summary>
    /// <remarks>
    /// The year is declared <c>number</c> in the DSL rather than <c>int</c>. A fractional year is
    /// truncated toward zero rather than
    /// rejected, which is what reading only the integral part of the value amounts to.
    /// </remarks>
    public static bool? IsLeapYear(decimal? year) =>
        year == null ? null : DateTime.IsLeapYear((int)year.Value);
}
