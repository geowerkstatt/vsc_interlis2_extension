using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Sharpen;
using Antlr4.Runtime.Tree;
using Geowerkstatt.Interlis.Compiler;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

public class FormatterVisitor : Interlis24ParserBaseVisitor<FormatterVisitor.Part>
{
    private readonly CommonTokenStream tokenStream;

    private int indentationSteps = 0;
    private int spacesPerIndentation = 2;

    /// <summary>
    /// Tokens that should not have a space before them.
    /// </summary>
    private static BitSet NoSpaceBefore = CreateBitSet([
        Interlis24Lexer.R_PAREN,
        Interlis24Lexer.R_BRACK,
        Interlis24Lexer.R_BRACE,
        Interlis24Lexer.DOUBLE_QUOTE_CLOSE,
        Interlis24Lexer.DOT,
        Interlis24Lexer.SEMICOLON,
        Interlis24Lexer.COMMA,
        Interlis24Lexer.COLON,
        Interlis24Lexer.ARROW,
        Interlis24Lexer.META_COMMENT_CLOSE,
    ]);

    /// <summary>
    /// Tokens that should not have a space after them.
    /// </summary>
    private static BitSet NoSpaceAfter = CreateBitSet([
        Interlis24Lexer.L_PAREN,
        Interlis24Lexer.L_BRACK,
        Interlis24Lexer.L_BRACE,
        Interlis24Lexer.DOUBLE_QUOTE_OPEN,
        Interlis24Lexer.DOT,
        Interlis24Lexer.NUMBER_SIGN,
        Interlis24Lexer.ARROW,
        Interlis24Lexer.DOUBLE_LEFT,
    ]);

    /// <summary>
    /// Tokens that require a newline after them.
    /// </summary>
    private static BitSet RequiresNewlineAfter = CreateBitSet([
        Interlis24Lexer.LINE_COMMENT,
        Interlis24Lexer.DOC_COMMENT,
        Interlis24Lexer.META_COMMENT_CLOSE,
    ]);

    /// <summary>
    /// Tokens that keep newlines around this token.
    /// </summary>
    private static BitSet KeepNewlines = CreateBitSet([
        Interlis24Lexer.META_COMMENT_OPEN,
        Interlis24Lexer.LINE_COMMENT,
        Interlis24Lexer.DOC_COMMENT,
        Interlis24Lexer.BLOCK_COMMENT,
    ]);

    /// <summary>
    /// Tokens that contain a newline.
    /// </summary>
    private static BitSet newLineTokens = CreateBitSet([
        Interlis24Lexer.LINEBREAK,
        Interlis24Lexer.LINE_COMMENT,
        Interlis24Lexer.META_COMMENT_CLOSE,
    ]);

    public FormatterVisitor(CommonTokenStream tokenStream)
    {
        this.tokenStream = tokenStream;
    }

    protected override Part DefaultResult => Part.Empty;

