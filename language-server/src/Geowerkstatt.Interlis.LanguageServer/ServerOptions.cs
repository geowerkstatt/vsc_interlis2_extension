namespace Geowerkstatt.Interlis.LanguageServer;

public class ServerOptions
{
    public const string ConfigSection = "LanguageServer";

    public required string LanguageName { get; set; }

    public required string TempFolderName { get; set; }
}
