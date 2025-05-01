using System.Text;
using System.Text.RegularExpressions;
using Geowerkstatt.Interlis.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class DiagramDocumentVisitorTests
{
    private readonly InterlisReader _reader = new();
    private DiagramDocumentVisitor CreateVisitor()
        => new(NullLogger<DiagramDocumentVisitor>.Instance);

    [TestMethod]
    public void SingleClass_GeneratesBasicDiagram()
    {
        const string model = @"INTERLIS 2.4;
                                MODEL M (en) AT ""http://example.com"" VERSION ""1"" =
                                  TOPIC T =
                                    CLASS A =
                                      ;
                                    END A;
                                  END T;
                                END M.";

        var ast     = _reader.ReadFile(new StringReader(model));
        var visitor = CreateVisitor();
        visitor.VisitInterlisFile(ast);

        var diagram = visitor.GetDiagramDocument()
                             .ReplaceLineEndings("\n");

        var expected = new StringBuilder()
            .AppendLine("classDiagram")
            .AppendLine("direction LR")
            .AppendLine("namespace Topic_T {")
            .AppendLine("  class A")
            .AppendLine("}")
            .AppendLine()
            .AppendLine()
            .AppendLine()
            .ToString()
            .ReplaceLineEndings("\n");

        Assert.AreEqual(expected, diagram);
    }

    [TestMethod]
    public void ClassWithAttribute_GeneratesAttributeLine()
    {
        const string model = @"INTERLIS 2.4;
                                MODEL M (en) AT ""http://example.com"" VERSION ""1"" =
                                  TOPIC T =
                                    CLASS C =
                                      foo : TEXT*5;
                                    END C;
                                  END T;
                                END M.";

        var ast     = _reader.ReadFile(new StringReader(model));
        var visitor = CreateVisitor();
        visitor.VisitInterlisFile(ast);

        var diagram = visitor.GetDiagramDocument();
        StringAssert.Contains(diagram, "C: +Text [5] foo");
    }

    [TestMethod]
    public void Inheritance_GeneratesArrow()
    {
        const string model = @"INTERLIS 2.4;
                                MODEL M (en) AT ""http://example.com"" VERSION ""1"" =
                                  TOPIC T =
                                    CLASS Base = END Base;
                                    CLASS Derived EXTENDS Base = END Derived;
                                  END T;
                                END M.";

        var ast     = _reader.ReadFile(new StringReader(model));
        var visitor = CreateVisitor();
        visitor.VisitInterlisFile(ast);

        var diagram = visitor.GetDiagramDocument();
        StringAssert.Contains(diagram, "Derived --|> Base");
    }

    [TestMethod]
    public void BinaryAssociation_EmitsOnlyAttribute_WhenAssociationLogicIsNotYetHooked()
    {
        const string model = @"INTERLIS 2.4;
                                MODEL M (en) AT ""http://example.com"" VERSION ""1"" =
                                  TOPIC T =
                                    CLASS A = attr : B; END A;
                                    CLASS B = END B;
                                    ASSOCIATION AB = A -- {0..*} B; END AB;
                                  END T;
                                END M.";

        var ast     = _reader.ReadFile(new StringReader(model));
        var visitor = CreateVisitor();
        visitor.VisitInterlisFile(ast);

        var diagram = visitor.GetDiagramDocument();
        StringAssert.Contains(diagram, "A: +B attr");
        StringAssert.DoesNotMatch(diagram, new Regex("--o"));
    }

    [TestMethod]
    public void NonBinaryAssociation_RendersOnlyLastRolePair()
    {
        const string model = @"INTERLIS 2.4;
                                MODEL M (en) AT ""http://example.com"" VERSION ""1"" =
                                  TOPIC T =
                                    CLASS X = END X;
                                    CLASS Y = END Y;
                                    CLASS Z = END Z;
                                    ASSOCIATION XYZ = X -- Y; Y -- Z; END XYZ;
                                  END T;
                                END M.";

        var ast     = _reader.ReadFile(new StringReader(model));
        var visitor = CreateVisitor();
        visitor.VisitInterlisFile(ast);

        var diagram = visitor.GetDiagramDocument();
        StringAssert.Contains(diagram, "Y \"*\" --o \"*\" Z : XYZ");
    }
}
