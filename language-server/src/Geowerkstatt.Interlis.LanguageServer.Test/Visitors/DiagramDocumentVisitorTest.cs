using Geowerkstatt.Interlis.Compiler;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class DiagramDocumentVisitorTests
{
    // ─── helpers ────────────────────────────────────────────────────────────────
    private static string BuildDiagram(string ili)
    {
        var reader  = new InterlisReader();
        var ast     = reader.ReadFile(new StringReader(ili));
        var visitor = new DiagramDocumentVisitor(NullLogger<DiagramDocumentVisitor>.Instance, "LR");
        visitor.VisitInterlisEnvironment(ast);
        return visitor.GetDiagramDocument().ReplaceLineEndings("\n");
    }

    // ─── basic cases ────────────────────────────────────────────────────────────
    [TestMethod]
    public void SingleClass_InTopic_ProducesNamespaceAndClass()
    {
        var diagram = BuildDiagram(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
              END T;
            END M.");

        StringAssert.Contains(diagram, "namespace Topic_T {");
        StringAssert.Contains(diagram, "class A");
    }

    [TestMethod]
    public void EmptyTopic_IsOmitted_FromOutput()
    {
        var diagram = BuildDiagram(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC Empty = END Empty;
            END M.");

        Assert.IsFalse(diagram.Contains("Topic_Empty"));
    }

    // ─── attributes & cardinalities ────────────────────────────────────────────
    [TestMethod]
    public void Attribute_WithMultiplicityGreaterThanOne_GetsSuffix()
    {
        //  LIST {n..m} OF <X>  is valid INTERLIS and ends up as a BagType whose
        //  cardinality is what the visitor looks at.
        //
        const string ili = @"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS B = END B;
                CLASS C =
                  many          : LIST OF B;
                  exactlyTwoRef : LIST {2..2} OF B;
                END C;
              END T;
            END M.
        ";

        var diagram = BuildDiagram(ili);

        // fixed size list (2) must render the [2] suffix
        StringAssert.Contains(diagram, "exactlyTwoRef[2] #colon; **B**");
    }

    // ─── inheritance ───────────────────────────────────────────────────────────
    [TestMethod]
    public void Inheritance_GeneratesGeneralisationArrow()
    {
        var diagram = BuildDiagram(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Base = END Base;
                CLASS Derived EXTENDS Base = END Derived;
              END T;
            END M.");

        StringAssert.Contains(diagram, "Derived --|> Base");
    }

    // ─── associations ──────────────────────────────────────────────────────────
    [TestMethod]
    public void Composition_GeneratesFilledDiamond()
    {
        const string ili = @"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Whole = END Whole;
                CLASS Part  = END Part;
                ASSOCIATION Comp =
                  WholeRef -<#> {1} Whole;
                  PartRef  --   {0..*} Part;
                END Comp;
              END T;
            END M.
        ";

        var diagram = BuildDiagram(ili);

        // one end must have a filled diamond, the other an open one,
        // orientation does not matter.
        Assert.IsTrue(
            diagram.Contains("o--") || diagram.Contains("--o"),
            "Diamond arrow was not rendered as expected.");

        StringAssert.Contains(diagram, "Comp");   // label present
    }

    // ─── structures & styling ──────────────────────────────────────────────────
    [TestMethod]
    public void Structure_GetsDashedStyle()
    {
        var diagram = BuildDiagram(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                STRUCTURE S =
                  id : TEXT*10;
                END S;
              END T;
            END M.");

        StringAssert.Contains(diagram, "class S");
        StringAssert.Contains(diagram, "style S fill:,stroke-dasharray:10 10");
    }

    // ─── meta-attribute driven colour ──────────────────────────────────────────
    [TestMethod]
    public void ColorMetaAttribute_AddsStyleLine()
    {
        var diagram = BuildDiagram(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                !!@ geow.uml.color = ""#ffcc00""
                CLASS C = END C;
              END T;
            END M.");

        StringAssert.Contains(diagram, "style C fill:#ffcc00,color:black,stroke:black");
    }
}
