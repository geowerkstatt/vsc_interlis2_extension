using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler to synchronize the text document contents between the client and this server.
/// </summary>
internal class TextDocumentSyncHandler(FileContentCache fileContentCache, TextDocumentSelector textDocumentSelector) : TextDocumentSyncHandlerBase
{
    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    /// <inheritdoc />
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, ServerConstants.InterlisLanguageName);

    /// <inheritdoc />
    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        fileContentCache.UpdateBuffer(notification.TextDocument.Uri, notification.TextDocument.Text);
        return Unit.Task;
    }

    /// <inheritdoc />
    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        fileContentCache.UpdateBuffer(notification.TextDocument.Uri, notification.ContentChanges.Last().Text);
        return Unit.Task;
    }

    /// <inheritdoc />
    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
    {
        fileContentCache.ClearBuffer(notification.TextDocument.Uri);
        return Unit.Task;
    }

    /// <inheritdoc />
    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        fileContentCache.UpdateBuffer(request.TextDocument.Uri, request.Text ?? string.Empty);
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new TextDocumentSyncRegistrationOptions()
    {
        DocumentSelector = textDocumentSelector,
        Change = Change,
        Save = new SaveOptions() { IncludeText = true }
    };
}

