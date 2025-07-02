using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

/// <summary>
/// Store values associated with document URIs.
/// </summary>
internal interface ICache<T> where T : class
{
    /// <summary>
    /// Event that is raised to invalidate values from this cache when a document has changed.
    /// </summary>
    public event Action<DocumentUri>? DocumentInvalidated;

    /// <summary>
    /// Gets or computes the value for the given document URI.
    /// </summary>
    /// <param name="uri">The document URI.</param>
    /// <returns>The computed or cached value for the given document URI.</returns>
    public ValueTask<T> GetAsync(DocumentUri uri);
}
