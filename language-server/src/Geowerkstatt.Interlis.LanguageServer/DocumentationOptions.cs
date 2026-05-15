namespace Geowerkstatt.Interlis.LanguageServer;

public class DocumentationOptions
{
    public const string ConfigSection = "interlis.documentation";

    /// <summary>
    /// How to display attributes of abstract classes.
    /// "separate" = Show only in abstract class (default)
    /// "inline" = Repeat in each subclass
    /// </summary>
    public string AbstractClassAttributes { get; set; } = "separate";

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
}
