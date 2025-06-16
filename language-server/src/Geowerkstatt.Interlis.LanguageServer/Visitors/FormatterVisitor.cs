using Antlr4.Runtime.Tree;
using Geowerkstatt.Interlis.Compiler;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

public class FormatterVisitor : Interlis24ParserBaseVisitor<string>
{
    private readonly ILogger logger;

    public FormatterVisitor(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<FormatterVisitor>();
    }

    public override string VisitTerminal(ITerminalNode node)
    {
        return node.ToString() ?? string.Empty;
    }

    protected override string AggregateResult(string aggregate, string nextResult)
    {
        return aggregate + nextResult;
    }

    public override string VisitInterlis([NotNull] Interlis24Parser.InterlisContext context)
    {
        var start = context.Start.StartIndex;
        var stop = context.Stop.StopIndex;
        var inputStream = context.Start.InputStream;
        var result = inputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, stop));
        return result;
    }
}
