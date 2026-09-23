namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Options for determining the INTERLIS version of a document.
/// </summary>
/// <param name="Uri">The uri to identify the text document.</param>
public record InterlisVersionOptions(string? Uri);
