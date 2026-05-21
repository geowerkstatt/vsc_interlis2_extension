namespace Geowerkstatt.Interlis.LanguageServer;

public class DocumentationOptions
{
    public const string ConfigSection = "interlis.documentation";

    /// <summary>
    /// <see cref="AbstractClassAttributes"/> value: inherited attributes are
    /// shown only inside the parent class section, not repeated in subclasses.
    /// </summary>
    public const string AbstractClassAttributesSeparate = "separate";

    /// <summary>
    /// <see cref="AbstractClassAttributes"/> value: inherited attributes from
    /// every ancestor (abstract or concrete) are repeated inline inside each
    /// subclass table.
    /// </summary>
    public const string AbstractClassAttributesInline = "inline";

    /// <summary>
    /// <see cref="AbstractClassAttributes"/> value: inherited attributes are
    /// repeated inline inside each subclass, but only when they were declared
    /// on an abstract ancestor.
    /// </summary>
    public const string AbstractClassAttributesInlineAbstractOnly = "inlineAbstractOnly";

    /// <summary>
    /// How to display inherited attributes. One of
    /// <see cref="AbstractClassAttributesSeparate"/>,
    /// <see cref="AbstractClassAttributesInline"/>, or
    /// <see cref="AbstractClassAttributesInlineAbstractOnly"/>.
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
