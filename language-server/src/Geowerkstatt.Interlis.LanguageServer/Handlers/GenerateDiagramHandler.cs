using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
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
    private readonly InterlisReader interlisReader;
    private readonly FileContentCache fileContentCache;
    private readonly ILanguageServerFacade languageServer;
    private readonly UiLanguageContext uiLanguageContext;

    public GenerateDiagramHandler(ILogger<GenerateDiagramHandler> logger, ILoggerFactory loggerFactory, InterlisReader interlisReader, FileContentCache fileContentCache, ILanguageServerFacade languageServer, UiLanguageContext uiLanguageContext, ISerializer serializer) : base(Command, serializer)
    {
        this.logger = logger;
        this.interlisReader = interlisReader;
        this.fileContentCache = fileContentCache;
        this.loggerFactory = loggerFactory;
        this.languageServer = languageServer;
        this.uiLanguageContext = uiLanguageContext;
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

        var uri = options.Uri;
        var orientation = options.Orientation;
        var fileContent = uri == null ? null : await fileContentCache.GetAsync(uri);
        if (string.IsNullOrEmpty(fileContent))
        {
            return null;
        }

        var uriForLog = uri?.ToString()?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        logger.LogInformation("Generate diagram for {Uri}", uriForLog);

        var locale = await GetLocalizationAsync(options.Language, cancellationToken);

        try
        {
            using var stringReader = new StringReader(fileContent);
            var interlisFile = interlisReader.ReadFile(stringReader);
            return GenerateDiagram(interlisFile, orientation, locale);
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

    private async Task<DocumentationLocalization> GetLocalizationAsync(string? requestLanguage, CancellationToken cancellationToken)
    {
        // Webview dropdown overrides the workspace setting; skip the round-trip when present.
        if (!string.IsNullOrEmpty(requestLanguage))
        {
            return DocumentationLocalization.For(uiLanguageContext.Resolve(requestLanguage));
        }

        try
        {
            var configRequest = new ConfigurationParams
            {
                Items = new[]
                {
                    new ConfigurationItem
                    {
                        Section = DocumentationOptions.ConfigSection
                    }
                }
            };

            var response = await languageServer.Workspace.RequestConfiguration(configRequest, cancellationToken);
            if (response.Any())
            {
                var configToken = response.First();
                var language = uiLanguageContext.Resolve(configToken?["language"]?.ToString());
                return DocumentationLocalization.For(language);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve configuration, using defaults");
        }

        return DocumentationLocalization.For(uiLanguageContext.Language);
    }

    private string GenerateDiagram(InterlisEnvironment interlisFile, String orientation, DocumentationLocalization locale)
    {
        DiagramDocumentVisitor visitor = new DiagramDocumentVisitor(loggerFactory.CreateLogger<DiagramDocumentVisitor>(), orientation, locale);
        visitor.VisitInterlisEnvironment(interlisFile);
        return visitor.GetDiagramDocument();
    }
}
