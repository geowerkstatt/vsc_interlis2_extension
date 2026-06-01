namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Holds the UI language reported by the language client at initialization
/// time. Used as the fallback when the workspace setting
/// "interlis.documentation.language" is "auto" or absent.
/// </summary>
public class UiLanguageContext
{
    public const string AutoLanguage = "auto";

    /// <summary>
    /// Language code (e.g. <c>de</c>, <c>fr</c>) provided by the client. The
    /// client is expected to normalize the value to one of the supported
    /// codes; unknown values fall back to German via
    /// <see cref="DocumentationLocalization.For(string?)"/>.
    /// </summary>
    public string Language { get; set; } = DocumentationLocalization.German;

    /// <summary>
    /// Resolves the language code to use for generating output. Explicit
    /// workspace settings win; "auto" or empty falls back to the
    /// client-reported UI language.
    /// </summary>
    public string Resolve(string? settingValue) =>
        string.IsNullOrEmpty(settingValue) || settingValue == AutoLanguage
            ? Language
            : settingValue;
}