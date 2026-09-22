using Geowerkstatt.Interlis.LanguageServer.Services;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler to resolve Goto Definition Requests (textDocument/definition) from the client.
/// </summary>
internal class DefinitionHandler(ILogger<DefinitionHandler> logger, SymbolLookup symbolLookup, TextDocumentSelector textDocumentSelector) : DefinitionHandlerBase
{
    /// <inheritdoc />
    public override async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        logger.LogTrace("Resolving Definition Request: {Request}", request);

        var lookup = await symbolLookup.ForDocumentAsync(request.TextDocument.Uri, cancellationToken);
        if (lookup.FindAt(request.Position) is not { } target)
        {
            return new LocationOrLocationLinks();
        }

        // Only the opening name, so the editor jumps to the declaration instead of offering a choice between it and
        // the name after END.
        var declaration = SymbolLookup.DeclarationLocations(target).FirstOrDefault();
        return declaration == null ? new LocationOrLocationLinks() : new LocationOrLocationLinks(new LocationOrLocationLink(declaration));
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
