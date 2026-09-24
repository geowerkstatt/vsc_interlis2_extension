using Geowerkstatt.Interlis.Compiler;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class DocumentSymbolVisitorTest
{
    private const string TestModel = """
        INTERLIS 2.4;
        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            DOMAIN
                Kind = (a, b);
            TOPIC TestTopic =
                STRUCTURE Point =
                    Coord : 0.0 .. 100.0;
                END Point;

                CLASS ClassA =
                    attrA : TEXT*10;
                    MANDATORY CONSTRAINT DEFINED (attrA);
                END ClassA;

                ASSOCIATION Assoc =
                    RoleA -- {0..*} ClassA;
                    RoleB -- {1} ClassA;
                END Assoc;
            END TestTopic;
        END TestModel.
        """;

    private static List<DocumentSymbol> Symbols(string source)
    {
        var environment = new InterlisReader().ReadFile(new StringReader(source), "file:///test.ili");
        return new DocumentSymbolVisitor().VisitInterlisEnvironment(environment) ?? [];
    }

    [TestMethod]
    public void BuildsTheDefinitionTree()
    {
        var symbols = Symbols(TestModel);

        var model = symbols.Single();
        Assert.AreEqual("TestModel", model.Name);
        Assert.AreEqual(SymbolKind.Module, model.Kind);
        CollectionAssert.AreEqual(new[] { "Kind", "TestTopic" }, model.Children!.Select(c => c.Name).ToList());

        var topic = model.Children!.Single(c => c.Name == "TestTopic");
        Assert.AreEqual(SymbolKind.Namespace, topic.Kind);
        CollectionAssert.AreEqual(new[] { "Point", "ClassA", "Assoc" }, topic.Children!.Select(c => c.Name).ToList());
        CollectionAssert.AreEqual(new[] { SymbolKind.Struct, SymbolKind.Class, SymbolKind.Interface }, topic.Children!.Select(c => c.Kind).ToList());

        var classA = topic.Children!.Single(c => c.Name == "ClassA");
        CollectionAssert.AreEqual(new[] { "attrA", "Constraint1" }, classA.Children!.Select(c => c.Name).ToList());
        CollectionAssert.AreEqual(new[] { SymbolKind.Property, SymbolKind.Event }, classA.Children!.Select(c => c.Kind).ToList());

        var association = topic.Children!.Single(c => c.Name == "Assoc");
        CollectionAssert.AreEqual(new[] { SymbolKind.Field, SymbolKind.Field }, association.Children!.Select(c => c.Kind).ToList());

        Assert.AreEqual(SymbolKind.Enum, model.Children!.Single(c => c.Name == "Kind").Kind);
    }

    [TestMethod]
    public void RangesSpanTheDeclarationAndSelectTheName()
    {
        var symbols = Symbols(TestModel);
        var classA = symbols.Single().Children!.Single(c => c.Name == "TestTopic").Children!.Single(c => c.Name == "ClassA");

        // Zero-based: 'CLASS ClassA =' is line 9, 'END ClassA;' line 12; the name starts after 'CLASS ' (8 + 6 = 14).
        Assert.AreEqual(new Range(new Position(9, 8), new Position(12, 19)), classA.Range);
        Assert.AreEqual(new Range(new Position(9, 14), new Position(9, 20)), classA.SelectionRange);
    }

    [TestMethod]
    public void SkipsDefinitionsWithoutName()
    {
        // A class whose name is not yet typed: the parser recovers without a name; the outline shows the rest.
        var symbols = Symbols("""
            INTERLIS 2.4;
            MODEL M AT "urn:example" VERSION "1" =
              TOPIC T =
                CLASS
              END T;
            END M.
            """);

        Assert.AreEqual("M", symbols.Single().Name);
        Assert.AreEqual("T", symbols.Single().Children!.Single().Name);
    }
}
