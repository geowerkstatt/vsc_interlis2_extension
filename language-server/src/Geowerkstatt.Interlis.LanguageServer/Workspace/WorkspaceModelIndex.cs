using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Workspace;

/// <summary>
/// Knows which models the INTERLIS files the editor can see define, so that an import can be resolved from an open
/// document or a file in the workspace before the model repositories are asked. Open documents are indexed from
/// their buffers and take precedence over the file on disk; the other <c>*.ili</c> files of the workspace folders are
/// indexed from disk when the server starts and whenever the client reports a change to them. The stored copies of
/// repository models (see <see cref="ExternalImportFileService"/>) are never indexed, even while the editor shows
/// one: they are not the user's files, and the repositories supply them anyway.
/// </summary>
public sealed class WorkspaceModelIndex
{
    /// <summary>
    /// Raised after a file was (re-)indexed or removed, with the names of the models it defined before and defines
    /// now, so that compilations depending on it can be invalidated.
    /// </summary>
    public event Action<DocumentUri, IReadOnlyCollection<string>>? FileChanged;

    private readonly FileContentCache fileContentCache;
    private readonly ExternalImportFileService externalImportFileService;
    private readonly ILogger<WorkspaceModelIndex> logger;
    private readonly ConcurrentDictionary<DocumentUri, IndexedFile> files = new();

    public WorkspaceModelIndex(FileContentCache fileContentCache, ExternalImportFileService externalImportFileService, ILogger<WorkspaceModelIndex> logger)
    {
        this.fileContentCache = fileContentCache;
        this.externalImportFileService = externalImportFileService;
        this.logger = logger;

        this.fileContentCache.DocumentInvalidated += Refresh;
    }

    /// <summary>
    /// Indexes the <c>*.ili</c> files of the given workspace folders in the background. Lookups meanwhile see the
    /// files indexed so far; a compilation that resolved an import elsewhere in the meantime is invalidated when
    /// the file defining the model is indexed (<see cref="FileChanged"/>), so it catches up on its own.
    /// </summary>
    /// <param name="folders">The workspace folders.</param>
    public void ScanWorkspace(IEnumerable<DocumentUri> folders)
    {
        var folderList = folders.ToList();
        _ = Task.Run(() =>
        {
            foreach (var folder in folderList)
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles(folder.GetFileSystemPath(), "*.ili", SearchOption.AllDirectories))
                    {
                        var uri = DocumentUri.FromFileSystemPath(path);
                        if (!fileContentCache.Contains(uri))
                        {
                            IndexFromDisk(uri);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not scan workspace folder {Folder} for INTERLIS files.", folder);
                }
            }

            logger.LogInformation("Indexed {Count} INTERLIS files in {Folders} workspace folder(s).", files.Count, folderList.Count);
        });
    }

    /// <summary>
    /// Applies a change on disk the client's file watcher reported (a save, a checkout, a file created or deleted).
    /// </summary>
    /// <param name="uri">The changed file.</param>
    /// <param name="change">The kind of change.</param>
    public void OnWatchedFileChanged(DocumentUri uri, FileChangeType change)
    {
        // An open document is indexed from its buffer (see Refresh), which may be ahead of the disk: re-indexing it
        // from disk here would revert the entry to the saved text and recompile its dependents against it. A deleted
        // open document stays indexed too; Refresh drops it once the editor closes it and the file is still gone.
        if (fileContentCache.Contains(uri) || externalImportFileService.IsExternalFile(uri))
        {
            return;
        }

        if (change == FileChangeType.Deleted)
        {
            Remove(uri);
        }
        else
        {
            IndexFromDisk(uri);
        }
    }

    /// <summary>
    /// Finds the file defining the model <paramref name="modelName"/> for the INTERLIS <paramref name="version"/>.
    /// An open document wins over a file on disk; among several files on disk the first by URI wins and the
    /// ambiguity is logged.
    /// </summary>
    /// <param name="modelName">The name of the model.</param>
    /// <param name="version">The INTERLIS version the model must be written in, or <see langword="null"/> for any.</param>
    /// <returns>The file defining the model, or <see langword="null"/> if no file the editor can see defines it.</returns>
    public IndexedFile? FindModel(string modelName, double? version)
    {
        var candidates = files.Values
            .Where(file => file.Models.Contains(modelName) && (version == null || file.Version == version))
            .OrderByDescending(file => fileContentCache.Contains(file.Uri))
            .ThenBy(file => file.Uri.ToString(), StringComparer.Ordinal)
            .ToList();

        if (candidates.Count > 1)
        {
            logger.LogWarning("Model '{ModelName}' is defined in several files: {Files}. Using {Chosen}.", modelName, string.Join(", ", candidates.Select(c => c.Uri)), candidates[0].Uri);
        }

        return candidates.FirstOrDefault();
    }

    /// <summary>Re-indexes a document whose buffer changed, or falls back to the file on disk when it was closed.</summary>
    private void Refresh(DocumentUri uri)
    {
        if (externalImportFileService.IsExternalFile(uri))
        {
            return;
        }

        if (fileContentCache.TryGetBuffer(uri, out var buffer))
        {
            Index(uri, buffer);
        }
        else if (File.Exists(uri.GetFileSystemPath()))
        {
            IndexFromDisk(uri);
        }
        else
        {
            Remove(uri);
        }
    }

    private void IndexFromDisk(DocumentUri uri)
    {
        try
        {
            Index(uri, File.ReadAllText(uri.GetFileSystemPath()));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read INTERLIS file {Uri}.", uri);
            Remove(uri);
        }
    }

    private void Index(DocumentUri uri, string source)
    {
        files.TryGetValue(uri, out var previous);
        if (previous?.Source == source)
        {
            // Unchanged text (e.g. a save right after the last change): nothing depending on it needs recompiling.
            return;
        }

        var indexed = Parse(uri, source);
        files[uri] = indexed;
        NotifyChanged(uri, previous, indexed);
    }

    private void Remove(DocumentUri uri)
    {
        if (files.TryRemove(uri, out var previous))
        {
            NotifyChanged(uri, previous, null);
        }
    }

    private void NotifyChanged(DocumentUri uri, IndexedFile? previous, IndexedFile? current)
    {
        var affectedModels = new HashSet<string>(previous?.Models ?? [], StringComparer.Ordinal);
        affectedModels.UnionWith(current?.Models ?? []);
        FileChanged?.Invoke(uri, affectedModels);
    }

    /// <summary>
    /// Parses the file without resolving anything, just far enough to know its version, models and dependencies. Parse
    /// errors are irrelevant here (they are reported when the document is compiled), so they are not logged.
    /// </summary>
    private IndexedFile Parse(DocumentUri uri, string source)
    {
        try
        {
            var reader = new InterlisReader(NullLoggerFactory.Instance);
            var environment = reader.ReadRule(new StringReader(source), (parser, visitor) => visitor.VisitInterlis(parser.interlis()), sourceUri: uri.ToString());
            var models = environment.Content.Values.Where(model => model != InternalModel.Interlis).ToList();
            return new IndexedFile(
                uri,
                source,
                environment.Version,
                models.Select(model => model.Name).ToList(),
                models.SelectMany(model => model.Dependencies.Select(dependency => dependency.ModelName)).Where(name => name != InternalModel.Interlis.Name).Distinct().ToList());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not parse {Uri} for indexing; it defines no models until it parses.", uri);
            return new IndexedFile(uri, source, null, [], []);
        }
    }
}
