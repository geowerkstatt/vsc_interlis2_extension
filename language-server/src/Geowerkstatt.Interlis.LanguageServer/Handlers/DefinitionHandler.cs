using Geowerkstatt.Interlis.LanguageServer.Cache;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers
{
    internal class DefinitionHandler(ILogger<DefinitionHandler> logger, ReferenceCache referenceCache, TextDocumentSelector textDocumentSelector) : DefinitionHandlerBase
    {
        public override Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            logger.LogTrace($"Resolving {nameof(DefinitionParams)}: {request}");

            var uri = request.TextDocument.Uri;
            var location = request.Position;

            var references = referenceCache
                .Get(uri)
                .Where(r => r.Start <= location && r.End >= location && r.Target.NameLocations.Any())
                .Select(r => new Location
                {
                    Uri = uri,
                    Range = new Range(r.Target.NameLocations.First().Start.ToOmnisharpPosition(), r.Target.NameLocations.First().End.ToOmnisharpPosition())
                })
                .Select(location => new LocationOrLocationLink(location));


            return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(references.ToList()));
        }

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions()
            {
                DocumentSelector = textDocumentSelector,
            };
        }
    }
}
