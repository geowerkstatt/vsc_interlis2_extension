using Geowerkstatt.Interlis.Compiler;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class ReferenceCollectorVisitorTest
{
    private const string TestModelAssociation = """
        INTERLIS 2.4;

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            TOPIC TestTopic =
                CLASS ClassA =
                    attrA: TEXT*10;
                END ClassA;

                CLASS ClassB =
                    attrB: 10..20;
                END ClassB;

                ASSOCIATION Assoc1 =
                    AssocA -- {0..*} ClassA;
                    AssocB -<> {1} ClassB;
                END Assoc1;
            END TestTopic;
        END TestModel.
        """;

    [TestMethod]
    public void TestInterlisFile()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModelAssociation), "file://test.ili");

        var visitor = new ReferenceCollectorVisitor();
        var references = visitor.VisitInterlisEnvironment(interlisFile);

        Assert.IsNotNull(references);
        CollectionAssert.AreEqual(new[] { "ClassA", "ClassB" }, references.Select(r => r.Target.Name).ToList());
    }

    private const string TestModelImports = """
        INTERLIS 2.4;

        MODEL ImportedModel (de) AT "http://models.geow.cloud" VERSION "1" =
        END ImportedModel.

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            IMPORTS ImportedModel;

            TOPIC TestTopic =
                CLASS ClassA =
                    attrA: TEXT*10;
                END ClassA;

                CLASS ClassB =
                    attrB: 10..20;
                END ClassB;

                ASSOCIATION Assoc1 =
                    AssocA -- {0..*} ClassA;
                    AssocB -<> {1} ClassB;
                END Assoc1;
            END TestTopic;
        END TestModel.
        """;

    [TestMethod]
    public void TestInterlisImports()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModelImports), "file://test.ili");

        var visitor = new ReferenceCollectorVisitor();
        var references = visitor.VisitInterlisEnvironment(interlisFile);

        Assert.IsNotNull(references);
        CollectionAssert.AreEqual(new[] { "ImportedModel", "ClassA", "ClassB" }, references.Select(r => r.Target.Name).ToList());
    }
}
