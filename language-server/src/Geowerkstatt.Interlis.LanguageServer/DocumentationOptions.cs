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
}
