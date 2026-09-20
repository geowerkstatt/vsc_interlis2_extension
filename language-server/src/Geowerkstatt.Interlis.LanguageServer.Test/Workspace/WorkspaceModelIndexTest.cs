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

    private static (FileContentCache Buffers, WorkspaceModelIndex Index) CreateIndex()
    {
        var buffers = new FileContentCache();
        var externalFiles = new ExternalImportFileService(NullLogger<ExternalImportFileService>.Instance, Options.Create(new ServerOptions { LanguageName = "INTERLIS2", TempFolderName = TempFolderName }));
        return (buffers, new WorkspaceModelIndex(buffers, externalFiles, NullLogger<WorkspaceModelIndex>.Instance));
    }

    [TestMethod]
    public void IgnoresOpenedRepositoryCopies()
    {
        var (buffers, index) = CreateIndex();
        var repositoryCopy = DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), TempFolderName, "models.interlis.ch", "core", "B.ili"));
        buffers.UpdateBuffer(repositoryCopy, ModelB);

        Assert.IsNull(index.FindModel("B", 2.4));
    }

    [TestMethod]
    public void FindsModelsOfOpenDocuments()
    {
        var (buffers, index) = CreateIndex();
        var uri = DocumentUri.From("file:///c:/work/b.ili");
        buffers.UpdateBuffer(uri, ModelB);

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
        buffers.UpdateBuffer(uri, ModelB);

        Assert.IsNull(index.FindModel("B", 2.3));
        Assert.IsNull(index.FindModel("Unknown", 2.4));

        buffers.ClearBuffer(uri);
        Assert.IsNull(index.FindModel("B", 2.4));
    }

    [TestMethod]
    public void ReportsAffectedModelsWhenAFileChanges()
    {
        var (buffers, index) = CreateIndex();
        var uri = DocumentUri.From("file:///c:/work/a.ili");
        var changes = new List<(DocumentUri Uri, IReadOnlyCollection<string> Models)>();
        index.FileChanged += (changedUri, models) => changes.Add((changedUri, models));

        buffers.UpdateBuffer(uri, ModelA);
        buffers.UpdateBuffer(uri, ModelA);
        buffers.UpdateBuffer(uri, ModelB);

        // The repeated identical text raises nothing.
        Assert.AreEqual(2, changes.Count);
        CollectionAssert.AreEquivalent(new[] { "A" }, changes[0].Models.ToList());
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, changes[1].Models.ToList());
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
}
