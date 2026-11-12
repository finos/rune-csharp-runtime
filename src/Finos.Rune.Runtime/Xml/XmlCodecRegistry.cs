namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// The dispatch table the reader and writer use: Rune type name to codec, root element name to
/// codec, and substitution group membership. Populated explicitly by generated registration code;
/// nothing here scans assemblies or reflects over types.
/// </summary>
public sealed class XmlCodecRegistry
{
    private readonly Dictionary<string, IXmlTypeCodec> _byTypeName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, IXmlTypeCodec> _byValueType = new();
    private readonly Dictionary<XmlQName, IXmlTypeCodec> _byElementName = new();
    private readonly Dictionary<string, List<IXmlTypeCodec>> _byElementLocalName = new(StringComparer.Ordinal);
    private readonly Dictionary<XmlQName, Dictionary<XmlQName, IXmlTypeCodec>> _substitutions = new();
    private readonly Dictionary<XmlQName, List<IXmlTypeCodec>> _substitutionMembers = new();

    /// <summary>
    /// Registers a codec under its type name, its value type, its element name (when it has one)
    /// and its substitution group (when it belongs to one).
    /// </summary>
    public XmlCodecRegistry Register(IXmlTypeCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        XmlTypeDescriptor descriptor = codec.Descriptor;
        _byTypeName[descriptor.TypeName] = codec;
        _byValueType[codec.ValueType] = codec;

        XmlQName? elementName = descriptor.ElementName ?? descriptor.Substitution?.ElementName;
        if (elementName.HasValue)
        {
            _byElementName[elementName.Value] = codec;
            if (!_byElementLocalName.TryGetValue(elementName.Value.LocalName, out List<IXmlTypeCodec>? bucket))
            {
                bucket = new List<IXmlTypeCodec>();
                _byElementLocalName[elementName.Value.LocalName] = bucket;
            }
            bucket.Add(codec);
        }

        XmlSubstitutionEntry? substitution = descriptor.Substitution;
        if (substitution != null)
        {
            if (!_substitutions.TryGetValue(substitution.Group, out Dictionary<XmlQName, IXmlTypeCodec>? group))
            {
                group = new Dictionary<XmlQName, IXmlTypeCodec>();
                _substitutions[substitution.Group] = group;
                _substitutionMembers[substitution.Group] = new List<IXmlTypeCodec>();
            }
            bool replacing = group.ContainsKey(substitution.ElementName);
            group[substitution.ElementName] = codec;
            List<IXmlTypeCodec> members = _substitutionMembers[substitution.Group];
            int existing = replacing
                ? members.FindIndex(m => m.Descriptor.Substitution?.ElementName == substitution.ElementName)
                : -1;
            if (existing >= 0)
            {
                members[existing] = codec;
            }
            else
            {
                members.Add(codec);
            }
        }
        return this;
    }

    /// <summary>Registers several codecs in order.</summary>
    public XmlCodecRegistry RegisterAll(params IXmlTypeCodec[] codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        foreach (IXmlTypeCodec codec in codecs)
        {
            Register(codec);
        }
        return this;
    }

    public IXmlTypeCodec? FindByTypeName(string typeName) =>
        _byTypeName.TryGetValue(typeName, out IXmlTypeCodec? codec) ? codec : null;

    public IXmlTypeCodec? FindByValueType(Type valueType) =>
        _byValueType.TryGetValue(valueType, out IXmlTypeCodec? codec) ? codec : null;

    /// <summary>
    /// Resolves a document root element. An exact namespace match wins; otherwise the local name
    /// is used, which is what documents that declare no namespace need.
    /// </summary>
    public IXmlTypeCodec? FindRoot(XmlQName elementName)
    {
        if (_byElementName.TryGetValue(elementName, out IXmlTypeCodec? exact))
        {
            return exact;
        }
        if (elementName.HasNamespace && _byElementName.TryGetValue(elementName.WithoutNamespace(), out IXmlTypeCodec? unqualified))
        {
            return unqualified;
        }
        return SingleByLocalName(elementName.LocalName);
    }

