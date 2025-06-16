using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer;

public sealed class DefinitionCache: ICache<List<IInterlisDefinition>>
{
    private readonly ILogger<DefinitionCache> logger;
    private readonly InterlisEnvironmentCache environmentCache;
    private readonly InterlisReader interlisReader;
    private readonly ConcurrentDictionary<string, List<IInterlisDefinition>> definitionsCache = new ConcurrentDictionary<string, List<IInterlisDefinition>>();

    public event Action<DocumentUri>? DocumentInvalidated;

    public DefinitionCache(ILogger<DefinitionCache> logger, InterlisEnvironmentCache environmentCache, InterlisReader interlisReader)
    {
        this.logger = logger;
        this.environmentCache = environmentCache;
        this.interlisReader = interlisReader;

        this.environmentCache.DocumentInvalidated += InvalidateCache;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        definitionsCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    public List<IInterlisDefinition> Get(DocumentUri uri)
    {
        List<IInterlisDefinition>? definitions;
        if (definitionsCache.TryGetValue(uri.ToString(), out definitions))
        {
            return definitions;
        }

        var environment = environmentCache.Get(uri);

        definitions = this.CollectDefinitions(environment);
        definitionsCache[uri.ToString()] = definitions;
        return definitions;
    }

    private List<IInterlisDefinition> CollectDefinitions(InterlisEnvironment source)
    {
        throw new NotImplementedException();
    }
}
