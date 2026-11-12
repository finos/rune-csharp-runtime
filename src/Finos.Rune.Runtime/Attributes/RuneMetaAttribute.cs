namespace Finos.Rune.Runtime.Attributes;

/// <summary>
/// Points a generated model type at its metadata companion, the class holding its validators.
/// Mirrors com.rosetta.model.lib.annotations.RosettaMeta.
/// </summary>
/// <remarks>
/// Java reaches the companion through <c>RosettaModelObject.metaData()</c>, an instance method on
/// every generated pojo. An attribute is used here instead so the validation tier stays additive:
/// generated types gain one line and no new member, and a model without validators is simply a
/// model without the attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RuneMetaAttribute : Attribute
{
    public RuneMetaAttribute(Type metaType)
    {
        MetaType = metaType;
    }

    /// <summary>The companion type, which implements <c>IRosettaMetaData&lt;T&gt;</c>.</summary>
    public Type MetaType { get; }
}