    /// <summary>
    /// Visit a terminal node. A terminal is responsible for all hidden tokens before and on the same line after the token.
    /// </summary>
    public override Part VisitTerminal(ITerminalNode node)
    {
        var hiddenTokensLeft = tokenStream.GetHiddenTokensToLeft(node.Symbol.TokenIndex);
        int start = hiddenTokensLeft switch
        {
            null => node.Symbol.TokenIndex,
            var tokens when tokens.First().TokenIndex == 0 => 0,
            var tokens => tokens.FirstOrDefault(t => newLineTokens.Get(t.Type), tokens.Last()).TokenIndex + 1,
        };

        var hiddenTokensRight = tokenStream.GetHiddenTokensToRight(node.Symbol.TokenIndex);
        int stop = hiddenTokensRight switch
        {
            null => node.Symbol.TokenIndex,
            var tokens when tokens.Last().Type == TokenConstants.EOF => tokens.Last().StopIndex,
            var tokens => tokens.FirstOrDefault(t => newLineTokens.Get(t.Type), tokens.Last()).TokenIndex,
        };

        return GetDefaultFormatting(start, stop);
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

    /// <summary>
    /// Get the string from the <see cref="tokenStream"/> between <paramref name="tokenStartIndex"/> and <paramref name="tokenStopIndex"/> with default formatting applied.
    /// </summary>
    internal Part GetDefaultFormatting(int tokenStartIndex, int tokenStopIndex)
    {
        if (tokenStartIndex < 0 || tokenStopIndex < 0 || tokenStartIndex > tokenStopIndex)
        {
            return Part.Empty;
        }

        var sb = new StringBuilder();
        var tokens = tokenStream.Get(tokenStartIndex, tokenStopIndex);
        IToken? firstToken = null;
        IToken? lastToken = null;
        foreach (var token in tokens)
        {
            if (token.Type == TokenConstants.EOF || token.Type == Interlis24Lexer.WHITESPACE || token.Type == Interlis24Lexer.LINEBREAK)
            {
                continue;
            }

            if (lastToken != null)
            {
                AppendDefault(sb, lastToken, token);
            }

            var lines = token.Text.Trim().Split(Environment.NewLine);
            sb.Append(lines[0]);
            if (lines.Length > 1)
            {
                foreach (var line in lines.Skip(1))
                {
                    AppendNewLines(sb, 1);

                    int trimCount = Math.Min(token.Column, line.TakeWhile(char.IsWhiteSpace).Count());
                    var trimmedLine = line.Substring(trimCount);
                    sb.Append(trimmedLine);
                }
            }

            lastToken = token;
            if (firstToken == null)
            {
                firstToken = token;
            }
        }

        return new Part(sb.ToString(), firstToken, lastToken);
    }

    /// <summary>
    /// Insert appropriate whitespace to the <see cref="StringBuilder"/> given the <paramref name="lastToken"/> and <paramref name="nextToken"/>.
    /// </summary>
    private void AppendDefault(StringBuilder sb, IToken lastToken, IToken nextToken)
    {
        if (KeepNewlines.Get(lastToken.Type) || KeepNewlines.Get(nextToken.Type))
        {
            AppendKeepNewlines(2)(sb, lastToken, nextToken);
        }
        else if (!NoSpaceAfter.Get(lastToken.Type) && !NoSpaceBefore.Get(nextToken.Type))
        {
            AppendSpace(sb, lastToken, nextToken);
        }
    }

    /// <summary>
    /// Returns an <see cref="InsertAction"/> that keeps the newline count from the input.
    /// If the input contains no newlines but the <paramref name="lastToken"/> requires a newline, it adds one.
    /// </summary>
    private Action<StringBuilder, IToken, IToken> AppendKeepNewlines(int maxNewlines = 2)
    {
        return (sb, lastToken, nextToken) =>
        {
            var newlineCount = GetNewlineCountBetween(lastToken, nextToken);

            newlineCount = Math.Min(maxNewlines, newlineCount);
            if (RequiresNewlineAfter.Get(lastToken.Type))
            {
                newlineCount = Math.Max(1, newlineCount);
            }

            AppendNewLines(sb, newlineCount);
        };
    }

    /// <summary>
    /// Returns an <see cref="InsertAction"/> that adds <paramref name="count"/> newlines to the <see cref="StringBuilder"/>.
    /// </summary>
    private Action<StringBuilder, IToken, IToken> AppendNewLine(int count = 1)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");
        return (sb, lasttoken, nextToken) => AppendNewLines(sb, count);
    }

    /// <summary>
    /// Append a single space to the <see cref="StringBuilder"/> except if the <paramref name="lastToken"/> requires a newline it adds one.
    /// </summary>
    private void AppendSpace(StringBuilder sb, IToken lastToken, IToken nextToken)
    {
        AppendNewLines(sb, RequiresNewlineAfter.Get(lastToken.Type) ? 1 : 0);
    }

    /// <summary>
    /// Append nothing to the <see cref="StringBuilder"/> except if the <paramref name="lastToken"/> requires a newline it adds one.
    /// </summary>
    private void AppendEmpty(StringBuilder sb, IToken lastToken, IToken nextToken)
    {
        if (RequiresNewlineAfter.Get(lastToken.Type))
        {
            AppendNewLines(sb, 1);
        }
    }

    /// <summary>
    /// Count how many newlines are between the <paramref name="lastToken"/> and the <paramref name="nextToken"/>.
    /// If lastToken is part of <see cref="newLineTokens"/>, it is also counted.
    /// </summary>
    private int GetNewlineCountBetween(IToken lastToken, IToken nextToken)
    {
        var start = lastToken.TokenIndex;
        var stop = nextToken.TokenIndex - 1;
        if (start > stop)
        {
            return 0;
        }

        return tokenStream.GetTokens(start, stop, newLineTokens)?.Count ?? 0;
    }

    private Predicate<IParseTree> IsOfType(int type)
    {
        return (child) => child is ITerminalNode node && node.Symbol.Type == type;
    }

    private static BitSet CreateBitSet(IEnumerable<int> items)
    {
        var bitSet = new BitSet();
        foreach (var item in items)
        {
            bitSet.Set(item);
        }

        return bitSet;
    }

