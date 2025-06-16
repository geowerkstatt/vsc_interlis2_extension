using Geowerkstatt.Interlis.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class ReactFlowVisitorTests
{
    // ─── helpers ────────────────────────────────────────────────────────────────
    private static string BuildDiagram(string ili)
    {
        var reader  = new InterlisReader();
        var ast     = reader.ReadFile(new StringReader(ili));
        var visitor = new ReactFlowVisitor(NullLogger<ReactFlowVisitor>.Instance, "LR");
        visitor.VisitInterlisFile(ast);
        return visitor.GetDiagramDocument().ReplaceLineEndings("\n");
    }

    [TestMethod]
    public void RunTest()
    {
        var diagram = BuildDiagram(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
              END T;
            END M.");
        Debug.WriteLine("hi");
        Debug.WriteLine(diagram);
    }
}
