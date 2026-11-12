using System.Numerics;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// BigDecimal-compatible analysis of a <see cref="decimal"/>: the unscaled value and scale that
/// Java's <c>BigDecimal.stripTrailingZeros()</c> would produce, and the digit counts derived from
/// them.
/// </summary>
/// <remarks>
/// <para>Needed because <c>checkNumber</c>'s digit constraints are specified in terms of
/// BigDecimal's <c>precision()</c> and <c>scale()</c>, and the two types disagree on what a scale
/// is: a C# <c>decimal</c> scale is always 0..28, whereas BigDecimal's goes negative once trailing
/// zeros are stripped (<c>12000</c> becomes unscaled 12 at scale -3). Reproducing that here keeps
/// the emitted failure messages identical rather than approximately identical.</para>
/// <para>Like <c>decimal</c>, BigDecimal preserves trailing zeros as scale, so <c>1.50</c> renders
/// as <c>"1.50"</c> on both sides and interpolated values in messages agree. The one rendering
/// divergence is BigDecimal's scientific notation, which only appears for a negative scale — i.e.
/// only when the source text itself was exponential, such as <c>1E+10</c>. See the divergence
/// register in harness/NOTES.md.</para>
/// </remarks>
public static class RuneDecimal
{
    private static readonly BigInteger MaxUnscaled = new(decimal.MaxValue);

    /// <summary>
    /// The unscaled value and scale after stripping trailing zeros, as
    /// <c>BigDecimal.stripTrailingZeros()</c> defines them. Scale may be negative.
    /// Zero normalises to unscaled 0 at scale 0.
    /// </summary>
    public static (BigInteger Unscaled, int Scale) StripTrailingZeros(decimal value)
    {
        (BigInteger unscaled, int scale) = Deconstruct(value);
        if (unscaled.IsZero)
        {
            return (BigInteger.Zero, 0);
        }
        while (BigInteger.Remainder(unscaled, 10).IsZero)
        {
            unscaled /= 10;
            scale--;
        }
        return (unscaled, scale);
    }

    /// <summary>
    /// Digit count of an unscaled value, as <c>BigDecimal.precision()</c> defines it. Zero has
    /// precision 1.
    /// </summary>
    public static int Precision(BigInteger unscaled)
    {
        if (unscaled.IsZero)
        {
            return 1;
        }
        BigInteger remaining = BigInteger.Abs(unscaled);
        int digits = 0;
        while (!remaining.IsZero)
        {
            remaining /= 10;
            digits++;
        }
        return digits;
    }

    /// <summary>
    /// Significant-digit count as the <c>digits</c> type constraint counts it.
    /// </summary>
    /// <remarks>
    /// Mirrors the three-step calculation in <c>ExpressionOperatorsNullSafe.checkNumber</c>:
    /// start from the stripped precision; when the scale reaches or exceeds it the leading zero
    /// counts too (<c>0.0012</c> is 5, not 2); when the scale is negative the stripped trailing
    /// zeros count again (<c>12000</c> is 5, not 2). The two adjustments are mutually exclusive
    /// because precision is never below 1.
    /// </remarks>
    public static int CountDigits(decimal value)
    {
        (BigInteger unscaled, int scale) = StripTrailingZeros(value);
        int precision = Precision(unscaled);
        int actual = precision;
        if (scale >= precision)
        {
            actual = scale + 1;
        }
        if (scale < 0)
        {
            actual -= scale;
        }
        return actual;
    }

    /// <summary>
    /// Fractional-digit count as the <c>fractionalDigits</c> type constraint counts it: the
    /// stripped scale, floored at zero.
    /// </summary>
    public static int CountFractionalDigits(decimal value)
    {
        (BigInteger _, int scale) = StripTrailingZeros(value);
        return scale < 0 ? 0 : scale;
    }

    /// <summary>Whether two decimals have the same value and stored scale.</summary>
    public static bool EqualsIncludingScale(decimal left, decimal right) =>
        left == right && GetScale(left) == GetScale(right);

