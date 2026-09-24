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
/// ones. Entries live until the file changes; nothing else evicts them. A compilation is cached from its start, so
/// that the requests arriving together (the diagnostics, the outline and the version status when a document is
/// opened) share one compilation, and with it one result, instead of each compiling the document on its own.
/// </summary>
public sealed class InterlisEnvironmentCache
{
    /// <summary>Raised when a document's compilation was dropped, so that what is derived from it can be recomputed.</summary>
    public event Action<DocumentUri>? DocumentInvalidated;

    private readonly OpenDocuments openDocuments;
    private readonly CompilationService compilationService;
    private readonly WorkspaceModelIndex workspaceModelIndex;
    private readonly ConcurrentDictionary<string, CachedCompilation> compilationCache = new();

    /// <summary>
    /// A compilation of <see cref="Source"/>, possibly still running; canceled through <see cref="Cancellation"/>
    /// when the entry is invalidated.
    /// </summary>
    private sealed class CachedCompilation(string source)
    {
        public string Source { get; } = source;

        public CancellationTokenSource Cancellation { get; } = new();

        /// <summary>Completes with the compilation; canceled when the entry was invalidated first.</summary>
        public TaskCompletionSource<Compilation> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The compilation once it completed successfully, else <see langword="null"/>.</summary>
        public volatile Compilation? Completed;
    }

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
        if (compilationCache.TryRemove(uri.ToString(), out var removed))
        {
            removed.Cancellation.Cancel();
        }

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

            // A running compilation's dependencies are not known yet; it is not invalidated here.
            if (entry.Completed is not { } compilation)
            {
                continue;
            }

            var models = compilation.Environment.Content.Values;
            if (models.Any(model => model.SourceUri == changedUri)
                || models.SelectMany(model => model.Dependencies).Any(dependency => affectedModels.Contains(dependency.ModelName)))
            {
                InvalidateCache(DocumentUri.From(uri));
            }
        }
    }

    /// <summary>
    /// Gets or computes the compilation of the given document. Callers asking while it is being computed wait for the
    /// same compilation. A compilation that is invalidated while a caller waits for it (the document changed) is
    /// replaced by one of the current text, which the caller then waits for. A compilation that fails, including one
    /// canceled from within without being invalidated, is not kept, so that the next request compiles again.
    /// </summary>
    /// <param name="uri">The document URI.</param>
    /// <param name="cancellationToken">Cancels waiting for the compilation; the compilation itself runs on for the other callers.</param>
    /// <returns>The document's environment and the compiler's diagnostics; empty for an unknown or empty document.</returns>
    public async ValueTask<Compilation> GetCompilationAsync(DocumentUri uri, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var source = GetSource(uri);
            if (string.IsNullOrEmpty(source))
            {
                return new Compilation(new InterlisEnvironment(), []);
            }

            var key = uri.ToString();
            var entry = GetOrStartCompilation(uri, key, source);
            try
            {
                return await entry.Result.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && entry.Cancellation.IsCancellationRequested)
            {
                // The compilation was invalidated while waiting for it: compile the current text.
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A failed compilation is not kept. This includes a cancellation from within the compilation (e.g. an
                // HTTP timeout): retrying the entry, which stays canceled, would loop forever.
                compilationCache.TryRemove(KeyValuePair.Create(key, entry));
                throw;
            }
        }
    }

    /// <summary>
    /// The cached compilation of <paramref name="source"/>, or a new one, started here, that replaces (and cancels)
    /// the compilation of an outdated source.
    /// </summary>
    private CachedCompilation GetOrStartCompilation(DocumentUri uri, string key, string source)
    {
        while (true)
        {
            if (!compilationCache.TryGetValue(key, out var existing))
            {
                var added = new CachedCompilation(source);
                if (compilationCache.TryAdd(key, added))
                {
                    _ = CompileAsync(uri, added);
                    return added;
                }
            }
            else if (existing.Source == source)
            {
                return existing;
            }
            else
            {
                var replacement = new CachedCompilation(source);
                if (compilationCache.TryUpdate(key, replacement, existing))
                {
                    existing.Cancellation.Cancel();
                    _ = CompileAsync(uri, replacement);
                    return replacement;
                }
            }
        }
    }

    private async Task CompileAsync(DocumentUri uri, CachedCompilation entry)
    {
        try
        {
            var compilation = await compilationService.CompileAsync(entry.Source, uri, entry.Cancellation.Token);
            entry.Completed = compilation;
            entry.Result.SetResult(compilation);
        }
        catch (OperationCanceledException)
        {
            entry.Result.SetCanceled();
        }
        catch (Exception ex)
        {
            entry.Result.SetException(ex);
        }
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
