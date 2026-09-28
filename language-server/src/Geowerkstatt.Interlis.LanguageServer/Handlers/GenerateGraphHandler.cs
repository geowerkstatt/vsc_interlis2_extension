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
/// Command handler that returns the class graph of an INTERLIS file and the models it imports as JSON
/// (see <see cref="GraphDocument"/>) for the interactive diagram renderer.
/// Responds to workspace.executeCommand requests using the executeCommandProvider capability of the language server protocol.
/// </summary>
public class GenerateGraphHandler : ExecuteTypedResponseCommandHandlerBase<GenerateGraphOptions, string?>
{
    public const string Command = "generateGraph";

    private readonly ILogger<GenerateGraphHandler> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly OpenDocuments openDocuments;
    private readonly InterlisEnvironmentCache environmentCache;
    private readonly DocumentationLanguageResolver languageResolver;

    public GenerateGraphHandler(ILogger<GenerateGraphHandler> logger, ILoggerFactory loggerFactory, OpenDocuments openDocuments, InterlisEnvironmentCache environmentCache, DocumentationLanguageResolver languageResolver, ISerializer serializer) : base(Command, serializer)
    {
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.openDocuments = openDocuments;
        this.environmentCache = environmentCache;
        this.languageResolver = languageResolver;
    }

    /// <summary>
    /// Handles the generateGraph requests.
    /// </summary>
    /// <param name="options">The requested options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>The graph as JSON, or <c>null</c> if the INTERLIS file was not found or could not be compiled.</returns>
    public override async Task<string?> Handle(GenerateGraphOptions options, CancellationToken cancellationToken)
    {
        if (options == null)
        {
            logger.LogWarning("generateGraph invoked without arguments");
            return null;
        }

        // Only open documents are known to the server; the client shows "Could not load diagram." for null.
        if (options.Uri is not { } uriString || !openDocuments.Contains(DocumentUri.From(uriString)))
        {
            return null;
        }

        var uri = DocumentUri.From(uriString);
        var uriForLog = uriString.Replace("\r", string.Empty).Replace("\n", string.Empty);
        logger.LogInformation("Generate graph for {Uri}", uriForLog);

        var locale = await languageResolver.ResolveAsync(options.Language, cancellationToken);

        try
        {
            // The same compilation the diagnostics use: cached, with the imports resolved. The whole environment is
            // visited so the graph can show the imported definitions instead of stubs.
            var compilation = await environmentCache.GetCompilationAsync(uri, cancellationToken);
            var visitor = new GraphDocumentVisitor(loggerFactory.CreateLogger<GraphDocumentVisitor>(), locale, uri.ToString());
            visitor.VisitInterlisEnvironment(compilation.Environment);
            return visitor.GetGraphJson();
        }
        catch (Exception ex)
        {
            // Editor content may be syntactically invalid, so a failed request degrades to the
            // webview's "Could not load diagram." message instead of crashing the server.
            logger.LogError(ex, "Failed to generate graph for {Uri}", uriForLog);
            return null;
        }
    }
}