    /// <summary>Compares an arbitrary-size integer with a decimal exactly.</summary>
    public static int Compare(BigInteger left, decimal right)
    {
        (BigInteger unscaled, int scale) = Deconstruct(right);
        return (left * Pow10(scale)).CompareTo(unscaled);
    }

    /// <summary>The smaller value, keeping the left operand when magnitudes tie.</summary>
    public static decimal Min(decimal left, decimal right) => left <= right ? left : right;

    /// <summary>The larger value, keeping the left operand when magnitudes tie.</summary>
    public static decimal Max(decimal left, decimal right) => left >= right ? left : right;

    /// <summary>The number of fractional decimal places stored with a value.</summary>
    public static int GetScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    /// <summary>Removes representable trailing zeros while preserving the numeric value.</summary>
    public static decimal TrimTrailingZeros(decimal value)
    {
        (BigInteger unscaled, int scale) = StripTrailingZeros(value);
        if (scale < 0)
        {
            unscaled *= Pow10(-scale);
            scale = 0;
        }
        return Create(unscaled, scale);
    }

    /// <summary>Converts an integral decimal to an int without truncating.</summary>
    public static int ToInt32Exact(decimal value)
    {
        if (!TryToInt32(value, out int result))
        {
            throw new OverflowException($"{value} cannot be represented exactly as an int.");
        }
        return result;
    }

    /// <summary>Tries to convert an integral decimal to an int without truncating.</summary>
    public static bool TryToInt32(decimal value, out int result)
    {
        result = 0;
        if (!TryToBigInteger(value, out BigInteger integral)
            || integral < int.MinValue || integral > int.MaxValue)
        {
            return false;
        }
        result = (int)integral;
        return true;
    }

    /// <summary>Converts an integral decimal to a long without truncating.</summary>
    public static long ToInt64Exact(decimal value)
    {
        if (!TryToInt64(value, out long result))
        {
            throw new OverflowException($"{value} cannot be represented exactly as a long.");
        }
        return result;
    }

    /// <summary>Tries to convert an integral decimal to a long without truncating.</summary>
    public static bool TryToInt64(decimal value, out long result)
    {
        result = 0;
        if (!TryToBigInteger(value, out BigInteger integral)
            || integral < long.MinValue || integral > long.MaxValue)
        {
            return false;
        }
        result = (long)integral;
        return true;
    }

    /// <summary>Converts an integral decimal to a BigInteger without truncating.</summary>
    public static BigInteger ToBigIntegerExact(decimal value)
    {
        if (!TryToBigInteger(value, out BigInteger result))
        {
            throw new OverflowException($"{value} has a fractional part and is not an integer.");
        }
        return result;
    }

    /// <summary>Tries to convert an integral decimal to a BigInteger without truncating.</summary>
    public static bool TryToBigInteger(decimal value, out BigInteger result)
    {
        (BigInteger unscaled, int scale) = Deconstruct(value);
        if (scale == 0)
        {
            result = unscaled;
            return true;
        }
        result = BigInteger.DivRem(unscaled, Pow10(scale), out BigInteger remainder);
        if (!remainder.IsZero)
        {
            result = BigInteger.Zero;
            return false;
        }
        return true;
    }

    /// <summary>Returns the value at the requested decimal scale using the supplied rounding mode.</summary>
    public static decimal SetScale(decimal value, int newScale, RuneRounding mode)
    {
        if (newScale > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(newScale), "decimal supports at most 28 fractional digits.");
        }

        (BigInteger unscaled, int scale) = Deconstruct(value);
        if (newScale >= scale)
        {
            return Create(unscaled * Pow10(newScale - scale), newScale);
        }

