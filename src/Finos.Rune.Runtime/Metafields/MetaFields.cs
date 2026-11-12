using Finos.Rune.Runtime.Attributes;
using Finos.Rune.Runtime.Serialization;

namespace Finos.Rune.Runtime.Metafields;

/// <summary>
/// Metadata container attached to model values carrying [metadata ...] annotations.
/// Mirrors com.rosetta.model.metafields.MetaFields (hand-written in the Java runtime
/// since DSL 9.24); the deprecated template/location/address surface is not ported.
/// Serialized unwrapped into its parent: scheme → "@scheme", globalKey → "@key",
/// externalKey → "@key:external", scopedKey → "@key:scoped".
/// </summary>
public sealed class MetaFields : RosettaModelObject
{
    private MetaFields(Builder builder)
    {
        Scheme = builder.Scheme;
        GlobalKey = builder.GlobalKey;
        ExternalKey = builder.ExternalKey;
        ScopedKey = builder.ScopedKey;
    }

    // Path names follow Java's processor paths, so scopedKey's path is "location".
    [RuneAttribute(WireNames.Scheme)]
    [RunePathName("scheme")]
    public string? Scheme { get; }

    [RuneAttribute(WireNames.Key)]
    [RunePathName("globalKey")]
    public string? GlobalKey { get; }

    [RuneAttribute(WireNames.ExternalKey)]
    [RunePathName("externalKey")]
    public string? ExternalKey { get; }

    [RuneAttribute(WireNames.ScopedKey)]
    [RunePathName("location")]
    public string? ScopedKey { get; }

    public static Builder CreateBuilder() => new Builder();

    public override Builder ToBuilder()
    {
        var builder = new Builder();
        builder.SetScheme(Scheme);
        builder.SetGlobalKey(GlobalKey);
        builder.SetExternalKey(ExternalKey);
        builder.SetScopedKey(ScopedKey);
        return builder;
    }

    public sealed class Builder : RosettaModelObjectBuilder
    {
        public string? Scheme { get; private set; }

        public string? GlobalKey { get; private set; }

        public string? ExternalKey { get; private set; }

        public string? ScopedKey { get; private set; }

        public Builder SetScheme(string? scheme)
        {
            Scheme = scheme;
            return this;
        }

        public Builder SetGlobalKey(string? globalKey)
        {
            GlobalKey = globalKey;
            return this;
        }

        public Builder SetExternalKey(string? externalKey)
        {
            ExternalKey = externalKey;
            return this;
        }

        public Builder SetScopedKey(string? scopedKey)
        {
            ScopedKey = scopedKey;
            return this;
        }

        public override MetaFields Build() => new MetaFields(this);

        public override Builder Prune() => this;

        public override bool HasData() =>
            Scheme != null || GlobalKey != null || ExternalKey != null || ScopedKey != null;
    }
}
