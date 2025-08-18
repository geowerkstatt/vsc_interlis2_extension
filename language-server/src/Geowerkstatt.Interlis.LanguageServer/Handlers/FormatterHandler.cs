using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Geowerkstatt.Interlis.LanguageServer.Visitors;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

public class FormatterHandler : DocumentFormattingHandlerBase
{
    private readonly FileContentCache fileContentCache;
    private readonly TextDocumentSelector documentSelector;

    public FormatterHandler(FileContentCache fileContentCache, TextDocumentSelector documentSelector)
    {
        this.fileContentCache = fileContentCache;
        this.documentSelector = documentSelector;
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = documentSelector,
        };
    }

    public override async Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
    {
        var inputText = await fileContentCache.GetAsync(request.TextDocument.Uri);

        var loggerFactory = NullLoggerFactory.Instance;
        var reader = new InterlisReader(loggerFactory);

        // token stream from Lexer
        var tokenStream = reader.RunLexer(new StringReader(inputText));

        // parse tree from parser
        var interlisParser = reader.GetParser(tokenStream);
        var parseTree = interlisParser.interlis();

        var formatter = new FormatterVisitor(tokenStream);
        var formattedOutput = formatter.VisitInterlis(parseTree);

        var lastToken = tokenStream.GetTokens().Last();
        var endPosition = new Position(lastToken.Line, lastToken.Column);

        var edit = new TextEdit
        {
            Range = new Range(new Position(0, 0), endPosition),
            NewText = formattedOutput.Content,
        };

        return new TextEditContainer(edit);
    }
}
