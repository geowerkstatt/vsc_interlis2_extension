namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Options for generating mermaid diagrams.
/// </summary>
/// <param name="Uri">The uri to identify the text document.</param>
public record GenerateDiagramOptions(string? Uri);
