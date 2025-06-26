using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

/// <summary>
/// Stores the reference definitions for each INTERLIS environment created by a document in memory.
/// </summary>
public sealed class ReferenceCache : ICache<List<ReferenceDefinition>>
{
    /// <inheritdoc />
    public event Action<DocumentUri>? DocumentInvalidated;

    private readonly InterlisEnvironmentCache environmentCache;
    private readonly ReferenceCollectorVisitor referenceCollector;
    private readonly ConcurrentDictionary<string, List<ReferenceDefinition>> referenceCache = new();

    public ReferenceCache(InterlisEnvironmentCache environmentCache, ReferenceCollectorVisitor referenceCollector)
    {
        this.environmentCache = environmentCache;
        this.referenceCollector = referenceCollector;

        this.environmentCache.DocumentInvalidated += InvalidateCache;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        referenceCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    /// <inheritdoc />
    public async ValueTask<List<ReferenceDefinition>> GetAsync(DocumentUri uri)
    {
        if (referenceCache.TryGetValue(uri.ToUnencodedString(), out var cachedDefinitions))
        {
            return cachedDefinitions;
        }

        var environment = await environmentCache.GetAsync(uri);
        AddReferencesFromEnvironment(environment);

        return referenceCache[uri.ToUnencodedString()];
    }

    private void AddReferencesFromEnvironment(InterlisEnvironment environment)
    {
        var references = referenceCollector.VisitInterlisEnvironment(environment) ?? new List<ReferenceDefinition>();
        var groupedReferences = references.GroupBy(d => d.OccurenceFile);
        foreach (var fileReferences in groupedReferences)
        {
            referenceCache[fileReferences.Key.ToString()] = fileReferences.ToList();
        }
    }
}
