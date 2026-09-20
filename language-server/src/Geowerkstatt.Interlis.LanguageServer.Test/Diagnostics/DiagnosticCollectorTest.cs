using Geowerkstatt.Interlis.Compiler;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Diagnostics;

[TestClass]
public class DiagnosticCollectorTest
{
    private const string ModelWithUnresolvedType = """
        INTERLIS 2.4;
        MODEL TestModel (de) AT "http://models.geow.cloud" VERSION "1" =
            TOPIC TestTopic =
                CLASS ClassA =
                    attrA: Unknown;
                END ClassA;
            END TestTopic;
        END TestModel.
        """;

    [TestMethod]
    public void CollectsCompilerProblemsWithTheirRange()
    {
        var collector = new DiagnosticCollector();
        using (var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(collector)))
        {
            new InterlisReader(loggerFactory).ReadFile(new StringReader(ModelWithUnresolvedType), "file:///test.ili");
        }

        var diagnostic = collector.Diagnostics.Single();
        Assert.AreEqual(LogLevel.Error, diagnostic.Level);
        Assert.AreEqual("Could not resolve 'reference 'Unknown' from TestModel.TestTopic.ClassA' at 5:19-5:26", diagnostic.Message);
        Assert.AreEqual("file:///test.ili", diagnostic.Range.SourceUri);
        Assert.AreEqual("5:19-5:26", diagnostic.Range.ToString());
    }

    [TestMethod]
    public void IgnoresLogEntriesWithoutRange()
    {
        var collector = new DiagnosticCollector();
        using (var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(collector)))
        {
            var logger = loggerFactory.CreateLogger("Test");
            logger.LogError("Something failed for {Name}", "x");
            logger.LogWarning("Plain warning");
        }

        Assert.AreEqual(0, collector.Diagnostics.Count);
    }
}
