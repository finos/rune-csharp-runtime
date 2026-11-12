using System.Collections;
using Finos.Rune.Runtime.Metafields;
using Finos.Rune.Runtime.Path;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Validation;

/// <summary>
/// Validates a Rune model object graph.
/// </summary>
/// <remarks>
/// <para>Entry point for consumers: <c>trade.Validate(serviceProvider)</c>.</para>
/// <para>The traversal reproduces Java's exactly, because the parity sweep compares result
/// sequences positionally. Java drives it through a generated
/// <c>process(RosettaPath, Processor)</c> visitor on every model object; C# has property metadata
/// and reflection instead, so the walk is driven by
/// <see cref="RunePropertyTable.DeclarationOrderEntries"/> — which is built to match that
/// visitor's order. See <c>RosettaTypeValidator</c>, <c>SimpleProcessor</c> and
/// <c>RosettaModelObject.processRosetta</c> in the Java runtime.</para>
/// <para>Validate built objects, not builders. Builders carry no wire metadata, so the walk
/// cannot see their attributes.</para>
/// </remarks>
public static class RuneValidation
{
    /// <summary>Validates <paramref name="instance"/> through a configured service provider.</summary>
    public static ValidationReport Validate<T>(
        this T instance, IServiceProvider services, RuneValidationOptions? options = null)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(services);
        return GetModelValidator(services).Validate(instance, options);
    }

    /// <summary>Validates and throws through a configured service provider.</summary>
    public static void ValidateOrThrow<T>(
        this T instance, IServiceProvider services, RuneValidationOptions? options = null)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(services);
        GetModelValidator(services).ValidateOrThrow(instance, options);
    }

    internal static ValidationReport Validate<T>(
        T instance, IValidatorFactory factory, RuneValidationOptions options)
        where T : class, IRosettaModelObject
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);

        var results = new List<IValidationResult>();
        var walker = new Walker(factory, options, results);
        RosettaPath root = RosettaPath.ValueOf(SimpleName(instance.GetType()));
        walker.Visit(root, instance);
        walker.Descend(root, instance);
        return new ValidationReport(instance, results);
    }

    private static IRuneModelValidator GetModelValidator(IServiceProvider services)
    {
        object? service = services.GetService(typeof(IRuneModelValidator));
        if (service is IRuneModelValidator validator)
        {
            return validator;
        }
        throw new InvalidOperationException(
            $"Required service {typeof(IRuneModelValidator).FullName} is not registered. "
            + "Register the generated Rune model services before validating.");
    }

    private static string SimpleName(Type type) => RuneTypeName.Simple(type);

    private sealed class Walker(
        IValidatorFactory factory, RuneValidationOptions options, List<IValidationResult> results)
    {
        /// <summary>The scope literal MetaFields.setScopedKey stamps on the Key it materializes.</summary>
        private const string JavaScopedKeyScope = "DOCUMENT";

        /// <summary>
        /// Collects one node's own results. Mirrors
        /// <c>RosettaTypeProcessor.processRosetta</c>: a null node yields nothing and, by
        /// returning false, stops the caller descending into it.
        /// </summary>
        internal bool Visit(RosettaPath path, object? instance)
        {
            if (instance == null)
            {
                return false;
            }
            IRosettaMetaData? metaData = RuneMetaRegistry.For(instance.GetType());
            if (metaData != null)
            {
                if (options.IncludeSuccesses)
                {
                    metaData.CollectValidationResults(path, instance, factory, results);
                }
                else
                {
                    var collected = new List<IValidationResult>();
                    metaData.CollectValidationResults(path, instance, factory, collected);
                    results.AddRange(collected.Where(r => !r.IsSuccess));
                }
            }
            return true;
        }

        /// <summary>Walks a node's attributes. Mirrors the generated <c>process()</c>.</summary>
        internal void Descend(RosettaPath path, object instance)
        {
            RunePropertyTable table = RunePropertyTable.For(instance.GetType());
            foreach (RunePropertyTable.Entry entry in table.DeclarationOrderEntries)
            {
                if (!typeof(IRosettaModelObject).IsAssignableFrom(entry.DeclaredType))
                {
                    continue;
                }
                RosettaPath sub = path.NewSubPath(entry.PathName);
                object? value = entry.Property.GetValue(instance);
                if (entry.ListElementType != null)
                {
                    ProcessList(sub, value as IEnumerable);
                }
                else
                {
                    ProcessSingle(sub, value);
                }
            }

            if (instance is MetaFields metaFields)
            {
                ProcessSynthesizedKeys(path, metaFields);
            }
        }

        /// <summary>
        /// Walks the <c>key</c> list that Java's MetaFields has and C#'s does not.
        /// </summary>
        /// <remarks>
        /// <para>Java stores scoped keys as <c>List&lt;Key&gt;</c> and derives <c>getScopedKey()</c>
        /// from <c>key.get(0).getKeyValue()</c>; <c>setScopedKey(s)</c> materializes exactly one
        /// <c>Key</c> with scope <c>"DOCUMENT"</c>, and clears the list when <c>s</c> is null.
        /// C# keeps only the flattened string, because that is what the wire carries. The two
        /// serialize identically, but the Java walk visits a Key node that C# would otherwise
        /// miss — and Key is the only source of <see cref="ValidationType.Key"/> results.</para>
        /// <para><c>@key</c> and <c>@key:external</c> are plain strings on both sides and produce
        /// no Key node. This runs after the attribute loop because Java emits <c>key</c> last in
        /// <c>MetaFields.process()</c>, behind five no-op <c>processBasic</c> calls.</para>
        /// </remarks>
        private void ProcessSynthesizedKeys(RosettaPath path, MetaFields metaFields)
        {
            if (metaFields.ScopedKey == null)
            {
                return;
            }
            Key key = Key.CreateBuilder()
                .SetKeyValue(metaFields.ScopedKey)
                .SetScope(JavaScopedKeyScope)
                .Build();
            ProcessList(path.NewSubPath("key"), new[] { key });
        }

        /// <summary>Mirrors <c>RosettaModelObject.processRosetta</c> for a single child.</summary>
        private void ProcessSingle(RosettaPath path, object? child)
        {
            bool processFurther = Visit(path, child);
            if (child != null && processFurther)
            {
                Descend(path, child);
            }
        }

        /// <summary>
        /// Mirrors <c>SimpleProcessor.processRosetta(list)</c> followed by
        /// <c>RosettaModelObject.processRosetta(list)</c>.
        /// </summary>
        /// <remarks>
        /// <para>Two passes, and three details that are easy to get wrong. Every element is first
        /// visited for its own results, all of them before any descent — results are not
        /// interleaved element by element. The list processor ANDs each element's outcome and a
        /// null yields false, so a single null anywhere suppresses descent into every element of
        /// the list.</para>
        /// <para>The two passes number elements differently: the first uses the raw loop index,
        /// the second increments only for non-null elements. They can only disagree on a list
        /// containing a null — which is also a list that never reaches the second pass, so the
        /// divergence is unobservable here. It is reproduced anyway, because the post-processing
        /// tier reuses this walk and does reach it.</para>
        /// </remarks>
        private void ProcessList(RosettaPath path, IEnumerable? children)
        {
            if (children == null)
            {
                return;
            }
            List<object?> items = children.Cast<object?>().ToList();

            bool processFurther = true;
            for (int i = 0; i < items.Count; i++)
            {
                processFurther &= Visit(path.WithIndex(i), items[i]);
            }
            if (!processFurther)
            {
                return;
            }
            int index = 0;
            foreach (object? child in items)
            {
                if (child != null)
                {
                    Descend(path.WithIndex(index), child);
                    index++;
                }
            }
        }
    }
}
