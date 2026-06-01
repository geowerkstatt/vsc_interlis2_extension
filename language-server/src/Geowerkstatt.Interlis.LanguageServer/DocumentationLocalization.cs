namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Localized labels for the generated markdown documentation and Mermaid
/// diagrams. Only labels we add appear here; user-defined INTERLIS identifiers
/// and INTERLIS keywords (TEXT, BOOLEAN, SURFACE, ...) stay as written.
/// </summary>
public class DocumentationLocalization
{
    public const string German = "de";
    public const string French = "fr";
    public const string Italian = "it";
    public const string English = "en";

    public string Language { get; }
    public string AttributeNameHeader { get; }
    public string CardinalityHeader { get; }
    public string TypeHeader { get; }
    public string EmptyClassPlaceholder { get; }
    public string InheritedSuffix { get; }
    public string NumericLabel { get; }
    public string BlackboxBinarySuffix { get; }
    public string BlackboxXmlSuffix { get; }

    private DocumentationLocalization(
        string language,
        string attributeNameHeader,
        string cardinalityHeader,
        string typeHeader,
        string emptyClassPlaceholder,
        string inheritedSuffix,
        string numericLabel,
        string blackboxBinarySuffix,
        string blackboxXmlSuffix)
    {
        Language = language;
        AttributeNameHeader = attributeNameHeader;
        CardinalityHeader = cardinalityHeader;
        TypeHeader = typeHeader;
        EmptyClassPlaceholder = emptyClassPlaceholder;
        InheritedSuffix = inheritedSuffix;
        NumericLabel = numericLabel;
        BlackboxBinarySuffix = blackboxBinarySuffix;
        BlackboxXmlSuffix = blackboxXmlSuffix;
    }

    /// <summary>
    /// Returns the bundle for the given language code. Falls back to German
    /// for any unknown value so legacy behavior is preserved.
    /// </summary>
    public static DocumentationLocalization For(string? language) =>
        language switch
        {
            English => EnglishBundle,
            French => FrenchBundle,
            Italian => ItalianBundle,
            _ => GermanBundle,
        };

    private static readonly DocumentationLocalization GermanBundle = new(
        language: German,
        attributeNameHeader: "Attributname",
        cardinalityHeader: "Kardinalität",
        typeHeader: "Typ",
        emptyClassPlaceholder: "keine Attribute in dieser Klasse",
        inheritedSuffix: "(geerbt)",
        numericLabel: "Numerisch",
        blackboxBinarySuffix: "Binär",
        blackboxXmlSuffix: "XML");

    private static readonly DocumentationLocalization EnglishBundle = new(
        language: English,
        attributeNameHeader: "Attribute name",
        cardinalityHeader: "Cardinality",
        typeHeader: "Type",
        emptyClassPlaceholder: "no attributes in this class",
        inheritedSuffix: "(inherited)",
        numericLabel: "Numeric",
        blackboxBinarySuffix: "Binary",
        blackboxXmlSuffix: "XML");

    private static readonly DocumentationLocalization FrenchBundle = new(
        language: French,
        attributeNameHeader: "Nom de l'attribut",
        cardinalityHeader: "Cardinalité",
        typeHeader: "Type",
        emptyClassPlaceholder: "aucun attribut dans cette classe",
        inheritedSuffix: "(hérité)",
        numericLabel: "Numérique",
        blackboxBinarySuffix: "Binaire",
        blackboxXmlSuffix: "XML");

    private static readonly DocumentationLocalization ItalianBundle = new(
        language: Italian,
        attributeNameHeader: "Nome dell'attributo",
        cardinalityHeader: "Cardinalità",
        typeHeader: "Tipo",
        emptyClassPlaceholder: "nessun attributo in questa classe",
        inheritedSuffix: "(ereditato)",
        numericLabel: "Numerico",
        blackboxBinarySuffix: "Binario",
        blackboxXmlSuffix: "XML");
}