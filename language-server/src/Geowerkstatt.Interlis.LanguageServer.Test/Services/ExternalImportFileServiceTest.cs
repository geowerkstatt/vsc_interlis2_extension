using Geowerkstatt.Interlis.RepositoryCrawler.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

[TestClass]
public class ExternalImportFileServiceTest
{
    private const string TempFolderName = "INTERLIS Language Server ExternalImportFileServiceTest";

    private static readonly Repository Repository = new() { HostNameId = "localhost", Uri = new Uri("http://localhost/models/"), Name = "localhost" };

    private static ExternalImportFileService CreateService() =>
        new(NullLogger<ExternalImportFileService>.Instance, Options.Create(new ServerOptions { LanguageName = "INTERLIS2", TempFolderName = TempFolderName }));

    private static Model CreateModel(string content) => new()
    {
        Name = "M",
        SchemaLanguage = "ili2_4",
        File = "sub/M.ili",
        Version = "1",
        ModelRepository = Repository,
        FileContent = new InterlisFile { MD5 = "", Content = content },
    };

    [TestMethod]
    public async Task NewReleaseOfTheModelReplacesTheStoredCopy()
    {
        using var service = CreateService();
        var first = await service.GetModelUriAsync(CreateModel("INTERLIS 2.4;\r\nMODEL M ="));
        var second = await service.GetModelUriAsync(CreateModel("!! Release 2\r\n\r\nINTERLIS 2.4;\r\nMODEL M ="));

        Assert.IsNotNull(second);
        Assert.AreEqual(first, second);
        Assert.AreEqual("!! Release 2\r\n\r\nINTERLIS 2.4;\r\nMODEL M =", File.ReadAllText(second.LocalPath));
        Assert.IsTrue(File.GetAttributes(second.LocalPath).HasFlag(FileAttributes.ReadOnly), "The stored copy stays read-only.");
    }
}
