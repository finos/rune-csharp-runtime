using Finos.Rune.Runtime.Mapper;
using Finos.Rune.Runtime.Serialization;
using Finos.Rune.Runtime.Validation;

namespace Finos.Rune.Runtime.Expression;

/// <summary>
/// The free functions generated expression code calls.
/// Mirrors com.rosetta.model.lib.expression.ExpressionOperatorsNullSafe.
/// </summary>
/// <remarks>
/// Every failure message is reproduced character for character. They are compared against the
/// Java implementation over the whole CDM test pack once data rules are switched on, and 147,062
/// of the pack's validation records are data rules. Do not reword them.
/// </remarks>
public static class RuneExpr
{
    /// <summary>True when the mapper holds anything.</summary>
    public static ComparisonResult Exists<T>(IMapper<T> o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return o.ResultCount > 0
            ? ComparisonResult.Success()
            : ComparisonResult.Failure(Describe(o.ErrorPaths) + " does not exist");
    }

    /// <summary>True when the mapper holds nothing.</summary>
    public static ComparisonResult NotExists<T>(IMapper<T> o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return o.ResultCount == 0
            ? ComparisonResult.Success()
            : ComparisonResult.Failure(Describe(o.Paths) + " does exist and is " + FormatMultiError(o));
    }

    /// <summary>True when the mapper holds exactly one value.</summary>
    public static ComparisonResult SingleExists<T>(IMapper<T> o)
    {
        ArgumentNullException.ThrowIfNull(o);
        if (o.ResultCount == 1)
        {
            return ComparisonResult.Success();
        }
        string error = o.ResultCount > 0
            ? $"Expected single {Describe(o.Paths)} but found {o.ResultCount} [{FormatMultiError(o)}]"
            : $"Expected single {Describe(o.ErrorPaths)} but found zero";
        return ComparisonResult.Failure(error);
    }

    /// <summary>True when the mapper holds more than one value.</summary>
    public static ComparisonResult MultipleExists<T>(IMapper<T> o)
    {
        ArgumentNullException.ThrowIfNull(o);
        if (o.ResultCount > 1)
        {
            return ComparisonResult.Success();
        }
        string error = o.ResultCount > 0
            ? $"Expected multiple {Describe(o.Paths)} but only one [{FormatMultiError(o)}]"
            : $"Expected multiple {Describe(o.ErrorPaths)} but found zero";
        return ComparisonResult.Failure(error);
    }

    /// <summary>
    /// True when every value of <paramref name="o2"/> appears in <paramref name="o1"/>.
    /// </summary>
    /// <remarks>
    /// Membership is scale-SENSITIVE for numbers, because Java uses <c>List.containsAll</c> and so
    /// <c>equals</c>. A list holding <c>1.0</c> does not contain <c>1.00</c>, even though
    /// <c>1.0 = 1.00</c> is true.
    /// </remarks>
    public static ComparisonResult Contains<T>(IMapper<T> o1, IMapper<T> o2)
    {
        ArgumentNullException.ThrowIfNull(o1);
        ArgumentNullException.ThrowIfNull(o2);
        IReadOnlyList<T> left = o1.GetMulti();
        IReadOnlyList<T> right = o2.GetMulti();
        if (left.Count == 0)
        {
            return ComparisonResult.Failure("Empty list does not contain all of " + FormatMultiError(o2));
        }
        if (right.Count == 0)
        {
            return ComparisonResult.Failure(FormatMultiError(o1) + " does not contain empty list");
        }
        bool all = right.All(r => left.Any(l => JavaEquals(l, r)));
        return all
            ? ComparisonResult.Success()
            : ComparisonResult.Failure(
                FormatMultiError(o1) + " does not contain all of " + FormatMultiError(o2));
    }

