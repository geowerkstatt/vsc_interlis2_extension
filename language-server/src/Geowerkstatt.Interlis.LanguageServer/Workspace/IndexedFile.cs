using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Workspace;

/// <summary>
/// An INTERLIS file the editor can see (an open document or a file in a workspace folder) as the
/// <see cref="WorkspaceModelIndex"/> knows it.
/// </summary>
/// <param name="Uri">The file's URI.</param>
/// <param name="Source">The file's text: the editor buffer for an open document, otherwise the content on disk.</param>
/// <param name="Version">The INTERLIS version the file declares, or <see langword="null"/> if it has no valid header.</param>
/// <param name="Models">The names of the models the file defines.</param>
/// <param name="Dependencies">The names of the models those models depend on: their imports and, for a translation, the base-language model.</param>
public sealed record IndexedFile(DocumentUri Uri, string Source, double? Version, IReadOnlyList<string> Models, IReadOnlyList<string> Dependencies);
