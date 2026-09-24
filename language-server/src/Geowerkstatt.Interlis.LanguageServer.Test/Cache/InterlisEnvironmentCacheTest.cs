using Geowerkstatt.Interlis.RepositoryCrawler;
using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Cache;

[TestClass]
public class InterlisEnvironmentCacheTest
{
    private const string ModelA = """
        INTERLIS 2.4;
        MODEL A AT "http://example.com" VERSION "1" =
          IMPORTS B;
        END A.
        """;

    private const string ModelB = """
        INTERLIS 2.4;
        MODEL B AT "http://example.com" VERSION "1" =
        END B.
        """;

    private static readonly DocumentUri UriA = DocumentUri.From("file:///c:/work/a.ili");

    [TestMethod]
    public async Task ConcurrentRequestsShareOneCompilation()
    {
        using var scan = await HeldScan.StartAsync((UriA, ModelA));

        // Both wait for B, which the held scan has not indexed yet.
        var first = scan.Workspace.Cache.GetCompilationAsync(UriA).AsTask();
        var second = scan.Workspace.Cache.GetCompilationAsync(UriA).AsTask();
        scan.Release();

        Assert.AreSame(await first, await second);
    }

    [TestMethod]
    public async Task RequestWaitingForAnInvalidatedCompilationGetsTheCurrentText()
    {
        using var scan = await HeldScan.StartAsync((UriA, ModelA));
        var waiting = scan.Workspace.Cache.GetCompilationAsync(UriA).AsTask();

        // The edit drops the import, so the compilation of the new text does not wait for the scan.
        scan.Workspace.Buffers.Update(UriA, ModelA.Replace("IMPORTS B;", string.Empty, StringComparison.Ordinal));
        var compilation = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        scan.Release();

        // Let the scan finish before its folder is deleted: on Windows a file being read cannot be deleted.
        await scan.Workspace.Index.FindModelAsync("B", 2.4, CancellationToken.None);

        Assert.IsTrue(compilation.Environment.Content.ContainsKey("A"));
        Assert.IsFalse(compilation.Environment.Content.ContainsKey("B"));
    }

    [TestMethod]
    public async Task CanceledRequestDoesNotCancelTheSharedCompilation()
    {
        using var scan = await HeldScan.StartAsync((UriA, ModelA));
        using var cancellation = new CancellationTokenSource();
        var canceled = scan.Workspace.Cache.GetCompilationAsync(UriA, cancellation.Token).AsTask();
        var other = scan.Workspace.Cache.GetCompilationAsync(UriA).AsTask();

        await cancellation.CancelAsync();
        try
        {
            await canceled;
            Assert.Fail("The canceled request completed.");
        }
        catch (OperationCanceledException)
        {
        }

        scan.Release();
        Assert.IsTrue((await other).Environment.Content.ContainsKey("B"));
    }

    [TestMethod]
    public async Task CompilationCanceledFromWithinFailsInsteadOfRetrying()
    {
        var repositories = new TimingOutRepositories();
        var workspace = TestWorkspace.Open(repositories, (UriA, ModelA));

        // Neither request may hang, and the second compiles again instead of getting the canceled compilation.
        for (var request = 1; request <= 2; request++)
        {
            var compiling = Task.Run(async () => await workspace.Cache.GetCompilationAsync(UriA));
            try
            {
                await compiling.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Fail("The compilation succeeded although the repositories timed out.");
            }
            catch (OperationCanceledException)
            {
            }

            Assert.AreEqual(request, repositories.Crawls);
        }
    }

    /// <summary>
    /// A workspace whose scan is held until <see cref="Release"/>, so that compilations importing B, which only the
    /// scan finds, keep running until then.
    /// </summary>
    private sealed class HeldScan : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private readonly ManualResetEventSlim release = new();
        private readonly SemaphoreSlim gateReached = new(0);

        private HeldScan(TestWorkspace workspace)
        {
            Workspace = workspace;
        }

        public TestWorkspace Workspace { get; }

        public static async Task<HeldScan> StartAsync(params (DocumentUri Uri, string Source)[] documents)
        {
            var scan = new HeldScan(TestWorkspace.Open(documents));
            var gate = Path.Combine(scan.folder, "gate.ili");
            Directory.CreateDirectory(Path.Combine(scan.folder, "sub"));

            // The scan indexes a folder's own files before its subfolders, so it reaches the gate before B.
            await File.WriteAllTextAsync(gate, ModelB.Replace("B", "Gate"));
            await File.WriteAllTextAsync(Path.Combine(scan.folder, "sub", "b.ili"), ModelB);

            // FileChanged runs on the scan's thread, so blocking it at the gate keeps the scan running.
            scan.Workspace.Index.FileChanged += (uri, _) =>
            {
                if (uri == DocumentUri.FromFileSystemPath(gate))
                {
                    scan.gateReached.Release();
                    scan.release.Wait();
                }
            };

            scan.Workspace.Index.ScanWorkspace([DocumentUri.FromFileSystemPath(scan.folder)]);
            Assert.IsTrue(await scan.gateReached.WaitAsync(TimeSpan.FromSeconds(10)));
            return scan;
        }

        public void Release() => release.Set();

        public void Dispose()
        {
            // Not disposed: the scan's thread may still be returning from the wait.
            release.Set();
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Repositories whose crawl times out the way an <see cref="HttpClient"/> reports it.</summary>
    private sealed class TimingOutRepositories : IRepositoryCrawler
    {
        private int crawls;

        public int Crawls => crawls;

        public Task<IDictionary<string, Repository>> CrawlModelRepositories(RepositoryCrawlerOptions options)
        {
            Interlocked.Increment(ref crawls);
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        }

        public Task<InterlisFile?> FetchInterlisFile(Model model, Func<string, InterlisFile?> getCachedFile)
            => Task.FromResult<InterlisFile?>(null);
    }
}
