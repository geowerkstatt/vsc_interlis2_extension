using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

public class FormatterHandler : DocumentFormattingHandlerBase
{
    private readonly FileContentCache fileContentCache;

    public FormatterHandler(FileContentCache fileContentCache)
    {
        this.fileContentCache = fileContentCache;
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.ili")
        };
    }

    public override Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
    {
        var inputText = fileContentCache.GetBuffer(request.TextDocument.Uri);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reader = new InterlisReader(loggerFactory);

        // token stream from Lexer
        var tokenStream = reader.RunLexer(new StringReader(inputText));

        // parse tree from parser
        var interlisParser = reader.GetParser(tokenStream);
        var parseTree = interlisParser.interlis();

        var formatter = new FormatterVisitor(loggerFactory, tokenStream);
        var formattedOutput = formatter.VisitInterlis(parseTree);

        var lastToken = tokenStream.GetTokens().Last();
        var endPosition = new Position(lastToken.Line, lastToken.Column);

        var edit = new TextEdit
        {
            Range = new Range(new Position(0, 0), endPosition),
            NewText = formattedOutput + Environment.NewLine,
        };

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edit));
    }
}
