using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

public class FormatterVisitor : Interlis24ParserBaseVisitor<string>
{
    private readonly CommonTokenStream tokenStream;
    private readonly ILogger logger;

    private int indentationSteps = 0;
    private int spacesPerIndentation = 4;

    /// <summary>
    /// Tokens that are not printet in the output.
    /// </summary>
    private static HashSet<int> SkippedTokens = new HashSet<int>
    {
        TokenConstants.EOF,
        Interlis24Lexer.WHITESPACE,
    };

    /// <summary>
    /// Tokens that should not have a space before them.
    /// </summary>
    private static HashSet<int> NoSpaceBefore = new HashSet<int>
    {
        Interlis24Lexer.SEMICOLON,
        Interlis24Lexer.COMMA,
        Interlis24Lexer.DOT,
        Interlis24Lexer.DOUBLE_QUOTE_CLOSE,
        Interlis24Lexer.R_PAREN,
        Interlis24Lexer.R_BRACK,
        Interlis24Lexer.R_BRACE,
    };

    /// <summary>
    /// Tokens that should not have a space after them.
    /// </summary>
    private static HashSet<int> NoSpaceAfter = new HashSet<int>
    {
        Interlis24Lexer.L_PAREN,
        Interlis24Lexer.L_BRACK,
        Interlis24Lexer.L_BRACE,
        Interlis24Lexer.DOUBLE_QUOTE_OPEN,
    };

    public FormatterVisitor(ILoggerFactory loggerFactory, CommonTokenStream tokenStream)
    {
        logger = loggerFactory.CreateLogger<FormatterVisitor>();
        this.tokenStream = tokenStream;
    }

    private class Scope : IDisposable
    {
        private Action? action;

        public Scope(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            action?.Invoke();
            action = null;
        }
    }

    /// <summary>
    /// Get the input text of the <paramref name="context"/>. That includes Whitespace and comments.
    /// </summary>
    private string GetInputText(ParserRuleContext context)
        => context.Start.InputStream.GetText(context.SourceInterval);

    /// <summary>
    /// Change the input in a way that every line has <paramref name="indentation"/> count of spaces at the beginning.
    /// </summary>
    internal static string SetIndentation(string input, int indentation)
    {
        if (indentation < 0) indentation = 0;
        var indent = new string(' ', indentation);
        var lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            // Only indent non-empty lines to avoid trailing spaces on blank lines
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = indent + lines[i].Trim();
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    internal string GetSpacesNormalizedString(int tokenStartIndex, int tokenStopIndex)
    {
        var sb = new StringBuilder();
        var tokens = tokenStream.Get(tokenStartIndex, tokenStopIndex);
        IToken? lastToken = null;
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (SkippedTokens.Contains(token.Type))
            {
                continue;
            }

            // Insert appropriate space
            if (lastToken != null && !NoSpaceAfter.Contains(lastToken.Type) && !NoSpaceBefore.Contains(token.Type))
            {
                sb.Append(' ');
            }

            sb.Append(token.Text.Trim());
            lastToken = token;
        }
        return SetIndentation(sb.ToString(), indentationSteps * spacesPerIndentation);
    }

    public override string VisitTerminal(ITerminalNode node)
    {
        return node.Symbol.Text;
    }

    protected override string AggregateResult(string aggregate, string nextResult)
    {
        return aggregate + nextResult;
    }

    public override string VisitInterlis([NotNull] Interlis24Parser.InterlisContext context)
    {
        var sb = new StringBuilder();

        var startIndex = context.Start.TokenIndex;
        var contentStartIndex = context.modelDef().FirstOrDefault()?.Start.TokenIndex;
        var stopIndex = context.Stop.TokenIndex;

        // Append all tokens at the start of the file
        sb.Append(GetSpacesNormalizedString(0, startIndex - 1));
        sb.Append(Environment.NewLine);

        if (contentStartIndex.HasValue)
        {
            sb.Append(GetSpacesNormalizedString(startIndex, contentStartIndex.Value - 1));
            sb.Append(Environment.NewLine);
            sb.Append(string.Concat(context.modelDef().Select(Visit)));
        }
        else
        {
            // No models
            sb.Append(GetSpacesNormalizedString(startIndex, stopIndex));
            sb.Append(Environment.NewLine);
        }

        // Things after the last model
        var fileEndString = GetSpacesNormalizedString(stopIndex + 1, tokenStream.Size - 2);
        if (!string.IsNullOrEmpty(fileEndString))
        {
            sb.Append(fileEndString);
            sb.Append(Environment.NewLine);
        }

        return sb.ToString();
    }

    public override string VisitModelDef([NotNull] Interlis24Parser.ModelDefContext context)
    {
        var sb = new StringBuilder();

        var startIndex = context.Start.TokenIndex;
        var atIndex = context.AT().Symbol.TokenIndex;
        var equalSignIndex = context.EQUAL_SIGN().Symbol.TokenIndex;
        var contentStartIndex = context.modelContents().FirstOrDefault()?.Start.TokenIndex;
        var contentEndIndex = context.modelContents().LastOrDefault()?.Stop.TokenIndex;
        var endIndex = context.END().Symbol.TokenIndex;
        var stopIndex = context.Stop.TokenIndex;

        sb.Append(GetSpacesNormalizedString(startIndex, atIndex - 1));
        sb.Append(Environment.NewLine);
        sb.Append(GetSpacesNormalizedString(atIndex, equalSignIndex));
        sb.Append(Environment.NewLine);

        indentationSteps += 1;
        using (var scope = new Scope(() => indentationSteps -= 1))
        {
            if (contentStartIndex.HasValue)
            {
                var modelConfig = GetSpacesNormalizedString(equalSignIndex + 1, contentStartIndex.Value - 1);
                if (!string.IsNullOrEmpty(modelConfig))
                {
                    sb.Append(modelConfig);
                    sb.Append(Environment.NewLine);
                }

                sb.Append(string.Concat(context.modelContents()
                    .Select(c => GetSpacesNormalizedString(c.Start.TokenIndex, c.Stop.TokenIndex))
                    .Select(s => s + Environment.NewLine)));
            }
            else
            {
                // no content
                var modelConfig = GetSpacesNormalizedString(equalSignIndex + 1, endIndex - 1);
                if (!string.IsNullOrEmpty(modelConfig))
                {
                    sb.Append(modelConfig);
                    sb.Append(Environment.NewLine);
                }
            }
        }

        // end
        sb.Append(GetSpacesNormalizedString(endIndex, stopIndex));
        sb.Append(Environment.NewLine);

        return sb.ToString();
    }
}
