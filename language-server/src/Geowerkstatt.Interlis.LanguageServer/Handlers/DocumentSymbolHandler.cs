using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler for document symbol requests (textDocument/documentSymbol), which feed the outline, the breadcrumbs and
/// "Go to Symbol in Editor".
/// </summary>
internal class DocumentSymbolHandler(InterlisEnvironmentCache environmentCache, TextDocumentSelector textDocumentSelector) : DocumentSymbolHandlerBase
{
    /// <inheritdoc />
    public override async Task<SymbolInformationOrDocumentSymbolContainer?> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri;
        var compilation = await environmentCache.GetCompilationAsync(uri, cancellationToken);
        var symbols = new DocumentSymbolVisitor().VisitInterlisEnvironment(compilation.ForDocument(uri)) ?? [];
        return new SymbolInformationOrDocumentSymbolContainer(symbols.Select(symbol => new SymbolInformationOrDocumentSymbol(symbol)));
    }

    /// <inheritdoc />
    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = textDocumentSelector,
    };
}
