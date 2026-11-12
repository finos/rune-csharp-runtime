using Finos.Rune.Runtime.Attributes;

namespace Finos.Rune.Runtime.Metafields;

/// <summary>
/// A scoped key definition. Mirrors com.rosetta.model.lib.meta.Key. On the Rune wire
/// scoped keys surface as MetaFields.ScopedKey ("@key:scoped") with scope "DOCUMENT";
/// Key itself never appears as an object.
/// </summary>
[RuneMeta(typeof(Validation.KeyMeta))]
public sealed class Key : RosettaModelObject
{
    private Key(Builder builder)
    {
        Scope = builder.Scope;
        KeyValue = builder.KeyValue;
    }

    /// <summary>
    /// Key scope. Not wire-bound (Java annotates it @RosettaAttribute("scope") but not
    /// @RuneAttribute), so it never serializes.
    /// </summary>
    public string? Scope { get; }

    /// <summary>The key value — the wire-bound attribute.</summary>
    [RuneAttribute("value")]
    public string? KeyValue { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetScope(Scope);
        builder.SetKeyValue(KeyValue);
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? Scope { get; private set; }

        public string? KeyValue { get; private set; }

        public Builder SetScope(string? scope)
        {
            Scope = scope;
            return this;
        }

        public Builder SetKeyValue(string? keyValue)
        {
            KeyValue = keyValue;
            return this;
        }

        public override Key Build() => new Key(this);

        public override Builder Prune() => this;

        /// <summary>
        /// Only the wire-bound key value counts as data, mirroring
        /// KeyBuilderImpl.hasData() (<c>keyValue != null</c>); a scope on its own has
        /// nothing to emit.
        /// </summary>
        public override bool HasData() => KeyValue != null;
    }
}
