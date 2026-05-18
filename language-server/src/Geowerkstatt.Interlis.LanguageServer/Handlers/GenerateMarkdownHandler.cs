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
/// Command handler to generate markdown documentation for an INTERLIS file.
/// Responds to workspace.executeCommand requests using the executeCommandProvider capability of the language server protocol.
/// </summary>
public class GenerateMarkdownHandler : ExecuteTypedResponseCommandHandlerBase<GenerateMarkdownOptions, string?>
{
    public const string Command = "generateMarkdown";

    private readonly ILogger<GenerateMarkdownHandler> logger;
    private readonly InterlisReader interlisReader;
    private readonly FileContentCache fileContentCache;
    private readonly ILanguageServerFacade languageServer;

    public GenerateMarkdownHandler(
        ILogger<GenerateMarkdownHandler> logger,
        InterlisReader interlisReader,
        FileContentCache fileContentCache,
        ILanguageServerFacade languageServer,
        ISerializer serializer)
        : base(Command, serializer)
    {
        this.logger = logger;
        this.interlisReader = interlisReader;
        this.fileContentCache = fileContentCache;
        this.languageServer = languageServer;
    }

    /// <summary>
    /// Handles the generateMarkdown requests.
    /// </summary>
    /// <param name="options">The requested options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>The generated markdown documentation, or <c>null</c> if the INTERLIS file was not found.</returns>
    public override async Task<string?> Handle(GenerateMarkdownOptions options, CancellationToken cancellationToken)
    {
        if (options == null)
        {
            logger.LogWarning("generateMarkdown invoked without arguments");
            return null;
        }

        var uri = options.Uri;
        var fileContent = uri == null ? null : await fileContentCache.GetAsync(uri);
        if (string.IsNullOrEmpty(fileContent))
        {
            return null;
        }

        var uriForLog = uri?.ToString()?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        logger.LogInformation("Generate markdown for {Uri}", uriForLog);

        var config = await GetDocumentationConfigAsync(cancellationToken);

        try
        {
            using var stringReader = new StringReader(fileContent);
            var interlisFile = interlisReader.ReadFile(stringReader);
            return GenerateMarkdown(interlisFile, config);
        }
        catch (Exception ex)
        {
            // File content comes from the editor and may be syntactically invalid;
            // the compiler can throw on malformed input. A failed documentation
            // request must degrade to the client's "please re-open the file" message,
            // never crash the language server. The exception is logged (not swallowed)
            // so it stays diagnosable in the Output channel.
            logger.LogError(ex, "Failed to generate markdown for {Uri}", uriForLog);
            return null;
        }
    }

    private async Task<DocumentationOptions> GetDocumentationConfigAsync(CancellationToken cancellationToken)
    {
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
                var defaults = new DocumentationOptions();
                return new DocumentationOptions
                {
                    AbstractClassAttributes = configToken?["abstractClassAttributes"]?.ToString() ?? defaults.AbstractClassAttributes,
                    AttributeNameHeader = configToken?["attributeNameHeader"]?.ToString() ?? defaults.AttributeNameHeader,
                    CardinalityHeader = configToken?["cardinalityHeader"]?.ToString() ?? defaults.CardinalityHeader,
                    TypeHeader = configToken?["typeHeader"]?.ToString() ?? defaults.TypeHeader,
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve configuration, using defaults");
        }

        return new DocumentationOptions();
    }

    private static string GenerateMarkdown(InterlisEnvironment interlisFile, DocumentationOptions config)
    {
        var visitor = new MarkdownDocumentationVisitor(config);
        visitor.VisitInterlisEnvironment(interlisFile);
        return visitor.GetDocumentation();
    }
}
