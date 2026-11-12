using System.Numerics;
using Finos.Rune.Runtime.Expression;

namespace Finos.Rune.Runtime.Mapper;

/// <summary>
/// The Java class simple name a value would have carried, for the root element of a
/// <see cref="MapperPath"/>.
/// </summary>
/// <remarks>
/// <para><c>MapperPath.addRoot</c> uses <c>clazz.getSimpleName()</c>, and that name is interpolated
/// into failure messages — <c>[Integer]</c> and <c>[BigDecimal]</c> appear on 49 lines of the CDM
/// test pack's validation output. <c>GetType().Name</c> would render <c>Int32</c> and
/// <c>Decimal</c> there and diverge.</para>
/// <para>Resolve from the boxed runtime value, never from <c>typeof(T)</c>: for a nullable value
/// type <c>typeof(decimal?).Name</c> is <c>Nullable`1</c>.</para>
/// </remarks>
public static class JavaTypeNames
{
    /// <summary>The Java simple name for a runtime value.</summary>
    public static string Of(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            IRosettaModelObject => SimpleName(value.GetType()),
            decimal => "BigDecimal",
            int => "Integer",
            long => "Long",
            short => "Short",
            BigInteger => "BigInteger",
            bool => "Boolean",
            string => "String",
            // Java labels a Rune date by its implementation class, DateImpl.
            DateOnly => "DateImpl",
            TimeOnly => "LocalTime",
            DateTimeOffset => "ZonedDateTime",
            DateTime => "LocalDateTime",
            Enum => SimpleName(value.GetType()),
            _ => SimpleName(value.GetType()),
        };
    }

    private static string SimpleName(Type type)
    {
        string name = type.Name;
        int arity = name.IndexOf('`', StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }
}
