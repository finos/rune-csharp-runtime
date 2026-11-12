namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// An XML qualified name: an optional namespace URI plus a local name.
/// </summary>
/// <remarks>
/// The Rune XML configuration writes a fully-qualified element name as
/// <c>&lt;namespace&gt;/&lt;localName&gt;</c> (for example <c>urn:my.schema/animal</c>), the same
/// single-string form rune-common's <c>SubstitutionMap.XMLFullyQualifiedName</c> parses with
/// <c>^(.+)/(.+)$</c>. <see cref="Parse"/> reproduces that split, including its greedy first
/// group, so <c>urn:a/b/c</c> is namespace <c>urn:a/b</c> and local name <c>c</c>.
/// </remarks>
public readonly struct XmlQName : IEquatable<XmlQName>
{
    /// <summary>The namespace URI, or null when the name is unqualified.</summary>
    public string? Namespace { get; }

    /// <summary>The local (unprefixed) element or attribute name.</summary>
    public string LocalName { get; }

    public XmlQName(string localName)
        : this(null, localName)
    {
    }

    public XmlQName(string? ns, string localName)
    {
        ArgumentNullException.ThrowIfNull(localName);
        Namespace = string.IsNullOrEmpty(ns) ? null : ns;
        LocalName = localName;
    }

    /// <summary>True when this name carries a namespace URI.</summary>
    public bool HasNamespace => Namespace != null;

    /// <summary>
    /// Splits a fully-qualified <c>namespace/localName</c> string. A string with no slash is
    /// taken to be a bare local name.
    /// </summary>
    public static XmlQName Parse(string fullyQualifiedName)
    {
        ArgumentNullException.ThrowIfNull(fullyQualifiedName);
        int slash = fullyQualifiedName.LastIndexOf('/');
        if (slash <= 0 || slash == fullyQualifiedName.Length - 1)
        {
            return new XmlQName(fullyQualifiedName);
        }
        return new XmlQName(fullyQualifiedName[..slash], fullyQualifiedName[(slash + 1)..]);
    }

    /// <summary>The name with its namespace dropped.</summary>
    public XmlQName WithoutNamespace() => new XmlQName(LocalName);

    /// <summary>
    /// Local-name equality that additionally requires the namespaces to agree whenever both are
    /// known. An absent namespace on either side matches anything, which is what the reader needs
    /// for documents that do not declare one.
    /// </summary>
    public bool MatchesLoosely(XmlQName other) =>
        string.Equals(LocalName, other.LocalName, StringComparison.Ordinal)
        && (Namespace == null || other.Namespace == null
            || string.Equals(Namespace, other.Namespace, StringComparison.Ordinal));

    public bool Equals(XmlQName other) =>
        string.Equals(LocalName, other.LocalName, StringComparison.Ordinal)
        && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is XmlQName other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Namespace, LocalName);

    public override string ToString() => Namespace == null ? LocalName : Namespace + "/" + LocalName;

    public static bool operator ==(XmlQName left, XmlQName right) => left.Equals(right);

    public static bool operator !=(XmlQName left, XmlQName right) => !left.Equals(right);
}
