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

    private const string TestModelEnumeration = """
        INTERLIS 2.4;

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            TOPIC TestTopic =
                CLASS TestClass =
                    attr1: (
                        topValue1,
                        topValue2 (subValue1, subValue2, subValue3 (subSubValue1, subSubValue2)),
                        topValue3
                    );
                END TestClass;
            END TestTopic;
        END TestModel;
        """;

    private const string TestModelNestedStruct = """
        INTERLIS 2.4;

        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            TOPIC TestTopic =
                CLASS TestClass =
                    attr1: MANDATORY TestStruct;
                    attr2: 10..20;
                END TestClass;

                STRUCTURE TestStruct =
                    attr1: TEXT*10;
                    attr2: MANDATORY (value1, value2);
                END TestStruct;
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

    [TestMethod]
    public void TestInterlisFileEnumeration()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModelEnumeration));

        var visitor = new MarkdownDocumentationVisitor();
        visitor.VisitInterlisFile(interlisFile);
        var documentation = visitor.GetDocumentation();

        const string expected = """
            # TestModel
            ## TestTopic
            ### TestClass
            | Attributname | Kardinalität | Typ |
            | --- | --- | --- |
            | attr1 | 0..1 | (<b>topValue1</b>, <b>topValue2</b> (subValue1, subValue2, subValue3 (<i>subSubValue1</i>, <i>subSubValue2</i>)), <b>topValue3</b>) |


            """;

        Assert.AreEqual(expected.ReplaceLineEndings(), documentation.ReplaceLineEndings());
    }

    [TestMethod]
    public void TestInterlisFileNestedStruct()
    {
        var reader = new InterlisReader();
        var interlisFile = reader.ReadFile(new StringReader(TestModelNestedStruct));

        var visitor = new MarkdownDocumentationVisitor();
        visitor.VisitInterlisFile(interlisFile);
        var documentation = visitor.GetDocumentation();

        const string structInlineTable =
            "<table>" +
            "<thead>" +
            "<tr><th>Attributname</th><th>Kardinalität</th><th>Typ</th></tr>" +
            "</thead>" +
            "<tbody>" +
            "<tr><td>attr1</td><td>0..1</td><td>Text [10]</td></tr>" +
            "<tr><td>attr2</td><td>1</td><td>(<b>value1</b>, <b>value2</b>)</td></tr>" +
            "</tbody>" +
            "</table>";

        var expected = $"""
            # TestModel
            ## TestTopic
            ### TestClass
            | Attributname | Kardinalität | Typ |
            | --- | --- | --- |
            | attr1 | 1 | TestStruct<br/>{structInlineTable} |
            | attr2 | 0..1 | 10..20 |

            ### TestStruct
            | Attributname | Kardinalität | Typ |
            | --- | --- | --- |
            | attr1 | 0..1 | Text [10] |
            | attr2 | 1 | (<b>value1</b>, <b>value2</b>) |


            """;

        Assert.AreEqual(expected.ReplaceLineEndings(), documentation.ReplaceLineEndings());
    }
}
