using Geowerkstatt.Interlis.Compiler.AST;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

[TestClass]
public class CompilationTest
{
    [TestMethod]
    public void ForDocumentKeepsOnlyTheDocumentsOwnModels()
    {
        var document = DocumentUri.From("file:///c:/models/A.ili");
        var own = new ModelDef { Name = "A", SourceUri = document.ToString() };
        var imported = new ModelDef { Name = "B", SourceUri = "file:///c:/models/B.ili" };
        var environment = new InterlisEnvironment
        {
            Version = 2.4,
            Content = { { InternalModel.Interlis.Name, InternalModel.Interlis }, { "A", own }, { "B", imported } },
        };

        var documentEnvironment = new Compilation(environment, []).ForDocument(document);

        Assert.AreEqual(2.4, documentEnvironment.Version);
        CollectionAssert.AreEqual(new[] { own }, documentEnvironment.Content.Values.ToList());
    }
}
