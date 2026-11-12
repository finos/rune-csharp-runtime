using System.Linq;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// List projections used by generated coercions.
/// </summary>
/// <remarks>
/// Has no counterpart in the Java runtime, and needs none: Java's generated coercions call
/// <c>stream().map(...)</c> on a list that is never null, because a Rune list attribute whose value is
/// absent still reads as an empty list there. In C# an absent list attribute is <c>null</c>, and
/// <c>Enumerable.Select</c> throws on a null source — so the widening a generated <c>MapC</c> lambda
/// performs has to preserve absence rather than fault on it.
/// </remarks>
public static class RuneList
{
    /// <summary>
    /// Projects each element of <paramref name="source"/>, mapping absence to absence: a
    /// <c>null</c> source yields <c>null</c>, not an empty list.
    /// </summary>
    /// <remarks>
    /// The two type arguments are always stated at the call site. The projection is usually a
    /// widening that is implicit inside the lambda — <c>EnumT</c> to <c>EnumT?</c> — and inferring
    /// <typeparamref name="TResult"/> from the lambda body would skip it, producing a list of the
    /// narrow type behind a reference of the wide one.
    /// </remarks>
    public static IReadOnlyList<TResult>? Map<TSource, TResult>(
        IReadOnlyList<TSource>? source, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return source?.Select(selector).ToList();
    }

    /// <summary>
    /// Appends <paramref name="values"/> to <paramref name="target"/>, treating absence as nothing to
    /// append. Backs a Rune <c>add</c> of a multi output.
    /// </summary>
    /// <remarks>
    /// A null <paramref name="values"/> is an expression that produced no items, which appends nothing
    /// — not a null element. That distinction is the reason this exists rather than a bare
    /// <c>AddRange</c>, which would throw.
    /// </remarks>
    public static void AddRange<T>(List<T> target, IReadOnlyList<T>? values)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (values != null)
        {
            target.AddRange(values);
        }
    }

    /// <summary>
    /// Replaces the contents of <paramref name="target"/>. Backs a Rune <c>set</c> of a multi output.
    /// </summary>
    /// <remarks>
    /// Clearing first is what makes a later <c>set</c> override an earlier operation rather than add to
    /// it, which is the difference between <c>set</c> and <c>add</c> on a list.
    /// </remarks>
    public static void ReplaceWith<T>(List<T> target, IReadOnlyList<T>? values)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Clear();
        AddRange(target, values);
    }
}