    /// <summary>
    /// Format the <paramref name="children"/> of a context with the default rules and apply custom <paramref name="rules"/> between children.
    /// </summary>
    private Part FormatChildren(IList<IParseTree>? children, IList<Rule> rules)
    {
        using var scope = new CaptureValue<int>(indentationSteps, v => indentationSteps = v);

        Part accu = Part.Empty;

        IParseTree? previous = null;
        if (children != null)
        {
            foreach (var child in children)
            {
                var specialRules = rules.Where(rule =>
                    previous != null &&
                    (
                        (rule.RelativeLocation == RelativeLocation.Before && rule.Node.Invoke(child))
                        || (rule.RelativeLocation == RelativeLocation.After && rule.Node.Invoke(previous))
                    )
                ).ToList();

                if (specialRules.Any())
                {
                    var sb = new StringBuilder();
                    sb.Append(accu.Content);
                    specialRules.ForEach(r => r.BeforeAction?.Invoke());
                    var next = Visit(child);
                    if (accu.StopToken != null && next.StartToken != null)
                    {
                        specialRules.First().InsertAction.Invoke(sb, accu.StopToken, next.StartToken);
                    }

                    sb.Append(next.Content);
                    accu = new Part(sb.ToString(), accu.StartToken, next.StopToken);
                }
                else
                {
                    accu = AggregateResult(accu, Visit(child));
                }

                previous = child;
            }
        }

        return accu;
    }

    /// <summary>
    /// Append <paramref name="count"/> newlines to the <see cref="StringBuilder"/> <paramref name="sb"/>.
    /// If <paramref name="count"/> is zero just a single space is appended.
    /// </summary>
    private void AppendNewLines(StringBuilder sb, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sb.Append(Environment.NewLine);
        }

