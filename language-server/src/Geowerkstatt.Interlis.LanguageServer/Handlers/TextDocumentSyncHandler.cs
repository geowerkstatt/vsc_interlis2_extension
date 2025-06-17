using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler to synchronize the text document contents between the client and this server.
/// </summary>
internal class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private const string LanguageName = "INTERLIS2";

    private readonly InterlisReader interlisReader;
    private readonly FileContentCache fileContentCache;
    private readonly ILanguageServerFacade router;
    private readonly TextDocumentSelector documentSelector = TextDocumentSelector.ForLanguage(LanguageName);

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public TextDocumentSyncHandler(FileContentCache fileContentCache, ILanguageServerFacade router, InterlisReader interlisReader)
    {
        this.interlisReader = interlisReader;
        this.fileContentCache = fileContentCache;
        this.router = router;
    }

    /// <inheritdoc />
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, LanguageName);

    /// <inheritdoc />
    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        var text = notification.TextDocument.Text;
        var uri = notification.TextDocument.Uri;
        fileContentCache.UpdateBuffer(uri, text);
        RunLinter(uri, text);

        return Unit.Task;
    }

    /// <inheritdoc />
    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        var text = notification.ContentChanges.Last().Text;
        var uri = notification.TextDocument.Uri;
        fileContentCache.UpdateBuffer(uri, text);        
        RunLinter(uri, text);

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
        DocumentSelector = documentSelector,
        Change = Change,
        Save = new SaveOptions() { IncludeText = true }
    };

    private void RunLinter(DocumentUri uri, string text)
    {
        using var stringReader = new StringReader(text);
        var interlisFile = interlisReader.ReadFile(stringReader);
        var visitor = new LinterDocumentationVisitor(uri.Path);
        var diagnostics = visitor.VisitInterlisEnvironment(interlisFile);
        router.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = diagnostics ?? new List<Diagnostic>()
        });
    }
}