        BigInteger divisor = Pow10(scale - newScale);
        BigInteger magnitude = BigInteger.Abs(unscaled);
        BigInteger quotient = BigInteger.DivRem(magnitude, divisor, out BigInteger remainder);
        if (!remainder.IsZero && RoundsAway(remainder, divisor, quotient, mode))
        {
            quotient += BigInteger.One;
        }
        if (unscaled.Sign < 0)
        {
            quotient = -quotient;
        }
        if (newScale < 0)
        {
            quotient *= Pow10(-newScale);
            newScale = 0;
        }
        return Create(quotient, newScale);
    }

    /// <summary>Divides and rounds once to an exact number of decimal places.</summary>
    public static decimal DivideToScale(decimal dividend, decimal divisor, int scale, RuneRounding mode)
    {
        if (divisor == 0m)
        {
            throw new DivideByZeroException();
        }
        if (scale > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "decimal supports at most 28 fractional digits.");
        }

        (BigInteger dividendUnscaled, int dividendScale) = Deconstruct(dividend);
        (BigInteger divisorUnscaled, int divisorScale) = Deconstruct(divisor);
        BigInteger numerator = BigInteger.Abs(dividendUnscaled);
        BigInteger denominator = BigInteger.Abs(divisorUnscaled);
        int shift = divisorScale + scale - dividendScale;
        if (shift > 0)
        {
            numerator *= Pow10(shift);
        }
        else if (shift < 0)
        {
            denominator *= Pow10(-shift);
        }
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        if (!remainder.IsZero && RoundsAway(remainder, denominator, quotient, mode))
        {
            quotient += BigInteger.One;
        }
        if ((dividendUnscaled.Sign < 0) ^ (divisorUnscaled.Sign < 0))
        {
            quotient = -quotient;
        }
        if (scale < 0)
        {
            quotient *= Pow10(-scale);
            scale = 0;
        }
        return Create(quotient, scale);
    }

    /// <summary>Rounds a value to a requested count of significant figures.</summary>
    public static decimal Round(decimal value, int significantFigures, RuneRounding mode)
    {
        if (significantFigures <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(significantFigures));
        }
        (BigInteger unscaled, int scale) = Deconstruct(value);
        int digits = Precision(unscaled);
        return digits <= significantFigures
            ? value
            : SetScale(value, scale - (digits - significantFigures), mode);
    }

    /// <summary>The unscaled value and scale exactly as the <c>decimal</c> stores them.</summary>
    private static (BigInteger Unscaled, int Scale) Deconstruct(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        int scale = (bits[3] >> 16) & 0xFF;
        bool negative = (bits[3] & unchecked((int)0x80000000)) != 0;

        BigInteger unscaled = new BigInteger((uint)bits[2]) << 64;
        unscaled |= new BigInteger((uint)bits[1]) << 32;
        unscaled |= new BigInteger((uint)bits[0]);
        if (negative)
        {
            unscaled = -unscaled;
        }
        return (unscaled, scale);
    }

    private static decimal Create(BigInteger unscaled, int scale)
    {
        if (scale is < 0 or > 28 || BigInteger.Abs(unscaled) > MaxUnscaled)
        {
            throw new OverflowException($"{unscaled} at scale {scale} cannot be represented as a decimal.");
        }

        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        BigInteger.Abs(unscaled).TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: false);
        return new decimal(
            BitConverter.ToInt32(bytes),
            BitConverter.ToInt32(bytes[4..]),
            BitConverter.ToInt32(bytes[8..]),
            unscaled.Sign < 0,
            (byte)scale);
    }

    private static BigInteger Pow10(int exponent) => BigInteger.Pow(10, exponent);

    private static bool RoundsAway(
        BigInteger remainder, BigInteger divisor, BigInteger quotient, RuneRounding mode) => mode switch
    {
        RuneRounding.Up => true,
        RuneRounding.Down => false,
        RuneRounding.HalfUp => remainder * 2 >= divisor,
        RuneRounding.HalfEven => remainder * 2 > divisor
            || (remainder * 2 == divisor && !quotient.IsEven),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
