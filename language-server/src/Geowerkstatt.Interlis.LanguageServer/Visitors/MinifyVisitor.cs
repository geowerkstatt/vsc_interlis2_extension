using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Geowerkstatt.Interlis.Compiler;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

public class MinifyVisitor : Interlis24ParserBaseVisitor<MinifyVisitor.Part>
{
    protected override Part DefaultResult => Part.Empty;

    public override Part VisitTerminal(ITerminalNode node)
    {
        return node.Symbol.Type switch
        {
            TokenConstants.EOF => new Part(string.Empty, node.Symbol, node.Symbol),
            _ => new Part(node.Symbol.Text, node.Symbol, node.Symbol),
        };
    }

    public override Part VisitErrorNode(IErrorNode node)
    {
        return VisitTerminal(node);
    }

    protected override Part AggregateResult(Part aggregate, Part nextResult)
    {
        if (aggregate == Part.Empty)
        {
            return nextResult;
        }
        else if (nextResult == Part.Empty)
        {
            return aggregate;
        }
        else
        {
            var sb = new StringBuilder();
            sb.Append(aggregate.Content);
            if (aggregate.StopToken != null && nextResult.StartToken != null)
            {
                AppendDefault(sb, aggregate.StopToken, nextResult.StartToken);
            }

            sb.Append(nextResult.Content);

            return new Part(sb.ToString(), aggregate.StartToken, nextResult.StopToken);
        }
    }

    private void AppendDefault(StringBuilder sb, IToken lastToken, IToken nextToken)
    {
        var nextChar = nextToken.Text.First();
        var previousChar = lastToken.Text.Last();

        if (IsIdentifierChar(previousChar) && IsIdentifierChar(nextChar))
        {
            sb.Append(' ');
        }
    }

    private bool IsIdentifierChar(char c)
    {
        return char.IsLetter(c) || char.IsNumber(c) || c == '_';
    }

    public override Part VisitString([NotNull] Interlis24Parser.StringContext context)
    {
        return new Part(context.Start.TokenSource.InputStream.GetText(new Interval(context.Start.StartIndex, context.Stop.StopIndex)), context.Start, context.Stop);
    }

    public record Part(string Content, IToken? StartToken, IToken? StopToken)
    {
        public static readonly Part Empty = new Part(string.Empty, null, null);
    }
}
