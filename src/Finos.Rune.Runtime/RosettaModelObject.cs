using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime;

/// <summary>
/// Base class of all generated immutable model types. Subclasses expose get-only
/// properties, a nested Builder derived from <see cref="RosettaModelObjectBuilder"/>,
/// and override <see cref="ToBuilder"/> with a covariant return type.
/// </summary>
/// <remarks>
/// Equality is by value, matching the Java POJOs: two separately-constructed objects with the same
/// attributes are equal. Rune's <c>=</c> operator relies on it — a data rule comparing
/// <c>derivedQuantity -&gt; unit = unit</c> compares two distinct instances — and so does any
/// consumer that puts model objects in a set or asserts on them in a test.
/// </remarks>
public abstract class RosettaModelObject : IRosettaModelObject
{
    public abstract RosettaModelObjectBuilder ToBuilder();

    IRosettaModelObjectBuilder IRosettaModelObject.ToBuilder() => ToBuilder();

    /// <summary>
    /// Value equality over the type's Rune attributes, in declaration order.
    /// </summary>
    /// <remarks>
    /// <para>Reflective over <see cref="RunePropertyTable"/> rather than generated per type, for the
    /// same reason the serializer is: one reflection-derived traversal, cached per type, that cannot
    /// drift from the one already proven against the Java reference. It also means the runtime's own
    /// model objects — <c>MetaFields</c>, <c>FieldWithMeta&lt;T&gt;</c>, <c>Key</c> — get value
    /// equality from the same code as the 775 generated ones.</para>
    /// <para>A list attribute treats absent and empty as the same value, reproducing Java's
    /// <c>ListEquals.listEquals</c>; an absent Rune list and an empty one are the same model.</para>
    /// <para>One deliberate divergence: Java compares <c>getType()</c>, the POJO's *interface*, so a
    /// built <c>Price</c> and a <c>Price.Builder</c> with equal attributes compare equal there. Here
    /// the runtime types differ and they do not. Nothing in the validation or serialization tiers
    /// compares across that boundary, and equality between an immutable value and a mutable builder
    /// would be a trap in C#, where the builder's hash would change under it.</para>
    /// </remarks>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }
        foreach (RunePropertyTable.Entry entry in RunePropertyTable.For(GetType()).DeclarationOrderEntries)
        {
            object? mine = entry.Property.GetValue(this);
            object? theirs = entry.Property.GetValue(obj);
            bool equal = entry.ListElementType != null
                ? ListsEqual(mine as System.Collections.IEnumerable, theirs as System.Collections.IEnumerable)
                : object.Equals(mine, theirs);
            if (!equal)
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Consistent with <see cref="Equals(object?)"/>, and deliberately not byte-compatible with
    /// Java's. Java's generated <c>hashCode</c> mixes a *list* attribute in by summing the hashes of
    /// its elements' class names, which collides every list of the same shape; nothing compares hash
    /// codes across the two implementations, so there is nothing to gain by reproducing it.
    /// </remarks>
    public override int GetHashCode()
    {
        int result = 0;
        foreach (RunePropertyTable.Entry entry in RunePropertyTable.For(GetType()).DeclarationOrderEntries)
        {
            object? value = entry.Property.GetValue(this);
            int contribution = entry.ListElementType != null
                ? ListHashCode(value as System.Collections.IEnumerable)
                : value?.GetHashCode() ?? 0;
            result = (31 * result) + contribution;
        }
        return result;
    }

    private static bool ListsEqual(System.Collections.IEnumerable? left, System.Collections.IEnumerable? right)
    {
        System.Collections.IEnumerator? l = left?.GetEnumerator();
        System.Collections.IEnumerator? r = right?.GetEnumerator();
        while (true)
        {
            bool hasLeft = l?.MoveNext() == true;
            bool hasRight = r?.MoveNext() == true;
            if (hasLeft != hasRight)
            {
                return false;
            }
            if (!hasLeft)
            {
                return true;
            }
            if (!object.Equals(l!.Current, r!.Current))
            {
                return false;
            }
        }
    }

    // Absent and empty must hash alike, because ListsEqual calls them equal.
    private static int ListHashCode(System.Collections.IEnumerable? values)
    {
        int result = 1;
        if (values != null)
        {
            foreach (object? value in values)
            {
                result = (31 * result) + (value?.GetHashCode() ?? 0);
            }
        }
        return result;
    }
}
