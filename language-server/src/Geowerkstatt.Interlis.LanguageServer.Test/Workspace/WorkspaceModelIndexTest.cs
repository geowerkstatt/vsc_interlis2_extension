using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer.Workspace;

[TestClass]
public class WorkspaceModelIndexTest
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

    private const string TempFolderName = "INTERLIS Language Server Test";

    private static (OpenDocuments Buffers, WorkspaceModelIndex Index) CreateIndex()
    {
        var buffers = new OpenDocuments();
        var externalFiles = new ExternalImportFileService(NullLogger<ExternalImportFileService>.Instance, Options.Create(new ServerOptions { LanguageName = "INTERLIS2", TempFolderName = TempFolderName }));
        return (buffers, new WorkspaceModelIndex(buffers, externalFiles, NullLogger<WorkspaceModelIndex>.Instance));
    }

    [TestMethod]
    public void IgnoresOpenedRepositoryCopies()
    {
        var (buffers, index) = CreateIndex();
        var repositoryCopy = DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), TempFolderName, "models.interlis.ch", "core", "B.ili"));
        buffers.Update(repositoryCopy, ModelB);

        Assert.IsNull(index.FindModel("B", 2.4));
    }

    [TestMethod]
    public void FindsModelsOfOpenDocuments()
    {
        var (buffers, index) = CreateIndex();
        var uri = DocumentUri.From("file:///c:/work/b.ili");
        buffers.Update(uri, ModelB);

        var found = index.FindModel("B", 2.4);

        Assert.IsNotNull(found);
        Assert.AreEqual(uri, found.Uri);
        Assert.AreEqual(ModelB, found.Source);
        Assert.AreEqual(2.4, found.Version);
        CollectionAssert.AreEqual(new[] { "B" }, found.Models.ToList());
    }

    [TestMethod]
    public void IgnoresModelsOfAnotherVersionAndClosedDocuments()
    {
        var (buffers, index) = CreateIndex();
        var uri = DocumentUri.From("file:///c:/work/b.ili");
        buffers.Update(uri, ModelB);

        Assert.IsNull(index.FindModel("B", 2.3));
        Assert.IsNull(index.FindModel("Unknown", 2.4));

        buffers.Close(uri);
        Assert.IsNull(index.FindModel("B", 2.4));
    }

    [TestMethod]
    public void ReportsAffectedModelsWhenAFileChanges()
    {
        var (buffers, index) = CreateIndex();
        var uri = DocumentUri.From("file:///c:/work/a.ili");
        var changes = new List<(DocumentUri Uri, IReadOnlyCollection<string> Models)>();
        index.FileChanged += (changedUri, models) => changes.Add((changedUri, models));

        buffers.Update(uri, ModelA);
        buffers.Update(uri, ModelA);
        buffers.Update(uri, ModelB);

        // The repeated identical text raises nothing.
        Assert.AreEqual(2, changes.Count);
        CollectionAssert.AreEquivalent(new[] { "A" }, changes[0].Models.ToList());
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, changes[1].Models.ToList());
    }

    [TestMethod]
    public void FindFilesUsingReturnsTheDefiningAndTheDependingFiles()
    {
        var (buffers, index) = CreateIndex();
        var a = DocumentUri.From("file:///c:/work/a.ili");
        var b = DocumentUri.From("file:///c:/work/b.ili");
        buffers.Update(a, ModelA);
        buffers.Update(b, ModelB);

        // A defines A and imports B, B defines B.
        CollectionAssert.AreEqual(new[] { a, b }, index.FindFilesUsing("B").Select(f => f.Uri).ToList());
        CollectionAssert.AreEqual(new[] { a }, index.FindFilesUsing("A").Select(f => f.Uri).ToList());
        Assert.AreEqual(0, index.FindFilesUsing("Unknown").Count);
    }

    [TestMethod]
    public async Task IndexesWatchedFilesFromDisk()
    {
        var (_, index) = CreateIndex();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ili");
        try
        {
            await File.WriteAllTextAsync(path, ModelB);
            index.OnWatchedFileChanged(DocumentUri.FromFileSystemPath(path), FileChangeType.Created);
            Assert.IsNotNull(index.FindModel("B", 2.4));

            index.OnWatchedFileChanged(DocumentUri.FromFileSystemPath(path), FileChangeType.Deleted);
            Assert.IsNull(index.FindModel("B", 2.4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ImportsResolveToFilesOfARunningWorkspaceScan()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var gate = Path.Combine(folder, "gate.ili");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        using var gateReached = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim();
        try
        {
            // The scan indexes a folder's own files before its subfolders, so it reaches the gate before B.
            await File.WriteAllTextAsync(gate, ModelB.Replace("B", "Gate"));
            await File.WriteAllTextAsync(Path.Combine(folder, "sub", "b.ili"), ModelB);
            var a = DocumentUri.From("file:///c:/work/a.ili");
            var workspace = TestWorkspace.Open((a, ModelA));

            // FileChanged runs on the scan's thread, so blocking it at the gate keeps the scan running.
            workspace.Index.FileChanged += (uri, _) =>
            {
                if (uri == DocumentUri.FromFileSystemPath(gate))
                {
                    gateReached.Release();
                    release.Wait();
                }
            };

            // A document compiled while the scan runs, as one the editor restores on startup: its import must wait
            // for the scan instead of being reported unresolved. Without waiting, it would complete well within
            // the grace period, before the scan is released.
            workspace.Index.ScanWorkspace([DocumentUri.FromFileSystemPath(folder)]);
            Assert.IsTrue(await gateReached.WaitAsync(TimeSpan.FromSeconds(10)));
            var compiling = workspace.Cache.GetCompilationAsync(a).AsTask();
            await Task.WhenAny(compiling, Task.Delay(TimeSpan.FromMilliseconds(500)));
            release.Set();
            var compilation = await compiling;

            Assert.IsTrue(compilation.Environment.Content.ContainsKey("B"));
            Assert.AreEqual(0, compilation.Diagnostics.Count, string.Join(Environment.NewLine, compilation.Diagnostics.Select(d => d.Message)));
        }
        finally
        {
            release.Set();
            Directory.Delete(folder, true);
        }
    }
}
