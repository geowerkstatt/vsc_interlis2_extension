namespace Geowerkstatt.Interlis.LanguageServer;

public class DocumentationOptions
{
    public const string ConfigSection = "interlis.documentation";

    /// <summary>
    /// <see cref="AbstractClassAttributes"/> value: inherited attributes from
    /// abstract parents are shown only inside the abstract class section.
    /// </summary>
    public const string AbstractClassAttributesSeparate = "separate";

    /// <summary>
    /// <see cref="AbstractClassAttributes"/> value: inherited attributes from
    /// abstract parents are repeated inline inside each concrete subclass.
    /// </summary>
    public const string AbstractClassAttributesInline = "inline";

    /// <summary>
    /// How to display attributes of abstract classes. One of
    /// <see cref="AbstractClassAttributesSeparate"/> or
    /// <see cref="AbstractClassAttributesInline"/>.
    /// </summary>
    public string AbstractClassAttributes { get; set; } = AbstractClassAttributesSeparate;

    /// <summary>
    /// Header for the attribute-name column in the generated markdown table.
    /// </summary>
    public string AttributeNameHeader { get; set; } = "Attributname";

    /// <summary>
    /// Header for the cardinality column in the generated markdown table.
    /// </summary>
    public string CardinalityHeader { get; set; } = "Kardinalität";

    /// <summary>
    /// Header for the type column in the generated markdown table.
    /// </summary>
    public string TypeHeader { get; set; } = "Typ";

    /// <summary>
    /// Placeholder text shown for a class that has no attributes. The visitor
    /// wraps this with markdown italics or HTML emphasis depending on context.
    /// </summary>
    public string EmptyClassPlaceholder { get; set; } = "keine Attribute in dieser Klasse";
}
