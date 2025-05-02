using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.Tools;
using Geowerkstatt.Interlis.Tools.AST;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
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

    public GenerateMarkdownHandler(ILogger<GenerateMarkdownHandler> logger, InterlisReader interlisReader, FileContentCache fileContentCache, ISerializer serializer) : base(Command, serializer)
    {
        this.logger = logger;
        this.interlisReader = interlisReader;
        this.fileContentCache = fileContentCache;
    }

    /// <summary>
    /// Handles the generateMarkdown requests.
    /// </summary>
    /// <param name="options">The requested options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>The generated markdown documentation, or <c>null</c> if the INTERLIS file was not found.</returns>
    public override Task<string?> Handle(GenerateMarkdownOptions options, CancellationToken cancellationToken)
    {
        if (options == null)
        {
            logger.LogWarning("generateMarkdown invoked without arguments");
            return Task.FromResult<string?>(null);
        }
        
        var uri = options.Uri;
        var fileContent = uri == null ? null : fileContentCache.GetBuffer(uri);
        if (string.IsNullOrEmpty(fileContent))
        {
            return Task.FromResult<string?>(null);
        }

        logger.LogInformation("Generate markdown for {0}", uri);

        using var stringReader = new StringReader(fileContent);
        var interlisFile = interlisReader.ReadFile(stringReader);
        var markdown = GenerateMarkdown(interlisFile);

        return Task.FromResult<string?>(markdown);
    }

    private static string GenerateMarkdown(InterlisFile interlisFile)
    {
        var visitor = new MarkdownDocumentationVisitor();
        visitor.VisitInterlisFile(interlisFile);
        return visitor.GetDocumentation();
    }
}
