using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

/// <summary>
/// Stores the INTERLIS environment for each opened document in memory.
/// </summary>
public sealed class InterlisEnvironmentCache : ICache<InterlisEnvironment>
{
    /// <inheritdoc />
    public event Action<DocumentUri>? DocumentInvalidated;

    private readonly FileContentCache fileContentCache;
    private readonly InterlisReader interlisReader;
    private readonly ConcurrentDictionary<string, InterlisEnvironment> environmentCache = new();

    public InterlisEnvironmentCache(FileContentCache fileContentCache, InterlisReader interlisReader)
    {
        this.fileContentCache = fileContentCache;
        this.interlisReader = interlisReader;

        this.fileContentCache.DocumentInvalidated += InvalidateCache;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        environmentCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    /// <inheritdoc />
    public async ValueTask<InterlisEnvironment> GetAsync(DocumentUri uri)
    {
        if (environmentCache.TryGetValue(uri.ToString(), out var ast))
        {
            return ast;
        }

        var source = await fileContentCache.GetAsync(uri);
        if (string.IsNullOrEmpty(source))
        {
            return new InterlisEnvironment();
        }

        var environment = interlisReader.ReadFile(new StringReader(source), uri.ToString());
        environmentCache[uri.ToString()] = environment;
        return environment;
    }
}
