namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Options for generating mermaid diagrams.
/// </summary>
/// <param name="Uri">The uri to identify the text document.</param>
/// <param name="Orientation">Mermaid direction token (<c>LR</c> or <c>TB</c>).</param>
/// <param name="Language">Optional language override (e.g. <c>de</c>, <c>en</c>, <c>auto</c>).
/// When set, takes precedence over the <c>interlis.documentation.language</c> workspace setting.</param>
public record GenerateDiagramOptions(string? Uri, string Orientation, string? Language = null);