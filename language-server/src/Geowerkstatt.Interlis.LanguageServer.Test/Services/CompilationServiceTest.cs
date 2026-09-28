using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

[TestClass]
public class CompilationServiceTest
{
    private const string ModelA = """
        INTERLIS 2.4;
        MODEL A AT "http://example.com" VERSION "1" =
          IMPORTS B;
        END A.
        """;

    [TestMethod]
    public async Task RepositoryLookupsRunConcurrently()
    {
        var repositories = new RendezvousRepositories();
        var a1 = DocumentUri.From("file:///c:/work/a1.ili");
        var a2 = DocumentUri.From("file:///c:/work/a2.ili");
        var workspace = TestWorkspace.Open(repositories, (a1, ModelA.Replace("MODEL A", "MODEL A1").Replace("END A.", "END A1.")), (a2, ModelA.Replace("MODEL A", "MODEL A2").Replace("END A.", "END A2.")));

        // B is in no open document, so both compilations look it up in the repositories.
        var compilations = await Task.WhenAll(workspace.Cache.GetCompilationAsync(a1).AsTask(), workspace.Cache.GetCompilationAsync(a2).AsTask());

        Assert.AreEqual(1, repositories.Crawls);
        var sourceUris = compilations.Select(compilation => compilation.Environment.Content.GetValueOrDefault("B")?.SourceUri).ToList();
        CollectionAssert.AllItemsAreNotNull(sourceUris, "Both compilations resolve B to its stored copy.");
        Assert.AreEqual(sourceUris[0], sourceUris[1]);
    }

    /// <summary>
    /// Repositories that publish model B, in a folder of their own so that no copy stored by an earlier run exists.
    /// A fetch waits for the other one, so the lookups have to run at the same time.
    /// </summary>
    private sealed class RendezvousRepositories : IRepositoryCrawler
    {
        private const string ModelB = """
            INTERLIS 2.4;
            MODEL B AT "http://example.com" VERSION "1" =
            END B.
            """;

        private readonly Uri uri = new($"http://models.example.com/{Guid.NewGuid():N}/");
        private readonly TaskCompletionSource bothFetching = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int fetches;
        private int crawls;

        public int Crawls => crawls;

        public Task<IDictionary<string, Repository>> CrawlModelRepositories(RepositoryCrawlerOptions options)
        {
            Interlocked.Increment(ref crawls);
            var repository = new Repository { HostNameId = uri.Host, Uri = uri, Name = uri.Host };
            repository.Models.Add(new Model { Name = "B", SchemaLanguage = "ili2_4", File = "B.ili", Version = "1", ModelRepository = repository });
            return Task.FromResult<IDictionary<string, Repository>>(new Dictionary<string, Repository> { [repository.HostNameId] = repository });
        }

        public async Task<InterlisFile?> FetchInterlisFile(Model model, Func<string, InterlisFile?> getCachedFile)
        {
            if (Interlocked.Increment(ref fetches) == 2)
            {
                bothFetching.SetResult();
            }

            await bothFetching.Task.WaitAsync(TimeSpan.FromSeconds(10));
            model.FileContent = new InterlisFile { MD5 = "B", Content = ModelB };
            return model.FileContent;
        }
    }
}
