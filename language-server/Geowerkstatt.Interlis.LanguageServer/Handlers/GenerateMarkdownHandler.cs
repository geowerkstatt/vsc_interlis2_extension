using Geowerkstatt.Interlis.Tools;
using Geowerkstatt.Interlis.Tools.AST;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

public class GenerateMarkdownHandler : ExecuteTypedResponseCommandHandlerBase<GenerateMarkdownOptions, string?>
{
    public const string Command = "generateMarkdown";

    private readonly ILogger<GenerateMarkdownHandler> _logger;
    private readonly InterlisReader _interlisReader;
    private readonly FileContentCache _fileContentCache;

    public GenerateMarkdownHandler(ILogger<GenerateMarkdownHandler> logger, InterlisReader interlisReader, FileContentCache fileContentCache, ISerializer serializer) : base(Command, serializer)
    {
        _logger = logger;
        _interlisReader = interlisReader;
        _fileContentCache = fileContentCache;
    }

    public override Task<string?> Handle(GenerateMarkdownOptions options, CancellationToken cancellationToken)
    {
        var uri = options.Uri;
        var fileContent = uri == null ? null : _fileContentCache.GetBuffer(uri);
        if (string.IsNullOrEmpty(fileContent))
        {
            return Task.FromResult<string?>(null);
        }

        _logger.LogWarning("Generate markdown for {0}", uri);

        using var stringReader = new StringReader(fileContent);
        var interlisFile = _interlisReader.ReadFile(stringReader);
        var markdown = GenerateMarkdown(interlisFile);

        return Task.FromResult<string?>(markdown);
    }

    private static string GenerateMarkdown(InterlisFile interlisFile)
    {
        return string.Join(", ", interlisFile.Content.Keys);
    }
}
