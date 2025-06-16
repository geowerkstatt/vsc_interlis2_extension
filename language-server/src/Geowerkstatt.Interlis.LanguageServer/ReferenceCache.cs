using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer;

public sealed class ReferenceCache : ICache<List<(RangePosition position, IInterlisDefinition target)>>
{
    private readonly ILogger<ReferenceCache> logger;
    private readonly InterlisEnvironmentCache environmentCache;
    private readonly InterlisReader interlisReader;
    private readonly ConcurrentDictionary<string, List<(RangePosition position, IInterlisDefinition target)>> referenceCache = new ConcurrentDictionary<string, List<(RangePosition position, IInterlisDefinition target)>>();

    public event Action<DocumentUri>? DocumentInvalidated;

    public ReferenceCache(ILogger<ReferenceCache> logger, InterlisEnvironmentCache environmentCache, InterlisReader interlisReader)
    {
        this.logger = logger;
        this.environmentCache = environmentCache;
        this.interlisReader = interlisReader;

        this.environmentCache.DocumentInvalidated += InvalidateCache;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        referenceCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    public List<(RangePosition position, IInterlisDefinition target)> Get(DocumentUri uri)
    {
        List<(RangePosition position, IInterlisDefinition target)>? definitions;
        if (referenceCache.TryGetValue(uri.ToString(), out definitions))
        {
            return definitions;
        }

        var environment = environmentCache.Get(uri);
        var referenceCollector = new ReferenceCollectorVisitor();
        definitions = referenceCollector.VisitInterlisEnvironment(environment) ?? new List<(RangePosition position, IInterlisDefinition target)>();

        referenceCache[uri.ToString()] = definitions;
        return definitions;
    }
}
