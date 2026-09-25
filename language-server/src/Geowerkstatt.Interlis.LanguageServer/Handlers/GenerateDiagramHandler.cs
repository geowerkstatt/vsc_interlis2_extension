using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Command handler to generate diagram document for an INTERLIS file.
/// Responds to workspace.executeCommand requests using the executeCommandProvider capability of the language server protocol.
/// </summary>
public class GenerateDiagramHandler : ExecuteTypedResponseCommandHandlerBase<GenerateDiagramOptions, string?>
{
    public const string Command = "generateDiagram";

    private readonly ILogger<GenerateDiagramHandler> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly OpenDocuments openDocuments;
    private readonly InterlisEnvironmentCache environmentCache;
    private readonly DocumentationLanguageResolver languageResolver;

    public GenerateDiagramHandler(ILogger<GenerateDiagramHandler> logger, ILoggerFactory loggerFactory, OpenDocuments openDocuments, InterlisEnvironmentCache environmentCache, DocumentationLanguageResolver languageResolver, ISerializer serializer) : base(Command, serializer)
    {
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.openDocuments = openDocuments;
        this.environmentCache = environmentCache;
        this.languageResolver = languageResolver;
    }

    /// <summary>
    /// Handles the generateDiagram requests.
    /// </summary>
    /// <param name="options">The requested options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>The generated diagram document, or <c>null</c> if the INTERLIS file was not found.</returns>
    public override async Task<string?> Handle(GenerateDiagramOptions options, CancellationToken cancellationToken)
    {
        if (options == null)
        {
            logger.LogWarning("generateDiagram invoked without arguments");
            return null;
        }

        // Only open documents are known to the server; the client shows "Could not load diagram." for null.
        if (options.Uri is not { } uriString || !openDocuments.Contains(DocumentUri.From(uriString)))
        {
            return null;
        }

        var uri = DocumentUri.From(uriString);
        var uriForLog = uriString.Replace("\r", string.Empty).Replace("\n", string.Empty);
        logger.LogInformation("Generate diagram for {Uri}", uriForLog);

        var locale = await languageResolver.ResolveAsync(options.Language, cancellationToken);

        try
        {
            // The same compilation the diagnostics use: cached, with the imports resolved. Only the document's own
            // models are drawn.
            var compilation = await environmentCache.GetCompilationAsync(uri, cancellationToken);
            return GenerateDiagram(compilation.ForDocument(uri), options.Orientation, locale);
        }
        catch (Exception ex)
        {
            // File content comes from the editor and may be syntactically invalid;
            // the compiler can throw on malformed input. A failed diagram request
            // must degrade to the webview's "Could not load diagram." message,
            // never crash the language server. The exception is logged (not swallowed)
            // so it stays diagnosable in the Output channel.
            logger.LogError(ex, "Failed to generate diagram for {Uri}", uriForLog);
            return null;
        }
    }

    private string GenerateDiagram(InterlisEnvironment interlisFile, String orientation, DocumentationLocalization locale)
    {
        DiagramDocumentVisitor visitor = new DiagramDocumentVisitor(loggerFactory.CreateLogger<DiagramDocumentVisitor>(), orientation, locale);
        visitor.VisitInterlisEnvironment(interlisFile);
        return visitor.GetDiagramDocument();
    }
}
