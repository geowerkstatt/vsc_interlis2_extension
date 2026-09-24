using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler for hover requests (textDocument/hover): over a name, the fully qualified name of the element it denotes,
/// each of its names linked to the declaration it stands for, and the element's doc comments (<c>/** ... */</c>).
/// All of it comes from the document's compilation (see <see cref="SymbolLookup"/>), which holds the imported models
/// too, so no file has to be read.
/// </summary>
internal class HoverHandler(SymbolLookup symbolLookup, TextDocumentSelector textDocumentSelector) : HoverHandlerBase
{
    /// <inheritdoc />
    public override async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var lookup = await symbolLookup.ForDocumentAsync(request.TextDocument.Uri, cancellationToken);
        if (lookup.NameRangeAt(request.Position) is not { } range || lookup.FindAt(request.Position) is not { } target)
        {
            return null;
        }

        var markdown = new StringBuilder();
        markdown.Append(QualifiedName(target)).Append('\n');
        if (target is IDocumentation { DocComments.Count: > 0 } documented)
        {
            markdown.Append('\n').AppendJoin("\n\n", documented.DocComments.Select(DocCommentText)).Append('\n');
        }

        return new Hover
        {
            Range = range,
            Contents = new MarkedStringsOrMarkupContent(new MarkupContent { Kind = MarkupKind.Markdown, Value = markdown.ToString() }),
        };
    }

    /// <inheritdoc />
    protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = textDocumentSelector,
    };

    /// <summary>
    /// The fully qualified name of <paramref name="target"/> as Markdown: every name in code style and, where the
    /// element was written down, a link to its declaration, so that from <c>A.Topic.Base</c> the model, the topic and
    /// the class can each be opened. The names are separated as the compiler's
    /// <see cref="IInterlisDefinition.FullyQualifiedName"/> separates them (<c>.</c>, or <c> -> </c> before an
    /// attribute), taken from that name so that the two agree.
    /// </summary>
    private static string QualifiedName(IReferenceTarget target)
    {
        if (target is not IInterlisDefinition definition)
        {
            return Name(target);
        }

        var outermostFirst = new Stack<IInterlisDefinition>();
        for (var current = definition; current != null; current = current.Parent as IInterlisDefinition)
        {
            outermostFirst.Push(current);
        }

        var markdown = new StringBuilder();
        IInterlisDefinition? previous = null;
        foreach (var current in outermostFirst)
        {
            if (previous != null)
            {
                var qualifiedName = current.FullyQualifiedName;
                var separator = qualifiedName.Length >= previous.FullyQualifiedName.Length + current.Name.Length
                    ? qualifiedName[previous.FullyQualifiedName.Length..^current.Name.Length]
                    : ".";
                markdown.Append(separator);
            }

            markdown.Append(Name(current));
            previous = current;
        }

        return markdown.ToString();
    }

    /// <summary>
    /// The name of <paramref name="target"/> in code style, linked to its declaration when it has one: a
    /// <c>file:</c> link with the one-based line and column of the name, which the editor opens at that position.
    /// The destination is written in angle brackets, so a path with spaces or parentheses stays intact.
    /// </summary>
    private static string Name(IReferenceTarget target)
    {
        var code = $"`{target.Name}`";
        return SymbolLookup.DeclarationLocations(target).FirstOrDefault() is { } declaration
            ? $"[{code}](<{declaration.Uri}#L{declaration.Range.Start.Line + 1},{declaration.Range.Start.Character + 1}>)"
            : code;
    }

    /// <summary>
    /// The text of a doc comment without its delimiters: the leading <c>/**</c>, the trailing <c>*/</c> and, on each
    /// line, the indentation and the single <c>*</c> a continuation line conventionally starts with. Only a star
    /// followed by a space or the end of the line is taken for that convention, so <c>**bold**</c> keeps its
    /// emphasis; a Markdown bullet has to be written with <c>-</c>, because <c>* item</c> reads as a continuation line.
    /// </summary>
    private static string DocCommentText(string docComment)
    {
        var body = docComment.StartsWith("/**", StringComparison.Ordinal) && docComment.EndsWith("*/", StringComparison.Ordinal) ? docComment[3..^2] : docComment;
        var lines = body.Split('\n').Select(line => line.Trim()).Select(line => line == "*" || line.StartsWith("* ", StringComparison.Ordinal) ? line[1..].Trim() : line);
        return string.Join('\n', lines).Trim();
    }
}
