using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

public sealed class InterlisEnvironmentCache: ICache<InterlisEnvironment>
{
    private readonly ILogger<InterlisEnvironmentCache> logger;
    private readonly FileContentCache fileContentCache;
    private readonly InterlisResolveService interlisResolveService;
    private readonly ConcurrentDictionary<string, InterlisEnvironment> environmentCache = new ConcurrentDictionary<string, InterlisEnvironment>();

    public event Action<DocumentUri>? DocumentInvalidated;

    public InterlisEnvironmentCache(ILogger<InterlisEnvironmentCache> logger, FileContentCache fileContentCache, InterlisResolveService interlisResolveService)
    {
        this.logger = logger;
        this.fileContentCache = fileContentCache;
        this.interlisResolveService = interlisResolveService;

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

        var environment = ReadEnvironment(source, uri.ToString());
        environmentCache[uri.ToString()] = environment;
        return environment;
    }

    private InterlisEnvironment ReadEnvironment(string source, string sourceUri)
    {
        return interlisResolveService.ResolveAsync(new StringReader(source), sourceUri).Result;
    }
}
