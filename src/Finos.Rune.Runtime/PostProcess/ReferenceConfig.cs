using Finos.Rune.Runtime.Path;

namespace Finos.Rune.Runtime.PostProcess;

/// <summary>
/// How reference resolution is scoped for a particular model — Java's <c>ReferenceConfig</c>.
/// </summary>
/// <remarks>
/// <para>
/// Global references are resolved document-wide, but <em>scoped</em> references are resolved only within
/// an enclosing region of the tree, and which type opens such a region is a property of the model rather
/// than of the runtime. Carrying it as a <see cref="Type"/> is what lets <c>Finos.Rune.Runtime</c> stay
/// model-agnostic; the CDM instance lives beside the generated model, exactly as Java splits
/// <c>ReferenceConfig</c> (in rune-common) from <c>CdmReferenceConfig</c> (in the CDM artifact).
/// </para>
/// </remarks>
public sealed class ReferenceConfig
{
    public ReferenceConfig(Type? scopeType, IReadOnlyList<RosettaPath> excludedPaths)
    {
        ScopeType = scopeType;
        ExcludedPaths = excludedPaths ?? throw new ArgumentNullException(nameof(excludedPaths));
    }

    /// <summary>
    /// The type that opens a scope for scoped references, or null for none.
    /// </summary>
    /// <remarks>
    /// Null is a supported configuration, not a missing value: with no scope type nothing registers a
    /// scope and every scoped key lands in one document-wide bucket. Java names that case explicitly
    /// (<c>noScopeOrExcludedPaths</c>), which is why this is nullable rather than required.
    /// </remarks>
    public Type? ScopeType { get; }

    /// <summary>
    /// Subtrees resolution does not enter, matched by trailing path element.
    /// </summary>
    /// <remarks>
    /// CDM excludes exactly one, <c>lineage</c> — a back-pointer whose references would otherwise be
    /// followed back up the tree.
    /// </remarks>
    public IReadOnlyList<RosettaPath> ExcludedPaths { get; }

    /// <summary>Resolve globally, scope nothing, exclude nothing.</summary>
    public static ReferenceConfig NoScopeOrExcludedPaths { get; } =
        new ReferenceConfig(null, Array.Empty<RosettaPath>());
}
