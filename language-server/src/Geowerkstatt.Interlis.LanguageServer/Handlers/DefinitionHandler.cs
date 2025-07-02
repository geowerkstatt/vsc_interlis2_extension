using Geowerkstatt.Interlis.LanguageServer.Cache;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler to resolve Goto Definition Requests (textDocument/definition) from the client.
/// </summary>
internal class DefinitionHandler(ILogger<DefinitionHandler> logger, ReferenceCache referenceCache, TextDocumentSelector textDocumentSelector) : DefinitionHandlerBase
{
    /// <inheritdoc />
    public override async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        logger.LogTrace("Resolving Definition Request: {Request}", request);

        var uri = request.TextDocument.Uri;
        var location = request.Position;

        var allReferencesInFile = await referenceCache.GetAsync(uri);
        var references = allReferencesInFile
            .Where(r => r.OccurenceStart <= location && r.OccurenceEnd >= location && r.Target.NameLocations.Count != 0)
            .Select(r => new Location
            {
                Uri = r.TargetFile,
                Range = r.Target.NameLocations.First().ToOmnisharpRange(),
            })
            .Select(location => new LocationOrLocationLink(location))
            .ToList();

        return new LocationOrLocationLinks(references);
    }

    /// <inheritdoc />
    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DefinitionRegistrationOptions()
        {
            DocumentSelector = textDocumentSelector,
        };
    }
}
