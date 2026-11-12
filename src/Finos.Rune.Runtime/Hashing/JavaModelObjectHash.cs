using System.Collections;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Hashing;

/// <summary>
/// Reproduces the generated Java FpML model object's virtual <c>hashCode()</c>.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="GlobalKeyHasher"/>. FpML ingestion uses ordinary generated
/// POJO hashes to mint temporary scoped keys, so the runtime subtype and Java's platform hashes are
/// load-bearing. Reflection is cached once per concrete model type. The API rejects other model
/// families: CDM has same-shape attribute overrides whose duplicate Java folds are intentionally not
/// represented in the core C# model, while rune-fpml 3.2.0 has none.
/// </remarks>
public static class JavaModelObjectHash
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyPlan>> Plans = new();

    /// <summary>Hashes <paramref name="value"/> as its runtime model subtype.</summary>
    public static int Of(IRosettaModelObject value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int result = 0;
        foreach (PropertyPlan property in Plans.GetOrAdd(value.GetType(), BuildPlan))
        {
            object? propertyValue = property.Property.GetValue(value);
            int contribution = property.IsEnum
                ? EnumHash(propertyValue, property.ItemType, property.IsList)
                : ObjectHash(propertyValue);
            unchecked
            {
                result = (31 * result) + contribution;
            }
        }
        return result;
    }

    private static IReadOnlyList<PropertyPlan> BuildPlan(Type concreteType)
    {
        RuneDataTypeAttribute? dataType = concreteType.GetCustomAttribute<RuneDataTypeAttribute>();
        if (dataType == null || !dataType.Namespace.StartsWith("fpml.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Generated Java model hashing is supported only for FpML types, not {concreteType}.");
        }

        var hierarchy = new Stack<Type>();
        for (Type? type = concreteType;
             type != null && type != typeof(RosettaModelObject);
             type = type.BaseType)
        {
            hierarchy.Push(type);
        }

        var result = new List<PropertyPlan>();
        while (hierarchy.Count > 0)
        {
            Type level = hierarchy.Pop();
            foreach (PropertyInfo property in level
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(candidate => candidate.MetadataToken))
            {
                if (property.GetCustomAttribute<RuneAttributeAttribute>() == null
                    || property.GetCustomAttribute<RuneIgnoreAttribute>() != null)
                {
                    continue;
                }

                Type? listItem = ListItemType(property.PropertyType);
                Type itemType = listItem ?? Nullable.GetUnderlyingType(property.PropertyType)
                    ?? property.PropertyType;
                result.Add(new PropertyPlan(property, listItem != null, itemType, itemType.IsEnum));
            }
        }
        return result;
    }

    private static Type? ListItemType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return Nullable.GetUnderlyingType(type.GetGenericArguments()[0])
                ?? type.GetGenericArguments()[0];
        }
        return null;
    }

    private static int EnumHash(object? value, Type enumType, bool isList)
    {
        if (value == null)
        {
            return 0;
        }

        int classHash = JavaHash.Of(JavaClassName(enumType));
        if (!isList)
        {
            return classHash;
        }

        int result = 0;
        foreach (object? item in (IEnumerable)value)
        {
            if (item == null)
            {
                throw new InvalidOperationException("A generated Java enum list cannot contain null.");
            }
            unchecked
            {
                result += classHash;
            }
        }
        return result;
    }

    private static int ObjectHash(object? value)
    {
        if (value == null)
        {
            return 0;
        }
        if (value is IRosettaModelObject model)
        {
            return Of(model);
        }
        if (value is string text)
        {
            return JavaHash.Of(text);
        }
        if (value is BigInteger integer)
        {
            return BigIntegerHash(integer);
        }
        if (value is decimal number)
        {
            return BigDecimalHash(number);
        }
        if (value is int int32)
        {
            return int32;
        }
        if (value is long int64)
        {
            unchecked
            {
                return (int)(int64 ^ (long)((ulong)int64 >> 32));
            }
        }
        if (value is short int16)
        {
            return int16;
        }
        if (value is byte byteValue)
        {
            return byteValue;
        }
        if (value is bool boolean)
        {
            return JavaHash.Of(boolean);
        }
        if (value is DateOnly date)
        {
            return RuneDateHash(date);
        }
        if (value is TimeOnly time)
        {
            return JavaHash.Of(time);
        }
        if (value is DateTime dateTime)
        {
            return JavaHash.Of(dateTime);
        }
        if (value is DateTimeOffset zonedDateTime)
        {
            return ZonedDateTimeHash(zonedDateTime);
        }
        if (value is Enum enumValue)
        {
            return JavaHash.Of(JavaClassName(enumValue.GetType()));
        }
        if (value is IEnumerable values)
        {
            return ListHash(values);
        }
        throw new InvalidOperationException(
            $"No generated Java model hash for value type {value.GetType()}.");
    }

    private static int ListHash(IEnumerable values)
    {
        int result = 1;
        foreach (object? value in values)
        {
            unchecked
            {
                result = (31 * result) + ObjectHash(value);
            }
        }
        return result;
    }

    private static int BigDecimalHash(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger unscaled = (uint)bits[0]
            + ((BigInteger)(uint)bits[1] << 32)
            + ((BigInteger)(uint)bits[2] << 64);
        if ((bits[3] & int.MinValue) != 0)
        {
            unscaled = -unscaled;
        }
        int scale = (bits[3] >> 16) & 0x7F;
        unchecked
        {
            return (31 * BigIntegerHash(unscaled)) + scale;
        }
    }

    private static int RuneDateHash(DateOnly value)
    {
        int result = 1;
        unchecked
        {
            result = (31 * result) + value.Day;
            result = (31 * result) + value.Month;
            return (31 * result) + value.Year;
        }
    }

    private static int ZonedDateTimeHash(DateTimeOffset value)
    {
        int offsetSeconds = (int)value.Offset.TotalSeconds;
        int rotatedOffset = unchecked((int)BitOperations.RotateLeft((uint)offsetSeconds, 3));
        return JavaHash.Of(value.DateTime) ^ offsetSeconds ^ rotatedOffset;
    }

    private static int BigIntegerHash(BigInteger value)
    {
        if (value.IsZero)
        {
            return 0;
        }

        int sign = value.Sign;
        BigInteger magnitude = BigInteger.Abs(value);
        var words = new List<uint>();
        while (!magnitude.IsZero)
        {
            words.Add((uint)(magnitude & uint.MaxValue));
            magnitude >>= 32;
        }

        int hash = 0;
        for (int index = words.Count - 1; index >= 0; index--)
        {
            unchecked
            {
                hash = (31 * hash) + (int)words[index];
            }
        }
        return sign < 0 ? -hash : hash;
    }

    private static string JavaClassName(Type type)
    {
        string? typeNamespace = type.Namespace;
        if (string.IsNullOrEmpty(typeNamespace))
        {
            throw new InvalidOperationException($"Generated enum type {type} has no namespace.");
        }
        string runeNamespace = string.Join(".", typeNamespace.Split('.').Select(LowerFirst));
        return runeNamespace + "." + type.Name;
    }

    private static string LowerFirst(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private sealed record PropertyPlan(
        PropertyInfo Property,
        bool IsList,
        Type ItemType,
        bool IsEnum);
}
