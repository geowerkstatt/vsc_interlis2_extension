using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

internal class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private const string LanguageName = "INTERLIS2";

    private readonly FileContentCache _fileContentCache;
    private readonly TextDocumentSelector _documentSelector = TextDocumentSelector.ForLanguage(LanguageName);

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public TextDocumentSyncHandler(FileContentCache fileContentCache)
    {
        _fileContentCache = fileContentCache;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, LanguageName);

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        _fileContentCache.UpdateBuffer(notification.TextDocument.Uri, notification.TextDocument.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        _fileContentCache.UpdateBuffer(notification.TextDocument.Uri, notification.ContentChanges.Last().Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
    {
        _fileContentCache.ClearBuffer(notification.TextDocument.Uri);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        _fileContentCache.UpdateBuffer(request.TextDocument.Uri, request.Text ?? string.Empty);
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new TextDocumentSyncRegistrationOptions()
    {
        DocumentSelector = _documentSelector,
        Change = Change,
        Save = new SaveOptions() { IncludeText = true }
    };
}

