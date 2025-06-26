using Geowerkstatt.Interlis.Compiler;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class ModelImportVisitorTest
{
    private const string TestModelImports = """
        INTERLIS 2.4;

        MODEL ImportedModel (de) AT "http://models.geow.cloud" VERSION "1" =
            IMPORTS Text_V2;
            IMPORTS UNQUALIFIED OtherModel;
        END ImportedModel.

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            IMPORTS Text_V2, ImportedModel;

            TOPIC TestTopic =
                CLASS ClassA =
                    attrA: TEXT*10;
                END ClassA;
            END TestTopic;
        END TestModel.
        """;

    [TestMethod]
    public void TestImports()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModelImports));

        var visitor = new ModelImportVisitor();
        var imports = visitor.VisitInterlisEnvironment(interlisFile);

        var expected = new[] { "INTERLIS", "ImportedModel", "OtherModel", "Text_V2" };
        CollectionAssert.AreEquivalent(expected, imports?.ToList());
    }
}
