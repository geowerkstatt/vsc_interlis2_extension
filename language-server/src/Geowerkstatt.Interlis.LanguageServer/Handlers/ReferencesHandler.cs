using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler for reference requests (textDocument/references), which feed _Find All References_ and _Peek References_.
/// <para>
/// The element at the requested position comes from the document itself (see <see cref="SymbolLookup"/>). Its uses
/// are then collected from the files that can name it: the files defining the models of the file declaring it and
/// the files importing those models, as the workspace index knows them, plus the requesting document itself. Each
/// of those files is compiled, without having to be open, because a file's own compilation is the only place where
/// its references are resolved.
/// </para>
/// </summary>
internal class ReferencesHandler(SymbolLookup symbolLookup, WorkspaceModelIndex workspaceModelIndex, TextDocumentSelector textDocumentSelector) : ReferencesHandlerBase
{
    /// <inheritdoc />
    public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri;
        var ownLookup = await symbolLookup.ForDocumentAsync(uri, cancellationToken);
        // The declaration first: the token of 'PROJECTION OF ClassA' declares the view's base and references the class,
        // and from a declaration the uses of the declared element are wanted (see DocumentLookup.DeclaredAt).
        if ((ownLookup.DeclaredAt(request.Position) ?? ownLookup.ReferencedAt(request.Position)) is not { } target)
        {
            return null;
        }

        var locations = new List<Location>();
        if (request.Context.IncludeDeclaration)
        {
            locations.AddRange(SymbolLookup.DeclarationLocations(target));
        }

        // The requesting document is searched even when the index does not list it: a repository model opened through
        // go-to-definition lives in the temp folder, which the index skips so that it never shadows the repository.
        var files = ownLookup.DeclaringModels(target)
            .SelectMany(model => workspaceModelIndex.FindFilesUsing(model.Name))
            .Select(file => file.Uri)
            .Append(uri)
            .Distinct();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lookup = file == uri ? ownLookup : await symbolLookup.ForDocumentAsync(file, cancellationToken);
            locations.AddRange(lookup.Occurrences(target));
        }

        return new LocationContainer(locations);
    }

    /// <inheritdoc />
    protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = textDocumentSelector,
    };
}