        sb.Append(' ', count <= 0 ? 1 : indentationSteps * spacesPerIndentation);
    }

    public override Part VisitInterlis([NotNull] Interlis24Parser.InterlisContext context)
    {
        var result = FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ModelDefContext, AppendNewLine(2))
            ]);

        // Append a newline at the end.
        return result with { Content = result.Content + Environment.NewLine };
    }

    public override Part VisitModelDef([NotNull] Interlis24Parser.ModelDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child == context.AT(), AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ModelContentsContext, AppendNewLine(2)),
                new Rule(RelativeLocation.After, child => child == context.EQUAL_SIGN(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child == context.END(), AppendNewLine(2), () => indentationSteps -= 1),
                new Rule(RelativeLocation.After, IsOfType(Interlis24Lexer.SEMICOLON), AppendNewLine()),
            ]);
    }

    public override Part VisitTopicDef([NotNull] Interlis24Parser.TopicDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.TopicContentsContext, AppendNewLine(2)),
                new Rule(RelativeLocation.After, child => child == context.EQUAL_SIGN(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child == context.END(), AppendNewLine(2), () => indentationSteps -= 1),
                new Rule(RelativeLocation.Before, child => child == context.EXTENDS(), AppendNewLine()),
                new Rule(RelativeLocation.After, IsOfType(Interlis24Lexer.SEMICOLON), AppendNewLine()),
            ]);
    }

    public override Part VisitClassDef([NotNull] Interlis24Parser.ClassDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child == context.ATTRIBUTE(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.After, child => child == context.ATTRIBUTE(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child == context.PARAMETER(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.After, child => child == context.PARAMETER(), AppendNewLine(), () => indentationSteps += 1),

                new Rule(RelativeLocation.After, child => child == context.EQUAL_SIGN(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child == context.END(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.Before, child => child == context.EXTENDS(), AppendNewLine()),
                new Rule(RelativeLocation.After, IsOfType(Interlis24Lexer.SEMICOLON), AppendNewLine()),

                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.AttributeDefContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ConstraintDefContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ParameterDefContext, AppendNewLine()),
            ]);
    }

    public override Part VisitAssociationDef([NotNull] Interlis24Parser.AssociationDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.EQUAL_SIGN()[0], AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child == context.END(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.Before, child => child == context.EXTENDS(), AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child == context.ATTRIBUTE(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.After, child => child == context.ATTRIBUTE(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.After, IsOfType(Interlis24Lexer.SEMICOLON), AppendNewLine()),

                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.RoleDefContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.AttributeDefContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ConstraintDefContext, AppendNewLine()),
            ]);
    }

    public override Part VisitDomainDef([NotNull] Interlis24Parser.DomainDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.DOMAIN(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.DomainTypeDefContext, AppendNewLine(2)),
            ]);
    }

    public override Part VisitDomainTypeDef([NotNull] Interlis24Parser.DomainTypeDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child == context.CONSTRAINTS(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.After, child => child == context.CONSTRAINTS(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.DomainConstraintContext, AppendNewLine()),
            ]);
    }

    public override Part VisitEnumeration([NotNull] Interlis24Parser.EnumerationContext context)
    {
        if (context.enumElement().Length <= 2)
        {
            return FormatChildren(context.children, []);
        }
        else
        {
            return FormatChildren(context.children,
                [
                    new Rule(RelativeLocation.After, child => child == context.L_PAREN(), AppendNewLine(), () => indentationSteps += 1),
                    new Rule(RelativeLocation.After, IsOfType(Interlis24Lexer.COMMA), AppendNewLine()),
                ]);
        }
    }

    public override Part VisitCoordinateType([NotNull] Interlis24Parser.CoordinateTypeContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.COORD(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.After, IsOfType(Interlis24Lexer.COMMA), AppendNewLine()),
            ]);
    }

    public override Part VisitTextType([NotNull] Interlis24Parser.TextTypeContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => true, AppendEmpty),
            ]);
    }

    public override Part VisitCardinality([NotNull] Interlis24Parser.CardinalityContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => true, AppendEmpty),
            ]);
    }

    public override Part VisitViewDef([NotNull] Interlis24Parser.ViewDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.EQUAL_SIGN(), AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child == context.EQUAL_SIGN(), AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child == context.formationDef() || child == context.EXTENDS(), AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child == context.END(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.After, child => child is ITerminalNode node && node.Symbol == context.name, AppendDefault, () => indentationSteps += 1),

                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.BaseExtensionDefContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.SelectionContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ConstraintDefContext, AppendNewLine()),
            ]);
    }

    public override Part VisitViewAttributes([NotNull] Interlis24Parser.ViewAttributesContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, IsOfType(Interlis24Lexer.ALL), AppendNewLine()),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.AttributeDefContext, AppendNewLine()),
                new Rule(RelativeLocation.Before, child => context._attribute.Contains((child as ITerminalNode)?.Symbol), AppendNewLine()),
            ]);
    }

    public override Part VisitRotationDef([NotNull] Interlis24Parser.RotationDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child == context.ARROW(), AppendSpace),
                new Rule(RelativeLocation.After, child => child == context.ARROW(), AppendSpace),
            ]);
    }

    public override Part VisitRefSys([NotNull] Interlis24Parser.RefSysContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.Before, child => child == context.L_BRACK(), AppendEmpty),
            ]);
    }

    public override Part VisitMetaDataBasketDef([NotNull] Interlis24Parser.MetaDataBasketDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.topic, AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, IsOfType(Interlis24Lexer.OBJECTS), AppendNewLine()),
            ]);
    }

    public override Part VisitConstraintsDef([NotNull] Interlis24Parser.ConstraintsDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.EQUAL_SIGN(), AppendNewLine(), () => indentationSteps += 1),
                new Rule(RelativeLocation.Before, child => child == context.END(), AppendNewLine(), () => indentationSteps -= 1),
                new Rule(RelativeLocation.Before, child => child is Interlis24Parser.ConstraintDefContext, AppendNewLine(1)),
            ]);
    }

    public override Part VisitAttributeDef([NotNull] Interlis24Parser.AttributeDefContext context)
    {
        return FormatChildren(context.children,
            [
                new Rule(RelativeLocation.After, child => child == context.children[0], AppendDefault, () => indentationSteps += 1),
            ]);
    }

    private enum RelativeLocation
    {
        Before,
        After,
    }

    private sealed class CaptureValue<T> : IDisposable
    {
        private readonly Action<T> reset;
        public T Value { get; }

        public CaptureValue(T value, Action<T> reset)
        {
            Value = value;
            this.reset = reset;
        }

        public void Dispose()
        {
            reset(Value);
        }
    }

    public record Part(string Content, IToken? StartToken, IToken? StopToken)
    {
        public static readonly Part Empty = new Part(string.Empty, null, null);
    }

    /// <summary>
    /// Record to define a special insert rule.
    /// </summary>
    /// <param name="RelativeLocation">Where this rule is applied relative to <paramref name="Node"/>.</param>
    /// <param name="Node">Return <see langword="true"/> if this rule applies to the <see cref="IParseTree"/> node.</param>
    /// <param name="InsertAction">The action that inserts custom spaces etc.</param>
    /// <param name="BeforeAction">Optional action to perform before the insert action, e.g. to change the indentation level.</param>
    private record Rule(RelativeLocation RelativeLocation, Predicate<IParseTree> Node, Action<StringBuilder, IToken, IToken> InsertAction, Action? BeforeAction = null);
}
