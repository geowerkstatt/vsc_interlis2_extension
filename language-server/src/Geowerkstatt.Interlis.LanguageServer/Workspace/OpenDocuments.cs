using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Geowerkstatt.Interlis.LanguageServer.Workspace;

/// <summary>
/// The documents the client has open, with the text of their editor buffers. This is the authoritative record of
/// what the user sees rather than a cache of something cheaper to recompute: while a document is open its buffer
/// wins over the file on disk everywhere in the server, so everything else is derived from this.
/// </summary>
public sealed class OpenDocuments
{
    /// <summary>
    /// Raised when a document was opened, changed, saved or closed, so that what is derived from its text can be
    /// recomputed.
    /// </summary>
    public event Action<DocumentUri>? DocumentChanged;

    private readonly ConcurrentDictionary<string, string> buffers = new();

    /// <summary>
    /// Stores the text of an open document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the document.</param>
    /// <param name="text">The text of its editor buffer.</param>
    public void Update(DocumentUri uri, string text)
    {
        buffers[uri.ToString()] = text;
        DocumentChanged?.Invoke(uri);
    }

    /// <summary>
    /// Forgets a document the client closed.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the document.</param>
    public void Close(DocumentUri uri)
    {
        buffers.TryRemove(uri.ToString(), out _);
        DocumentChanged?.Invoke(uri);
    }

    /// <summary>
    /// Whether the document is open.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the document.</param>
    public bool Contains(DocumentUri uri) => buffers.ContainsKey(uri.ToString());

    /// <summary>
    /// Gets the text of an open document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the document.</param>
    /// <param name="text">The text of its editor buffer, if the document is open.</param>
    /// <returns>Whether the document is open.</returns>
    public bool TryGetText(DocumentUri uri, [MaybeNullWhen(false)] out string text) => buffers.TryGetValue(uri.ToString(), out text);

    /// <summary>
    /// The text of an open document, or an empty string if it is not open.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the document.</param>
    public string GetText(DocumentUri uri) => TryGetText(uri, out var text) ? text : string.Empty;
}
