using System.Globalization;

namespace Finos.Rune.Runtime.Hashing;

/// <summary>
/// The running global-key hash: a single <c>int</c> folded over an object graph, rendered as hex.
/// </summary>
/// <remarks>
/// <para>
/// This is <c>com.regnosys.rosetta.common.hashing.IntegerReport</c> (rune-common 12.3.1). Two things
/// about it are easy to get wrong and are the reason it is its own type rather than a couple of
/// inline expressions:
/// </para>
/// <list type="bullet">
/// <item>
/// An <em>object</em> that is visited contributes structure but no value — <see cref="Accumulate()"/>
/// multiplies by 31 and adds nothing. So two graphs that differ only in shape still hash differently,
/// even when they carry identical values.
/// </item>
/// <item>
/// The rendering is <c>Integer.toHexString</c>, which is <b>unpadded</b>. Real keys are therefore not
/// all eight digits wide — CDM ships <c>b08711</c> alongside <c>14c27c88</c> — and formatting with
/// <c>"x8"</c> would silently break every key whose top byte happens to be zero.
/// </item>
/// </list>
/// <para>
/// "NonNull", in the collector that drives this, is literal: a null basic value accumulates nothing
/// at all, not even the multiply. Only present values reach here.
/// </para>
/// </remarks>
public sealed class HashAccumulator
{
    private int _result;

    /// <summary>The hash as folded so far, before rendering.</summary>
    public int Result => _result;

    /// <summary>Fold in a visited object: structure only, no value.</summary>
    public HashAccumulator Accumulate()
    {
        unchecked { _result *= 31; }
        return this;
    }

    /// <summary>Fold in a basic value's hash.</summary>
    public HashAccumulator Accumulate(int valueHash)
    {
        unchecked { _result = _result * 31 + valueHash; }
        return this;
    }

    /// <summary>Render as Java's <c>Integer.toHexString</c> does: lower-case, unpadded, two's complement.</summary>
    public override string ToString() =>
        ((uint)_result).ToString("x", CultureInfo.InvariantCulture);
}
