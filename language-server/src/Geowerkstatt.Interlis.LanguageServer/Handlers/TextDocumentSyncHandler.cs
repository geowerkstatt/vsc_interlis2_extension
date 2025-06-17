using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using MediatR;
using Microsoft.Extensions.Logging;
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

    private readonly FileContentCache fileContentCache;
    private readonly ILanguageServerFacade router;
    private readonly TextDocumentSelector documentSelector = TextDocumentSelector.ForLanguage(LanguageName);

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public TextDocumentSyncHandler(FileContentCache fileContentCache, ILanguageServerFacade router)
    {
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
        var logProvider = new LinterLogProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logProvider));
        var interlisFile = new InterlisReader(loggerFactory).ReadFile(stringReader);
        var visitor = new LinterDocumentationVisitor(uri.Path);
        var diagnostics = visitor.VisitInterlisEnvironment(interlisFile) ?? new List<Diagnostic>();

        // Add log messages as diagnostics (extract position if present)
        var logLineRegex = new System.Text.RegularExpressions.Regex(@" at line (\d+):(\d+)");
        foreach (var log in logProvider.GetMessages())
        {
            var match = logLineRegex.Match(log);
            int line = 0, character = 0;
            string message = log;
            if (match.Success)
            {
                line = int.Parse(match.Groups[1].Value) - 1; // 0-based
                character = int.Parse(match.Groups[2].Value) - 1; // 0-based
                message = log.Substring(0, match.Index) + log.Substring(match.Index + match.Length);
                message = message.TrimEnd();
            }
            diagnostics.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Warning,
                Message = message,
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(new Position(line, character), new Position(line, character))
            });
        }

        router.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = diagnostics
        });
    }
}

