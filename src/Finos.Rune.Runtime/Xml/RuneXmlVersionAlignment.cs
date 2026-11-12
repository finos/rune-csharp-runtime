namespace Finos.Rune.Runtime.Xml;

/// <summary>
/// Which rune-common release this XML runtime reproduces, and the one place the two pinned versions
/// differ.
/// </summary>
/// <remarks>
/// <para>Two rune-common versions are in play. <see cref="OracleVersion"/> is
/// <c>cdm.pin</c>'s <c>RUNE_SERIALIZATION_VERSION</c> — the bundle the pinned <c>cdm-java</c>
/// artifact was built against, and therefore the behavioural oracle every conformance threshold is
/// measured against. <see cref="GeneratorVersion"/> is this repository's own
/// <c>rosetta.bundle.version</c>, one minor ahead, used at generation time to parse the rune-fpml
/// XML configuration.</para>
/// <para><b>The XML tier is the same code in both.</b> Every source file under
/// <c>com.regnosys.rosetta.common.serialisation.xml</c> — the config model, <c>SubstitutionMap</c>,
/// the annotation introspector, <c>RosettaXmlMapper</c>, <c>UnknownZoneProvider</c>,
/// <c>XMLContentModelMatcher</c>, <c>XMLContentModelDisambiguatingDeserializer</c>,
/// <c>VirtualPathBuilderHelper</c>, <c>XMLContentModelOrderer</c>, <c>RosettaBeanSerializer</c> and
/// the unwrapping serializers — is identical between the two tags apart from the copyright year in
/// its licence header, and so are the XML test fixtures. Nothing this runtime ports is
/// version-conditional: matching, ordering, occurrence keys, namespace resolution, VIRTUAL
/// flattening, substitution groups and lenient recovery are one behaviour, not two.</para>
/// <para><b>The single difference is the pretty-printed line separator.</b> 12.5.1 added
/// <c>RosettaObjectMapperCreator.platformIndependentPrettyPrinter</c>, which gives an
/// <c>XmlMapper</c> a <c>DefaultXmlPrettyPrinter().withCustomNewLine("\n")</c>. 12.3.1 sets no
/// default pretty printer, so Jackson's <c>DefaultXmlPrettyPrinter</c> separates lines with
/// <c>System.getProperty("line.separator")</c> — verified present in both jackson-dataformat-xml
/// 2.17.1 (12.3.1) and 2.18.8 (12.5.1). Indentation (two spaces), the empty-element form
/// (<c>&lt;a/&gt;</c>) and attribute ordering are unchanged. The two versions therefore emit
/// byte-identical XML on any platform whose separator is already <c>\n</c>, which is where the
/// pinned pack expectations were produced and where the gate runs.</para>
/// <para><b>Consequence for M8/M9.</b> Neither threshold byte-compares pretty-printed XML — M8
/// parses inputs, M9 compares the ingested CDM output tree-equal against the shipped expectations —
/// and the follow-on XML gate compares infosets, which ignore insignificant whitespace outright. So
/// the difference cannot move a tracked metric. <see cref="LineFeedNewLines"/> is the default and is
/// correct for the pinned pack; <see cref="PlatformNewLines"/> exists only for the case of a Java
/// oracle whose output was captured on a platform with a non-<c>\n</c> separator.</para>
/// <para>The residual risk that is <em>not</em> answerable from source is the Jackson bump itself
/// (2.17.1 to 2.18.8). It is environmental rather than semantic, and the honest way to settle it is
/// to compare against the actual pinned <c>cdm-java</c> output in the cross-language step rather
/// than to reason about Jackson internals.</para>
/// </remarks>
public static class RuneXmlVersionAlignment
{
    /// <summary>
    /// The rune-common bundle the pinned CDM Java oracle was built against
    /// (<c>cdm.pin</c>'s <c>RUNE_SERIALIZATION_VERSION</c>). Conformance reproduces this.
    /// </summary>
    public const string OracleVersion = "12.3.1";

    /// <summary>
    /// The rune-common bundle this repository's generator depends on
    /// (<c>pom.xml</c>'s <c>rosetta.bundle.version</c>). Used at generation time only.
    /// </summary>
    public const string GeneratorVersion = "12.5.1";

    /// <summary>
    /// Write options separating lines with <c>\n</c>: rune-common 12.5.1 on every platform, and
    /// 12.3.1 on any platform whose line separator is <c>\n</c>. This is what
    /// <see cref="XmlWriteOptions.Default"/> already does; the method exists so a caller can state
    /// the intent explicitly.
    /// </summary>
    public static XmlWriteOptions LineFeedNewLines(XmlWriteOptions? from = null) =>
        (from ?? XmlWriteOptions.Default).WithNewLine("\n");

    /// <summary>
    /// Write options separating lines with <see cref="Environment.NewLine"/>, reproducing
    /// rune-common 12.3.1 even on a platform whose separator is not <c>\n</c>. Only needed when
    /// byte-comparing against Java oracle output captured on such a platform.
    /// </summary>
    public static XmlWriteOptions PlatformNewLines(XmlWriteOptions? from = null) =>
        (from ?? XmlWriteOptions.Default).WithNewLine(Environment.NewLine);
}