    /// <summary>True when the two mappers share no value. Scale-sensitive, like Contains.</summary>
    public static ComparisonResult Disjoint<T>(IMapper<T> o1, IMapper<T> o2)
    {
        ArgumentNullException.ThrowIfNull(o1);
        ArgumentNullException.ThrowIfNull(o2);
        IReadOnlyList<T> left = o1.GetMulti();
        IReadOnlyList<T> right = o2.GetMulti();
        var common = new List<T>();
        foreach (T item in left)
        {
            if (right.Any(r => JavaEquals(item, r)) && !common.Any(c => JavaEquals(c, item)))
            {
                common.Add(item);
            }
        }
        if (common.Count == 0)
        {
            return ComparisonResult.Success();
        }
        return ComparisonResult.Failure(
            FormatMultiError(o1) + " is not disjoint from " + FormatMultiError(o2)
            + "common items are " + RuneComparer.Describe(common));
    }

    /// <summary>
    /// The distinct values, treating numbers that differ only in scale as one.
    /// </summary>
    /// <remarks>
    /// Java is <c>HashSet.add</c> over the values, so <c>hashCode()</c> decides first and
    /// <c>equals()</c> only breaks ties within a bucket. That matters for one reason: a value read out
    /// of a function's own output is a <em>builder</em> there, and Java's generated <c>hashCode()</c>
    /// folds an unset list attribute as <c>1</c> on a builder (an empty <c>ArrayList</c>) and as
    /// <c>0</c> on a built object (a null field). So a builder never collides with a structurally
    /// identical built object, and <c>distinct</c> keeps both — even though <c>equals</c> says they
    /// are the same, because <c>ListEquals.listEquals</c> treats null and empty alike. CDM reaches
    /// this in <c>Create_ContractFormation</c>, whose <c>distinct</c> concatenates the instruction's
    /// legal agreements with the ones already on the output, and whose expectation therefore records
    /// every agreement twice.
    /// </remarks>
    public static MapperC<T> DistinctIgnoringPrecision<T>(IMapper<T> o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var seen = new List<(bool FromOutputBuilder, object? Key)>();
        var result = new List<(T Value, bool FromOutputBuilder)>();
        foreach (IMapperItem item in Items(o))
        {
            if (item.IsError)
            {
                continue;
            }

            bool builderHashBucket = item.FromOutputBuilder && item.Value is IRosettaModelObject;
            (bool, object?) key = (builderHashBucket, item.Value);
            if (!seen.Any(s => s.FromOutputBuilder == key.Item1 && Equals(s.Key, key.Item2)))
            {
                seen.Add(key);
                result.Add(((T)item.Value!, item.FromOutputBuilder));
            }
        }
        return MapperC<T>.OfWithBuilderFlags(result);
    }

    /// <summary>
    /// The mapper's items when it exposes them, and value-only items otherwise (a
    /// <see cref="ComparisonResult"/> is an <see cref="IMapper{T}"/> too).
    /// </summary>
    private static IEnumerable<IMapperItem> Items<T>(IMapper<T> mapper) =>
        mapper is Mapper.IMapperShape shape
            ? shape.Items
            : mapper.GetMulti().Select(v => (IMapperItem)new MapperItem<T>(
                v, MapperPath.Builder().AddNull(), v is null, null));

    /// <summary>
    /// Checks how many of a type's choice options are set.
    /// </summary>
    /// <remarks>
    /// Java reflects on <c>get&lt;Capitalised&gt;</c>. Here the lookup goes through
    /// <see cref="RunePropertyTable"/> keyed on the Rune attribute name, which sidesteps the
    /// property-renaming rule entirely — the C# property for <c>period</c> is <c>PeriodValue</c>,
    /// so a name-built accessor lookup would miss.
    /// </remarks>
    public static ComparisonResult Choice<T>(
        IMapper<T> mapper, IReadOnlyList<string> choiceFieldNames, ChoiceRuleValidationMethod necessity)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(choiceFieldNames);
        T value = mapper.Get();
        List<string> populated = PopulatedFields(value, choiceFieldNames);

