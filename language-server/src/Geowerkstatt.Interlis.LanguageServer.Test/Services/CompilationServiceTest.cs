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
    public async Task RepositoryLookupsDoNotRunConcurrently()
    {
        var repositories = new SlowRepositories();
        var a1 = DocumentUri.From("file:///c:/work/a1.ili");
        var a2 = DocumentUri.From("file:///c:/work/a2.ili");
        var workspace = TestWorkspace.Open(repositories, (a1, ModelA.Replace("MODEL A", "MODEL A1").Replace("END A.", "END A1.")), (a2, ModelA.Replace("MODEL A", "MODEL A2").Replace("END A.", "END A2.")));

        // B is in no open document, so both compilations look it up in the repositories.
        await Task.WhenAll(workspace.Cache.GetCompilationAsync(a1).AsTask(), workspace.Cache.GetCompilationAsync(a2).AsTask());

        Assert.AreEqual(2, repositories.Crawls);
        Assert.AreEqual(1, repositories.MaxConcurrentCrawls);
    }

    /// <summary>Repositories that know no models and take a while to crawl, counting crawls running at once.</summary>
    private sealed class SlowRepositories : IRepositoryCrawler
    {
        private readonly object counts = new();
        private int running;

        public int Crawls { get; private set; }

        public int MaxConcurrentCrawls { get; private set; }

        public async Task<IDictionary<string, Repository>> CrawlModelRepositories(RepositoryCrawlerOptions options)
        {
            var now = Interlocked.Increment(ref running);
            lock (counts)
            {
                Crawls++;
                MaxConcurrentCrawls = Math.Max(MaxConcurrentCrawls, now);
            }

            await Task.Delay(200);
            Interlocked.Decrement(ref running);
            return new Dictionary<string, Repository>();
        }

        public Task<InterlisFile?> FetchInterlisFile(Model model, Func<string, InterlisFile?> getCachedFile)
            => Task.FromResult<InterlisFile?>(null);
    }
}