    /// <summary>
    /// Resolves one member of a substitution group from the element name found in the document.
    /// An exact namespace match wins over a local-name match, so an unprefixed element in the base
    /// schema's default namespace never resolves to an extension type of the same local name.
    /// </summary>
    /// <remarks>
    /// The local-name fallback scans <see cref="SubstitutionMembers"/>, which is registration order,
    /// and takes the first match. That is the same "first registered wins" rule the Java reference
    /// applies when a namespace-less element leaves several candidates, and unlike iterating the
    /// element-name map it does not depend on hash ordering.
    /// </remarks>
    public IXmlTypeCodec? FindSubstitute(XmlQName group, XmlQName elementName)
    {
        var candidates = new List<IXmlTypeCodec>();
        CollectConcreteSubstitutes(group, candidates, new HashSet<XmlQName>());
        foreach (IXmlTypeCodec candidate in candidates)
        {
            if (candidate.Descriptor.Substitution?.ElementName == elementName)
            {
                return candidate;
            }
        }
        foreach (IXmlTypeCodec candidate in candidates)
        {
            XmlQName? candidateName = candidate.Descriptor.Substitution?.ElementName;
            if (candidateName != null
                && string.Equals(candidateName.Value.LocalName, elementName.LocalName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolves an <c>elementRef</c>. The reference may name one declared element directly or a
    /// substitution-group head, including a head whose concrete members sit beneath another
    /// abstract head.
    /// </summary>
    public IXmlTypeCodec? FindElementReference(XmlQName reference, XmlQName elementName)
    {
        if (_byElementName.TryGetValue(reference, out IXmlTypeCodec? direct)
            && !direct.Descriptor.IsAbstract
            && reference.MatchesLoosely(elementName))
        {
            return direct;
        }
        return FindSubstitute(reference, elementName);
    }

    /// <summary>The members of a substitution group, in registration order.</summary>
    public IReadOnlyList<IXmlTypeCodec> SubstitutionMembers(XmlQName group) =>
        _substitutionMembers.TryGetValue(group, out List<IXmlTypeCodec>? members)
            ? members
            : Array.Empty<IXmlTypeCodec>();

    /// <summary>
    /// True when several members of <paramref name="group"/> are registered under the same element
    /// local name, so a bare local name cannot identify one of them on a read.
    /// </summary>
    public bool IsSubstitutionLocalNameAmbiguous(XmlQName group, string localName)
    {
        ArgumentNullException.ThrowIfNull(localName);
        if (!_substitutions.TryGetValue(group, out Dictionary<XmlQName, IXmlTypeCodec>? members))
        {
            return false;
        }
        int matches = 0;
        foreach (XmlQName elementName in members.Keys)
        {
            if (string.Equals(elementName.LocalName, localName, StringComparison.Ordinal) && ++matches > 1)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The element name a substituted value is written as, under the supplied qualification policy.
    /// </summary>
    /// <remarks>
    /// The Java reference writes <c>SubstitutionMap.getSubstitutedName</c>, which is the local name
    /// alone. <see cref="XmlSubstitutionNamespacePolicy.WhenAmbiguous"/> — the default — reproduces
    /// that wherever the local name identifies exactly one member, and keeps the namespace only
    /// where dropping it would make the element unreadable.
    /// </remarks>
    public XmlQName? SubstitutedElementName(
        RosettaModelObject value,
        XmlSubstitutionNamespacePolicy policy = XmlSubstitutionNamespacePolicy.WhenAmbiguous)
    {
        ArgumentNullException.ThrowIfNull(value);
        IXmlTypeCodec? codec = FindByValueType(value.GetType());
        XmlSubstitutionEntry? substitution = codec?.Descriptor.Substitution;
        XmlQName? name = substitution?.ElementName ?? codec?.Descriptor.ElementName;
        if (name == null)
        {
            return null;
        }
        bool qualify = policy switch
        {
            XmlSubstitutionNamespacePolicy.Always => true,
            XmlSubstitutionNamespacePolicy.WhenAmbiguous => substitution != null
                && IsSubstitutionLocalNameAmbiguous(substitution.Group, name.Value.LocalName),
            _ => false,
        };
        return qualify ? name.Value : name.Value.WithoutNamespace();
    }

    private IXmlTypeCodec? SingleByLocalName(string localName)
    {
        if (!_byElementLocalName.TryGetValue(localName, out List<IXmlTypeCodec>? bucket) || bucket.Count == 0)
        {
            return null;
        }
        return bucket[0];
    }

    private void CollectConcreteSubstitutes(
        XmlQName group, List<IXmlTypeCodec> candidates, HashSet<XmlQName> visited)
    {
        if (!visited.Add(group) || !_substitutionMembers.TryGetValue(group, out List<IXmlTypeCodec>? members))
        {
            return;
        }
        foreach (IXmlTypeCodec candidate in members)
        {
            XmlSubstitutionEntry? substitution = candidate.Descriptor.Substitution;
            if (substitution == null)
            {
                continue;
            }
            if (!candidate.Descriptor.IsAbstract && !substitution.IsAbstract)
            {
                candidates.Add(candidate);
            }
            CollectConcreteSubstitutes(substitution.ElementName, candidates, visited);
        }
    }
}
