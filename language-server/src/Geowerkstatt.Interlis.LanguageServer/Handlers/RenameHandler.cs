using Antlr4.Runtime;
using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Services;
using Geowerkstatt.Interlis.LanguageServer.Workspace;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Handler for rename requests (textDocument/prepareRename and textDocument/rename), which feed _Rename Symbol_.
/// <para>
/// The element at the requested position is found as for go-to-definition (see <see cref="DocumentLookup.FindAt"/>).
/// A name that only follows another element's name is not renamed on its own: from an implicit base name
/// (<c>ALL OF ClassA</c> in a view over <c>ClassA</c>) or an <c>EXTENDED</c> redefinition the rename moves to the
/// element they are named after (see <see cref="AstExtensions.NameSource"/>). The renamed element's declaration,
/// its uses in the files that can name it (see <see cref="ReferencesHandler"/>) and, transitively, the elements named
/// after it with their own uses are rewritten, each as the span of that one name.
/// </para>
/// </summary>
internal class RenameHandler(SymbolLookup symbolLookup, WorkspaceModelIndex workspaceModelIndex, ExternalImportFileService externalImportFileService, TextDocumentSelector textDocumentSelector) : RenameHandlerBase, IPrepareRenameHandler
{
    /// <inheritdoc />
    public async Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        var lookup = await symbolLookup.ForDocumentAsync(request.TextDocument.Uri, cancellationToken);
        if (lookup.NameRangeAt(request.Position) is not { } range || RenamedElement(lookup, request.Position) is not { } target)
        {
            return null;
        }

        return new RangeOrPlaceholderRange(new PlaceholderRange { Range = range, Placeholder = target.Name });
    }

    /// <inheritdoc />
    public override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        if (!IsName(request.NewName))
        {
            throw Failure($"'{request.NewName}' is not a valid INTERLIS name.");
        }

        var uri = request.TextDocument.Uri;
        var lookups = new Dictionary<DocumentUri, DocumentLookup> { [uri] = await symbolLookup.ForDocumentAsync(uri, cancellationToken) };
        if (RenamedElement(lookups[uri], request.Position) is not { } root)
        {
            throw Failure("There is no element to rename at this position.");
        }

        if (IsDeclaredNextTo(lookups[uri], root, request.NewName))
        {
            throw Failure($"'{request.NewName}' is already declared next to '{root.Name}'.");
        }

        var edits = new Dictionary<DocumentUri, HashSet<Location>>();
        var renamed = new HashSet<string>();
        var pending = new Queue<(DocumentLookup FoundIn, IReferenceTarget Element)>([(lookups[uri], root)]);
        while (pending.TryDequeue(out var item))
        {
            var (foundIn, element) = item;
            if (SymbolLookup.DeclarationKey(element) is not { } key || !renamed.Add(key))
            {
                continue;
            }

            Add(edits, SymbolLookup.DeclarationLocations(element));

            var files = foundIn.DeclaringModels(element)
                .SelectMany(model => workspaceModelIndex.FindFilesUsing(model.Name))
                .Select(file => file.Uri)
                .Append(uri)
                .Distinct();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!lookups.TryGetValue(file, out var lookup))
                {
                    lookups[file] = lookup = await symbolLookup.ForDocumentAsync(file, cancellationToken);
                }

                Add(edits, lookup.Occurrences(element));
                foreach (var dependent in lookup.NamedAfter(element))
                {
                    pending.Enqueue((lookup, dependent));
                }
            }
        }

        return new WorkspaceEdit
        {
            Changes = edits.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Select(location => new TextEdit { Range = location.Range, NewText = request.NewName })),
        };
    }

    /// <inheritdoc />
    protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = textDocumentSelector,
        PrepareProvider = true,
    };

    /// <summary>
    /// The element a rename at <paramref name="position"/> renames: the element named there, or the element it is
    /// named after when it does not name itself. <see langword="null"/> when no element is named there; an error when
    /// the element cannot be renamed because it was never written down (predefined by INTERLIS) or lies in a model
    /// from the repositories, which is not the user's to change.
    /// </summary>
    private IReferenceTarget? RenamedElement(DocumentLookup lookup, Position position)
    {
        if (lookup.FindAt(position) is not { } target)
        {
            return null;
        }

        // Guarded against a cycle of extensions, which the compiler reports but leaves in the AST.
        var visited = new HashSet<IReferenceTarget>();
        while (visited.Add(target) && target is IInterlisDefinition definition && definition.NameSource() is { } source)
        {
            target = source;
        }

        var declaration = SymbolLookup.DeclarationLocations(target).FirstOrDefault();
        if (declaration == null)
        {
            throw Failure($"'{target.Name}' is predefined by INTERLIS and cannot be renamed.");
        }

        if (externalImportFileService.IsExternalFile(declaration.Uri))
        {
            throw Failure($"'{target.Name}' is declared in a model from the model repositories and cannot be renamed.");
        }

        return target;
    }

    /// <summary>
    /// Whether an element is already declared under <paramref name="name"/> next to <paramref name="target"/>: in
    /// the same container, or among the models of the compilation for a model. Only the declared names are checked;
    /// an inherited name the new one would collide with is left to the diagnostics after the rename.
    /// </summary>
    private static bool IsDeclaredNextTo(DocumentLookup lookup, IReferenceTarget target, string name) => target switch
    {
        ModelDef => lookup.Compilation.Environment.Content.ContainsKey(name),
        IInterlisDefinition { Parent: { } parent } => parent.Content.ContainsKey(name),
        _ => false,
    };

    /// <summary>
    /// The error for a request that is well-formed but cannot be served; the client shows its message to the user.
    /// </summary>
    private static RpcErrorException Failure(string message) => new(ErrorCodes.RequestFailed, null!, message);

    /// <summary>
    /// Whether <paramref name="text"/> is one INTERLIS name: the lexer has to see exactly one identifier token, so a
    /// keyword, an embedded separator or a leading digit is rejected by the rules the compiler applies.
    /// </summary>
    private static bool IsName(string text)
    {
        var lexer = new Interlis24Lexer(new AntlrInputStream(text));
        lexer.RemoveErrorListeners();
        var tokens = lexer.GetAllTokens();
        return tokens.Count == 1 && tokens[0].Type == Interlis24Lexer.IDENTIFIER;
    }

    private static void Add(Dictionary<DocumentUri, HashSet<Location>> edits, IEnumerable<Location> locations)
    {
        foreach (var location in locations)
        {
            // A token can declare one element and use another (PROJECTION OF ClassA declares the implicit base and
            // uses the class): both are renamed, but the text is replaced once.
            if (!edits.TryGetValue(location.Uri, out var set))
            {
                edits[location.Uri] = set = [];
            }

            set.Add(location);
        }
    }
}
