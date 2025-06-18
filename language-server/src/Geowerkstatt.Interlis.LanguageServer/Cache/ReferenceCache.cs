using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

public sealed class ReferenceCache : ICache<List<ReferenceDefinition>>
{
    private readonly ILogger<ReferenceCache> logger;
    private readonly InterlisEnvironmentCache environmentCache;
    private readonly ReferenceCollectorVisitor referenceCollector;
    private readonly ConcurrentDictionary<string, List<ReferenceDefinition>> referenceCache = new ConcurrentDictionary<string, List<ReferenceDefinition>>();

    public event Action<DocumentUri>? DocumentInvalidated;

    public ReferenceCache(ILogger<ReferenceCache> logger, InterlisEnvironmentCache environmentCache, ReferenceCollectorVisitor referenceCollector)
    {
        this.logger = logger;
        this.environmentCache = environmentCache;
        this.referenceCollector = referenceCollector;

        this.environmentCache.DocumentInvalidated += InvalidateCache;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        referenceCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    public List<ReferenceDefinition> Get(DocumentUri uri)
    {
        List<ReferenceDefinition>? definitions;
        if (referenceCache.TryGetValue(uri.ToString(), out definitions))
        {
            return definitions;
        }

        var environment = environmentCache.Get(uri);
        AddReferencesFromEnvironment(environment);

        return referenceCache[uri.ToString()];
    }

    private void AddReferencesFromEnvironment(InterlisEnvironment environment)
    {
        var definitions = referenceCollector.VisitInterlisEnvironment(environment) ?? new List<ReferenceDefinition>();
        var groupedDefinitions = definitions.GroupBy(d => d.OccurenceFile);
        foreach (var fileDefinitions in groupedDefinitions)
        {
            referenceCache[fileDefinitions.Key.ToString()] = fileDefinitions.ToList(); ;
        }
    }
}
