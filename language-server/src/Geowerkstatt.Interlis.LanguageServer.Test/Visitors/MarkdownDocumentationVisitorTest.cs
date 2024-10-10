using Geowerkstatt.Interlis.Tools;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class MarkdownDocumentationVisitorTest
{
    private const string TestModel = """
    INTERLIS 2.4;

    MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
        TOPIC TestTopic =
            CLASS TestClass =
                attr1: TEXT*12;
                attr2: MANDATORY BOOLEAN;
            END TestClass;
        END TestTopic;
    END TestModel;
    """;

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
    END TestModel;
    """;

    [TestMethod]
    public void TestInterlisFile()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModel));

        var visitor = new MarkdownDocumentationVisitor();
        visitor.VisitInterlisFile(interlisFile);
        var documentation = visitor.GetDocumentation();

        const string expected = """
        # TestModel
        ## TestTopic
        ### TestClass
        | Attributname | Kardinalität | Typ |
        | --- | --- | --- |
        | attr1 | 0..1 | Text [12] |
        | attr2 | 1 | Boolean |


        """;

        Assert.AreEqual(expected.ReplaceLineEndings(), documentation.ReplaceLineEndings());
    }

    [TestMethod]
    public void TestInterlisFileAssociation()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModelAssociation));

        var visitor = new MarkdownDocumentationVisitor();
        visitor.VisitInterlisFile(interlisFile);
        var documentation = visitor.GetDocumentation();

        const string expected = """
        # TestModel
        ## TestTopic
        ### ClassA
        | Attributname | Kardinalität | Typ |
        | --- | --- | --- |
        | attrA | 0..1 | Text [10] |
        | AssocB | 1 | ClassB |

        ### ClassB
        | Attributname | Kardinalität | Typ |
        | --- | --- | --- |
        | attrB | 0..1 | 10..20 |
        | AssocA | 0..n | ClassA |


        """;

        Assert.AreEqual(expected.ReplaceLineEndings(), documentation.ReplaceLineEndings());
    }
}
