using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

[TestClass]
public class InterlisVersionHandlerTest
{
    private static readonly DocumentUri Document24 = DocumentUri.From("file:///c:/models/A.ili");
    private static readonly DocumentUri Document23 = DocumentUri.From("file:///c:/models/B.ili");
    private static readonly DocumentUri DocumentWithoutHeader = DocumentUri.From("file:///c:/models/C.ili");
    private static readonly DocumentUri RepositoryModel = DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), TestWorkspace.TempFolderName, "models.interlis.ch", "D.ili"));

    private static InterlisVersionHandler CreateHandler()
    {
        var workspace = TestWorkspace.Open(
            (Document24, """
                INTERLIS 2.4;
                MODEL A AT "http://example.com" VERSION "1" =
                END A.
                """),
            (Document23, """
                !! Header comment
                INTERLIS 2.3;
                MODEL B AT "http://example.com" VERSION "1" =
                END B.
                """),
            (DocumentWithoutHeader, """
                MODEL C AT "http://example.com" VERSION "1" =
                END C.
                """),
            (RepositoryModel, """
                INTERLIS 2.3;
                MODEL D AT "http://example.com" VERSION "1" =
                END D.
                """));
        return new InterlisVersionHandler(NullLogger<InterlisVersionHandler>.Instance, workspace.Index, workspace.Cache, new LspSerializer());
    }

    private static Task<double?> Version(InterlisVersionHandler handler, DocumentUri? uri)
        => handler.Handle(new InterlisVersionOptions(uri?.ToString()), CancellationToken.None);

    [TestMethod]
    public async Task ReturnsTheVersionOfTheHeader()
    {
        var handler = CreateHandler();

        Assert.AreEqual(2.4, await Version(handler, Document24));
        Assert.AreEqual(2.3, await Version(handler, Document23));
    }

    [TestMethod]
    public async Task ReturnsTheVersionOfARepositoryModelTheEditorShows()
    {
        // Repository models are not indexed, so the version comes from their compilation.
        Assert.AreEqual(2.3, await Version(CreateHandler(), RepositoryModel));
    }

    [TestMethod]
    public async Task ReturnsNullWithoutAHeaderOrForAnUnknownDocument()
    {
        var handler = CreateHandler();

        Assert.IsNull(await Version(handler, DocumentWithoutHeader));
        Assert.IsNull(await Version(handler, DocumentUri.From("file:///c:/models/Unknown.ili")));
        Assert.IsNull(await Version(handler, null));
    }
}
