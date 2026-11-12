namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Marks an attribute declared <c>[metadata id]</c> in the Rune model — Java's
/// <c>AttributeMeta.GLOBAL_KEY_FIELD</c>.
/// </summary>
/// <remarks>
/// <para>
/// This flag does two unrelated jobs in the global-key tier, which is why it has to be visible at
/// runtime rather than inferred from the property's type:
/// </para>
/// <list type="number">
/// <item>
/// It <b>excludes</b> the attribute from its parent's hash entirely — value and structure both. So
/// <c>Trade</c>'s key is taken over a <c>Trade</c> with <c>tradeDate</c> and <c>tradeTime</c>
/// removed.
/// </item>
/// <item>
/// It is the <b>only</b> way a metadata wrapper earns a key of its own. Every
/// <c>FieldWithMeta&lt;T&gt;</c> is a global-key carrier by type, but the key is assigned only when
/// the attribute it hangs off is marked here — which is why <c>Trade.tradeDate</c> serializes as
/// <c>{"@key":"3f1101","@data":"2018-04-01"}</c> while <c>Party.name</c> is a bare
/// <c>{"@data":"Fund 2"}</c>.
/// </item>
/// </list>
/// <para>
/// Nothing else distinguishes <c>[metadata id]</c> in the generated C#: <c>id</c>, <c>scheme</c> and
/// <c>location</c> all collapse to the same <c>FieldWithMeta&lt;T&gt;</c> property type, and only
/// <c>location</c> had a marker before this. CDM 7 declares it on ten attributes across eight types.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RuneGlobalKeyFieldAttribute : Attribute
{
}
