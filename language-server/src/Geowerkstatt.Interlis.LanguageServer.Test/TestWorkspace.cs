using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer;

/// <summary>
/// The server's services wired up as in <c>Program.cs</c>, without a client and without the model repositories, for
/// tests that drive a handler over open documents.
/// </summary>
internal sealed record TestWorkspace(WorkspaceModelIndex Index, ExternalImportFileService ExternalFiles, InterlisEnvironmentCache Cache, SymbolLookup SymbolLookup)
{
    /// <summary>The folder under the temp path holding the stored repository models.</summary>
    public const string TempFolderName = "INTERLIS Language Server Test";

    public TextDocumentSelector Selector { get; } = TextDocumentSelector.ForLanguage("INTERLIS2");

    /// <summary>
    /// Creates the workspace with the given documents open. Every import has to resolve from these documents, because
    /// the repositories answer nothing.
    /// </summary>
    public static TestWorkspace Open(params (DocumentUri Uri, string Source)[] documents) => Open(new NoRepositories(), documents);

    /// <summary>
    /// Creates the workspace with the given documents open and <paramref name="repositories"/> answering the imports
    /// no open document defines.
    /// </summary>
    public static TestWorkspace Open(IRepositoryCrawler repositories, params (DocumentUri Uri, string Source)[] documents)
    {
        var buffers = new OpenDocuments();
        var externalFiles = new ExternalImportFileService(NullLogger<ExternalImportFileService>.Instance, Options.Create(new ServerOptions { LanguageName = "INTERLIS2", TempFolderName = TempFolderName }));
        var index = new WorkspaceModelIndex(buffers, externalFiles, NullLogger<WorkspaceModelIndex>.Instance);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{RepositoryCrawlerOptions.SectionName}:{nameof(RepositoryCrawlerOptions.RootRepositoryUri)}"] = "http://localhost",
            [$"{RepositoryCrawlerOptions.SectionName}:{nameof(RepositoryCrawlerOptions.CacheDbFolder)}"] = Path.Combine(Path.GetTempPath(), TempFolderName),
        }).Build();
        var repositorySearcher = new RepositorySearcher(repositories, configuration, NullLoggerFactory.Instance);
        var compilationService = new CompilationService(index, repositorySearcher, NullLoggerFactory.Instance, externalFiles);
        var cache = new InterlisEnvironmentCache(buffers, compilationService, index);

        // Opened after the index exists, as in the server, so that it indexes them and their imports resolve.
        foreach (var (uri, source) in documents)
        {
            buffers.Update(uri, source);
        }

        return new TestWorkspace(index, externalFiles, cache, new SymbolLookup(cache, new ReferenceCollectorVisitor()));
    }

    private sealed class NoRepositories : IRepositoryCrawler
    {
        public Task<IDictionary<string, Repository>> CrawlModelRepositories(RepositoryCrawlerOptions options)
            => Task.FromResult<IDictionary<string, Repository>>(new Dictionary<string, Repository>());

        public Task<InterlisFile?> FetchInterlisFile(Model model, Func<string, InterlisFile?> getCachedFile)
            => Task.FromResult<InterlisFile?>(null);
    }
}
