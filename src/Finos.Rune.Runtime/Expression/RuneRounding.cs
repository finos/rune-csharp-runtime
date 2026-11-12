namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// The rounding modes CDM's rounding functions ask for.
/// </summary>
/// <remarks>
/// <para>A subset of <c>java.math.RoundingMode</c> — exactly the three that CDM's
/// <c>RoundingModeEnum</c> (UP, DOWN) and <c>RoundingDirectionEnum</c> (UP, DOWN, NEAREST) map onto.
/// The rest are absent on purpose: an untested rounding path is a silently wrong number, and adding one
/// is cheap when something needs it.</para>
/// <para>Not <c>MidpointRounding</c>: .NET's modes are defined toward positive or negative infinity,
/// while these are defined relative to zero, which is what the model means.</para>
/// </remarks>
public enum RuneRounding
{
    /// <summary>Away from zero whenever anything is dropped. <c>RoundingMode.UP</c>.</summary>
    Up,

    /// <summary>Toward zero, dropping the remainder. <c>RoundingMode.DOWN</c>.</summary>
    Down,

    /// <summary>Nearest, with ties going away from zero. <c>RoundingMode.HALF_UP</c>.</summary>
    HalfUp,

    /// <summary>
    /// Nearest, with ties going to the even neighbour. <c>RoundingMode.HALF_EVEN</c>, banker's rounding.
    /// </summary>
    /// <remarks>Used by CDM's fixed-scale vector division.</remarks>
    HalfEven,
}
