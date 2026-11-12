using Finos.Rune.Runtime.Metafields;

namespace Finos.Rune.Runtime.Meta;

/// <summary>Non-generic internal view of FieldWithMeta&lt;T&gt; used by the serialization
/// engine (choice options and flattening need wrapper access without knowing T).</summary>
internal interface IFieldWithMetaView
{
    object? ValueObject { get; }

    MetaFields? MetaObject { get; }

    Type ValueType { get; }
}

/// <summary>Non-generic internal view of ReferenceWithMeta&lt;T&gt;.</summary>
internal interface IReferenceWithMetaView
{
    object? ValueObject { get; }

    string? GlobalReferenceValue { get; }

    string? ExternalReferenceValue { get; }

    Reference? ScopedReferenceObject { get; }

    Type ValueType { get; }
}
