using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Collections.Concurrent;

namespace Geowerkstatt.Interlis.LanguageServer.Diagnostics;

/// <summary>
/// Publishes the compiler's problems for an open document to the client (<c>textDocument/publishDiagnostics</c>).
/// Whenever a document's compilation is invalidated (its text changed, or a file it depends on did) it is compiled
/// again a short while later, so that typing does not compile on every keystroke; a further invalidation during the
/// wait or the compilation cancels that run. A closed document gets an empty list. Only the problems located in the
/// document itself are published; problems of the models it imports are reported in their own file and are dropped
/// here.
/// </summary>
internal sealed class DiagnosticsPublisher
{
    private readonly OpenDocuments openDocuments;
    private readonly InterlisEnvironmentCache environmentCache;
    private readonly ILanguageServerFacade languageServer;
    private readonly ILogger<DiagnosticsPublisher> logger;

    public DiagnosticsPublisher(OpenDocuments openDocuments, InterlisEnvironmentCache environmentCache, ILanguageServerFacade languageServer, ILogger<DiagnosticsPublisher> logger)
    {
        this.openDocuments = openDocuments;
        this.environmentCache = environmentCache;
        this.languageServer = languageServer;
        this.logger = logger;

        this.environmentCache.DocumentInvalidated += OnDocumentInvalidated;
    }

    /// <summary>The <c>source</c> shown next to the diagnostics in the client.</summary>
    private const string DiagnosticSource = "interlis";

    /// <summary>How long a document must stay unchanged before it is compiled.</summary>
    private static readonly TimeSpan PublishDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>The cancellation of the latest scheduled run per open document.</summary>
    private readonly ConcurrentDictionary<DocumentUri, CancellationTokenSource> pending = new();

    private void OnDocumentInvalidated(DocumentUri uri)
    {
        if (openDocuments.Contains(uri))
        {
            Schedule(uri);
        }
        else
        {
            Clear(uri);
        }
    }

    /// <summary>
    /// Compiles the document after the <see cref="PublishDelay"/> and publishes its diagnostics, unless it is scheduled
    /// again or cleared in the meantime.
    /// </summary>
    /// <param name="uri">The document to publish diagnostics for.</param>
    private void Schedule(DocumentUri uri)
    {
        var cancellation = new CancellationTokenSource();
        pending.AddOrUpdate(uri, cancellation, (_, previous) =>
        {
            previous.Cancel();
            return cancellation;
        });

        _ = PublishAsync(uri, cancellation.Token);
    }

    /// <summary>
    /// Removes the diagnostics of a document the client closed.
    /// </summary>
    /// <param name="uri">The closed document.</param>
    private void Clear(DocumentUri uri)
    {
        if (pending.TryRemove(uri, out var cancellation))
        {
            cancellation.Cancel();
        }

        Publish(uri, []);
    }

    /// <summary>
    /// The problems of a compilation that lie in the document itself. Problems of the models the document imports
    /// carry the imported file's URI and are not published on the document.
    /// </summary>
    internal static IEnumerable<CompilerDiagnostic> ForDocument(Compilation compilation, DocumentUri uri)
    {
        var documentUri = uri.ToString();
        return compilation.Diagnostics.Where(diagnostic => diagnostic.Range.SourceUri == documentUri);
    }

    /// <summary>
    /// Converts a compiler problem to the diagnostic sent to the client. The log level maps onto the four LSP
    /// severities: errors stay errors, warnings stay warnings, informational entries become information and the
    /// debug/trace levels become hints.
    /// </summary>
    internal static Diagnostic ToDiagnostic(CompilerDiagnostic diagnostic)
    {
        return new Diagnostic
        {
            Range = diagnostic.Range.ToOmnisharpRange(),
            Severity = diagnostic.Level switch
            {
                LogLevel.Critical or LogLevel.Error => DiagnosticSeverity.Error,
                LogLevel.Warning => DiagnosticSeverity.Warning,
                LogLevel.Information => DiagnosticSeverity.Information,
                _ => DiagnosticSeverity.Hint,
            },
            Message = diagnostic.Message,
            Source = DiagnosticSource,
        };
    }

    private async Task PublishAsync(DocumentUri uri, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PublishDelay, cancellationToken);
            var compilation = await environmentCache.GetCompilationAsync(uri, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Publish(uri, ForDocument(compilation, uri).Select(ToDiagnostic));
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change, or the document was closed.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish diagnostics for {Uri}.", uri);
        }
    }

    private void Publish(DocumentUri uri, IEnumerable<Diagnostic> diagnostics)
    {
        languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(diagnostics),
        });
    }
}
