using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

/// <summary>
/// Stores the compilation (the INTERLIS environment and the compiler's diagnostics) of each opened document in memory.
/// </summary>
public sealed class InterlisEnvironmentCache : ICache<InterlisEnvironment>
{
    /// <inheritdoc />
    public event Action<DocumentUri>? DocumentInvalidated;

    private readonly FileContentCache fileContentCache;
    private readonly CompilationService compilationService;
    private readonly ConcurrentDictionary<string, (string Source, Compilation Compilation)> compilationCache = new();

    public InterlisEnvironmentCache(FileContentCache fileContentCache, CompilationService compilationService, WorkspaceModelIndex workspaceModelIndex)
    {
        this.fileContentCache = fileContentCache;
        this.compilationService = compilationService;

        this.fileContentCache.DocumentInvalidated += InvalidateCache;
        workspaceModelIndex.FileChanged += InvalidateDependents;
    }

    private void InvalidateCache(DocumentUri uri)
    {
        compilationCache.Remove(uri.ToString(), out _);
        DocumentInvalidated?.Invoke(uri);
    }

    /// <summary>
    /// Invalidates the compilations of the other documents that depend on a changed file: those that compiled a model
    /// from it (the environment is flat, so this covers transitive dependencies), and those whose environment depends
    /// on a model it defines (an import or a <c>TRANSLATION OF</c> base), which catches a dependency that was still
    /// unresolved when the compilation was made (the file did not exist, had another version or another model name)
    /// at any level of the chain.
    /// </summary>
    private void InvalidateDependents(DocumentUri changedFile, IReadOnlyCollection<string> affectedModels)
    {
        var changedUri = changedFile.ToString();
        foreach (var (uri, entry) in compilationCache)
        {
            if (uri == changedUri)
            {
                continue;
            }

            var models = entry.Compilation.Environment.Content.Values;
            if (models.Any(model => model.SourceUri == changedUri)
                || models.SelectMany(model => model.Dependencies).Any(dependency => affectedModels.Contains(dependency.ModelName)))
            {
                InvalidateCache(DocumentUri.From(uri));
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<InterlisEnvironment> GetAsync(DocumentUri uri) => (await GetCompilationAsync(uri)).Environment;

    /// <summary>
    /// Gets or computes the compilation of the given document.
    /// </summary>
    /// <param name="uri">The document URI.</param>
    /// <param name="cancellationToken">Cancels a compilation that has to be computed.</param>
    /// <returns>The document's environment and the compiler's diagnostics; empty for an unknown or empty document.</returns>
    public async ValueTask<Compilation> GetCompilationAsync(DocumentUri uri, CancellationToken cancellationToken = default)
    {
        var source = await fileContentCache.GetAsync(uri);
        if (compilationCache.TryGetValue(uri.ToString(), out var cached) && cached.Source == source)
        {
            return cached.Compilation;
        }

        if (string.IsNullOrEmpty(source))
        {
            return new Compilation(new InterlisEnvironment(), []);
        }

        var compilation = await compilationService.CompileAsync(source, uri, cancellationToken);

        // The document may have changed while compiling (the change invalidated the cache, but this compilation
        // would re-populate it with a stale result): only keep it if the buffer still holds the compiled source.
        if (await fileContentCache.GetAsync(uri) == source)
        {
            compilationCache[uri.ToString()] = (source, compilation);
        }

        return compilation;
    }
}
