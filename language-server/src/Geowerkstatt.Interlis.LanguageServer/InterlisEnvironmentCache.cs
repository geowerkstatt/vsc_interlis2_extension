using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer;

public sealed class InterlisEnvironmentCache: ICache<InterlisEnvironment>
{
    private readonly ILogger<InterlisEnvironmentCache> logger;
    private readonly FileContentCache fileContentCache;
    private readonly InterlisReader interlisReader;
    private readonly ConcurrentDictionary<string, InterlisEnvironment> environmentCache = new ConcurrentDictionary<string, InterlisEnvironment>();

    public event Action<DocumentUri>? DocumentInvalidated;

    public InterlisEnvironmentCache(ILogger<InterlisEnvironmentCache> logger, FileContentCache fileContentCache, InterlisReader interlisReader)
    {
        this.logger = logger;
        this.fileContentCache = fileContentCache;
        this.interlisReader = interlisReader;

        this.fileContentCache.DocumentInvalidated += InvalidateCache;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        environmentCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    public InterlisEnvironment Get(DocumentUri uri)
    {
        if (environmentCache.TryGetValue(uri.ToString(), out var ast))
        {
            return ast;
        }

        var source = fileContentCache.Get(uri);
        if (string.IsNullOrEmpty(source))
        {
            return new InterlisEnvironment();
        }

        var environment = interlisReader.ReadFile(new StringReader(source), uri.ToString());
        environmentCache[uri.ToString()] = environment;
        return environment;
    }
}
