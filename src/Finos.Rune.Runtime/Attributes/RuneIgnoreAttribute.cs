namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Excludes a member from Rune JSON binding. Emitted on legacy-signature members that
/// exist only to satisfy a base type's contract when an attribute override narrows the
/// type or cardinality; the member carrying RuneAttribute wins.
/// Mirrors com.rosetta.model.lib.annotations.RuneIgnore.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = false)]
public sealed class RuneIgnoreAttribute : Attribute
{
}
