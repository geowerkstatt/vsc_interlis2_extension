using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// Stores the content of the currently opened INTERLIS files in memory.
/// </summary>
public sealed class FileContentCache
{
    private readonly ConcurrentDictionary<string, string> buffers = new ConcurrentDictionary<string, string>();

    /// <summary>
    /// Update the buffer for the given document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the file.</param>
    /// <param name="buffer">The file content.</param>
    public void UpdateBuffer(DocumentUri uri, string buffer)
    {
        buffers.AddOrUpdate(uri.ToString(), buffer, (k, v) => buffer);
    }

    /// <summary>
    /// Get the buffer for the given document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the file.</param>
    /// <returns>The file content if the file exists, or an empty string otherwise.</returns>
    public string GetBuffer(DocumentUri uri)
    {
        return buffers.TryGetValue(uri.ToString(), out var buffer) ? buffer : string.Empty;
    }

    /// <summary>
    /// Remove the buffer for the given document.
    /// </summary>
    /// <param name="uri">A <see cref="DocumentUri"/> to identify the file.</param>
    public void ClearBuffer(DocumentUri uri)
    {
        buffers.TryRemove(uri.ToString(), out _);
    }
}