        if (necessity.Check(populated.Count))
        {
            return ComparisonResult.Success();
        }
        string definition = necessity.GetDescription() + " of '" + string.Join("', '", choiceFieldNames) + "'. ";
        string detail = populated.Count == 0
            ? "No fields are set."
            : "Set fields are '" + string.Join("', '", populated) + "'.";
        return ComparisonResult.Failure(definition + detail);
    }

    /// <summary>Checks that exactly the required attributes, and no others, are set.</summary>
    public static ComparisonResult OnlyExists<T>(
        IMapper<T> mapper, IReadOnlyList<string> allFieldNames, IReadOnlyList<string> requiredFields)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(allFieldNames);
        ArgumentNullException.ThrowIfNull(requiredFields);
        IReadOnlyList<T> objects = mapper.GetMulti();
        string requiredMessage = "'" + string.Join("', '", requiredFields) + "'";

        if (objects.Count == 0)
        {
            return ComparisonResult.Failure(
                $"Expected only {requiredMessage} to be set, but object was absent.");
        }

        ComparisonResult accumulated = ComparisonResult.Success();
        foreach (T value in objects)
        {
            accumulated = accumulated.AndNullSafe(ValidateOnlyExists(value, allFieldNames, requiredFields, requiredMessage));
        }
        return accumulated;
    }

    private static ComparisonResult ValidateOnlyExists<T>(
        T value, IReadOnlyList<string> allFieldNames, IReadOnlyList<string> requiredFields, string requiredMessage)
    {
        List<string> populated = PopulatedFields(value, allFieldNames);
        if (new HashSet<string>(populated, StringComparer.Ordinal)
            .SetEquals(new HashSet<string>(requiredFields, StringComparer.Ordinal)))
        {
            return ComparisonResult.Success();
        }
        if (populated.Count == 0)
        {
            return ComparisonResult.Failure(
                $"Expected only {requiredMessage} to be set, but no fields were set.");
        }
        string setFields = "'" + string.Join("', '", populated) + "'";
        return ComparisonResult.Failure(
            $"Expected only {requiredMessage} to be set, but set fields are: {setFields}.");
    }

    private static List<string> PopulatedFields<T>(T value, IReadOnlyList<string> fieldNames)
    {
        var populated = new List<string>();
        if (value == null)
        {
            return populated;
        }
        RunePropertyTable table = RunePropertyTable.For(value.GetType());
        foreach (string name in fieldNames)
        {
            RunePropertyTable.Entry entry = table.FindByWireName(name)
                ?? throw new ArgumentException(
                    $"{value.GetType()} has no attribute '{name}'.", nameof(fieldNames));
            if (ExistenceChecker.IsSet(entry.Property.GetValue(value)))
            {
                populated.Add(name);
            }
        }
        return populated;
    }

    /// <summary>
    /// Scale-sensitive equality, as Java's <c>Object.equals</c>. Backs Contains and Disjoint,
    /// which use <c>List.containsAll</c> and <c>Collections.disjoint</c> rather than compareTo.
    /// </summary>
    internal static bool JavaEquals(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        if (left is decimal leftNumber && right is decimal rightNumber)
        {
            return RuneDecimal.EqualsIncludingScale(leftNumber, rightNumber);
        }
        return left.Equals(right);
    }

    /// <summary>
    /// Renders the values behind a mapper. A model object renders as its type name only — Java
    /// notes that logging a whole object makes the messages unreadable.
    /// </summary>
    /// <summary>Renders the values behind a mapper for a failure message.</summary>
    public static string FormatMultiError<T>(IMapper<T> o)
    {
        IReadOnlyList<T> values = o.GetMulti();
        object? first = values.Count > 0 ? values[0] : null;
        if (first is IRosettaModelObject)
        {
            return JavaTypeNames.Of(first) + "Impl";
        }
        return RuneComparer.Describe(values);
    }

    /// <summary>Renders a path list the way Java prints a List of Mapper.Path.</summary>
    public static string Describe(IReadOnlyList<IMapperPath> paths) =>
        "[" + string.Join(", ", paths.Select(p => p.FullPath)) + "]";
}
