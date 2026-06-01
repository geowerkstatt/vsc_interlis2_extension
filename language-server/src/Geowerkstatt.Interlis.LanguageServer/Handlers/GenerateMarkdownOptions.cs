namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Options for generating markdown documentation.
/// </summary>
/// <param name="Uri">The uri to identify the text document.</param>
/// <param name="Language">Optional language override (e.g. <c>de</c>, <c>en</c>, <c>auto</c>).
/// When set, takes precedence over the <c>interlis.documentation.language</c> workspace setting.</param>
public record GenerateMarkdownOptions(string? Uri, string? Language = null);