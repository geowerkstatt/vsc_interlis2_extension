using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

/// <summary>
/// Stores the compilation (the INTERLIS environment and the compiler's diagnostics) of a document in memory, keyed
/// by its URI and tagged with the source it was compiled from, so a cached entry is used only while that source is
/// still current. Open documents are compiled from their buffer; a file the editor merely sees in the workspace is
/// compiled from the indexed content, so an entry is kept for every file that was compiled, not only for the open
/// ones. Entries live until the file changes; nothing else evicts them.
/// </summary>
public sealed class InterlisEnvironmentCache : ICache<InterlisEnvironment>
{
    /// <summary>Raised when a document's compilation was dropped, so that what is derived from it can be recomputed.</summary>
    public event Action<DocumentUri>? DocumentInvalidated;

    private readonly OpenDocuments openDocuments;
    private readonly CompilationService compilationService;
    private readonly WorkspaceModelIndex workspaceModelIndex;
    private readonly ConcurrentDictionary<string, (string Source, Compilation Compilation)> compilationCache = new();

    public InterlisEnvironmentCache(OpenDocuments openDocuments, CompilationService compilationService, WorkspaceModelIndex workspaceModelIndex)
    {
        this.openDocuments = openDocuments;
        this.compilationService = compilationService;
        this.workspaceModelIndex = workspaceModelIndex;

        // Both run on a buffer change, and the index must re-parse the document before the dependents are
        // determined from it. That order holds because dependency injection constructs the index first, as a
        // constructor argument of this cache, so it subscribes first.
        this.openDocuments.DocumentChanged += InvalidateCache;
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
        var changedFileIsOpen = openDocuments.Contains(changedFile);
        foreach (var (uri, entry) in compilationCache)
        {
            // An open document's own compilation is invalidated by its buffer change; a file that is not open has
            // no other trigger, so its entry is dropped here.
            if (uri == changedUri && changedFileIsOpen)
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
        var source = GetSource(uri);
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
        // would re-populate it with a stale result): only keep it if the source is still the one that was compiled.
        if (GetSource(uri) == source)
        {
            compilationCache[uri.ToString()] = (source, compilation);
        }

        return compilation;
    }

    /// <summary>
    /// The text to compile for a document: its editor buffer while it is open, otherwise the content of the
    /// workspace file as the index read it, so that a file can be compiled without opening it (e.g. to search it
    /// for references). Empty when the editor does not see the file at all.
    /// </summary>
    private string GetSource(DocumentUri uri)
    {
        var buffer = openDocuments.GetText(uri);
        return string.IsNullOrEmpty(buffer) ? workspaceModelIndex.Find(uri)?.Source ?? string.Empty : buffer;
    }
}
