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
        var interlisFile = reader.ReadFile(new StringReader(TestModelAssociation));

        var visitor = new ReferenceCollectorVisitor();
        var references = visitor.VisitInterlisEnvironment(interlisFile);

        Assert.IsNotNull(references);
        Assert.AreEqual(2, references.Count);
        Assert.AreEqual("ClassA", references[0].target.Name);
        Assert.AreEqual("ClassB", references[1].target.Name);
    }
}
