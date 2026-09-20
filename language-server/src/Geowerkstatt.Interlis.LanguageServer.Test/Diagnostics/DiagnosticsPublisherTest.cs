using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Geowerkstatt.Interlis.LanguageServer.Diagnostics;

[TestClass]
public class DiagnosticsPublisherTest
{
    [TestMethod]
    public void ForDocumentKeepsOnlyTheDocumentsOwnProblems()
    {
        var document = DocumentUri.From("file:///c:/models/Test.ili");
        var inDocument = new CompilerDiagnostic(LogLevel.Error, "in document", new RangePosition(1, 0, 1, 5, document.ToString()));
        var inImport = new CompilerDiagnostic(LogLevel.Error, "in import", new RangePosition(1, 0, 1, 5, "file:///c:/models/Imported.ili"));
        var withoutSource = new CompilerDiagnostic(LogLevel.Warning, "no source", new RangePosition(1, 0, 1, 5));
        var compilation = new Compilation(new InterlisEnvironment(), [inDocument, inImport, withoutSource]);

        var published = DiagnosticsPublisher.ForDocument(compilation, document).ToList();

        CollectionAssert.AreEqual(new[] { inDocument }, published);
    }

    [TestMethod]
    public void ToDiagnosticCarriesRangeMessageAndSource()
    {
        var diagnostic = DiagnosticsPublisher.ToDiagnostic(new CompilerDiagnostic(LogLevel.Error, "Could not resolve 'X'", new RangePosition(4, 19, 4, 26, "file:///test.ili")));

        Assert.AreEqual(new Position(4, 19), diagnostic.Range.Start);
        Assert.AreEqual(new Position(4, 26), diagnostic.Range.End);
        Assert.AreEqual("Could not resolve 'X'", diagnostic.Message);
        Assert.AreEqual("interlis", diagnostic.Source);
    }

    [DataTestMethod]
    [DataRow(LogLevel.Critical, DiagnosticSeverity.Error)]
    [DataRow(LogLevel.Error, DiagnosticSeverity.Error)]
    [DataRow(LogLevel.Warning, DiagnosticSeverity.Warning)]
    [DataRow(LogLevel.Information, DiagnosticSeverity.Information)]
    [DataRow(LogLevel.Debug, DiagnosticSeverity.Hint)]
    [DataRow(LogLevel.Trace, DiagnosticSeverity.Hint)]
    public void ToDiagnosticMapsEveryLogLevelToASeverity(LogLevel level, DiagnosticSeverity expected)
    {
        var diagnostic = DiagnosticsPublisher.ToDiagnostic(new CompilerDiagnostic(level, "message", new RangePosition(0, 0, 0, 1)));

        Assert.AreEqual(expected, diagnostic.Severity);
    }
}
