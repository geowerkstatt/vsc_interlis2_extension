using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.LanguageServer.Cache;
using Geowerkstatt.Interlis.LanguageServer.Visitors;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Services;

/// <summary>
/// Answers what a position in a document names and where an element's name is written, for the features that
/// navigate by name (go-to-definition, find-references and rename). An element is any
/// <see cref="IReferenceTarget"/>: a definition, or a meta object a basket declares, which can be pointed at
/// without being a definition.
/// </summary>
internal sealed class SymbolLookup(InterlisEnvironmentCache environmentCache, ReferenceCollectorVisitor referenceCollector)
{
    /// <summary>
    /// Compiles the document (from the cache) and collects the names written in it that denote an element.
    /// </summary>
    /// <param name="uri">The document.</param>
    /// <param name="cancellationToken">Cancels a compilation that has to be computed.</param>
    public async ValueTask<DocumentLookup> ForDocumentAsync(DocumentUri uri, CancellationToken cancellationToken = default)
    {
        var compilation = await environmentCache.GetCompilationAsync(uri, cancellationToken);

        // A compilation also resolves the references of the models it imports, which belong to their own files.
        var references = (referenceCollector.VisitInterlisEnvironment(compilation.Environment) ?? [])
            .Where(reference => DocumentUri.From(reference.OccurenceFile) == uri)
            .ToList();

        return new DocumentLookup(uri, compilation, references);
    }

    /// <summary>
    /// The places the name of <paramref name="target"/> is written in its own declaration, in the file declaring it.
    /// An INTERLIS declaration normally names itself twice, once where it opens and once after its <c>END</c>; the
    /// opening name comes first. Empty for an element that was never written down (the predefined
    /// <c>INTERLIS</c> model).
    /// </summary>
    /// <param name="target">The declared element.</param>
    public static IEnumerable<Location> DeclarationLocations(IReferenceTarget target)
    {
        return target.NameLocations
            .Where(name => name.SourceUri != null)
            .Select(name => new Location { Uri = DocumentUri.From(name.SourceUri!), Range = name.ToOmnisharpRange() });
    }

    /// <summary>
    /// Identifies an element across compilations by where its name is declared (file and span). Every compilation
    /// parses the models it imports again, so the same element is a different object in each of them and cannot be
    /// matched by identity; and a meta object has no fully qualified name to match by. <see langword="null"/> for an
    /// element never written down.
    /// </summary>
    /// <param name="target">The element.</param>
    public static string? DeclarationKey(IReferenceTarget target)
        => target.NameLocations.FirstOrDefault() is { SourceUri: { } uri } name ? $"{uri}#{name}" : null;
}

/// <summary>
/// What one document contributes to a name lookup: its compilation and the names written in the document itself. A
/// compilation is rooted at one document, so it is the only place where that document's references are resolved;
/// the uses of an element in other files have to be looked up in their own compilations.
/// </summary>
/// <param name="Uri">The document.</param>
/// <param name="Compilation">Its compilation, including the models it imports.</param>
/// <param name="References">
/// The names written in the document itself that denote an element, one per name: in <c>Model.Topic.ClassA</c>
/// the model, the topic and the class are three entries with their own spans (see <see cref="ReferenceCollectorVisitor"/>).
/// </param>
internal sealed record DocumentLookup(DocumentUri Uri, Compilation Compilation, IReadOnlyList<ReferenceDefinition> References)
{
    /// <summary>
    /// The element the <paramref name="position"/> names: the element denoted by the name written there — in
    /// <c>Model.Topic.ClassA</c> the topic while the position is on <c>Topic</c>, the class once it is on
    /// <c>ClassA</c> — or, when the position is on a declaration instead, the declared element. <see langword="null"/>
    /// when it names neither, including on the separators between the names of a path.
    /// </summary>
    /// <remarks>
    /// The declarations searched are those reachable through the containers and constraints of the document's own
    /// models, the base names of a view and the meta objects of a basket included. An implicit base name
    /// (<c>PROJECTION OF ClassA</c>) is declared by the same token that references the class, where the reference
    /// wins here (see <see cref="DeclaredAt"/>). The elements of an enumeration cannot be found, because the AST does
    /// not model them as referenceable definitions.
    /// </remarks>
    /// <param name="position">The position in the document.</param>
    public IReferenceTarget? FindAt(Position position) => ReferencedAt(position) ?? DeclaredAt(position);

    /// <summary>
    /// The element denoted by the name written at <paramref name="position"/>, or <see langword="null"/> when no
    /// resolved name is written there.
    /// </summary>
    /// <param name="position">The position in the document.</param>
    public IReferenceTarget? ReferencedAt(Position position)
        => References.FirstOrDefault(r => r.OccurenceStart <= position && r.OccurenceEnd >= position)?.Target;

    /// <summary>
    /// The element whose declaration writes its name at <paramref name="position"/>, or <see langword="null"/>. A
    /// position can reference one element and declare another at once: the token of <c>PROJECTION OF ClassA</c>
    /// references the class and declares the view's base name. Go-to-definition follows the reference
    /// (<see cref="FindAt"/>); find-references starts from the declaration, so that from the base's declaration the
    /// base's uses are listed rather than the class's.
    /// </summary>
    /// <param name="position">The position in the document.</param>
    public IReferenceTarget? DeclaredAt(Position position)
        => Declared().FirstOrDefault(target => target.NameLocations.Any(name => Contains(name, position)));

    /// <summary>
    /// The span of the name written at <paramref name="position"/>, be it a use or a declaration, or
    /// <see langword="null"/> when no name is written there: the text a rename started there replaces.
    /// </summary>
    /// <param name="position">The position in the document.</param>
    public Range? NameRangeAt(Position position)
    {
        if (References.FirstOrDefault(r => r.OccurenceStart <= position && r.OccurenceEnd >= position) is { } reference)
        {
            return new Range(reference.OccurenceStart, reference.OccurenceEnd);
        }

        return Declared().SelectMany(target => target.NameLocations).FirstOrDefault(name => Contains(name, position))?.ToOmnisharpRange();
    }

    /// <summary>
    /// The elements this document declares that take their name from <paramref name="target"/> instead of choosing
    /// their own (see <see cref="AstExtensions.NameSource"/>): the implicit base names of views over it and the
    /// <c>EXTENDED</c> redefinitions of it. They have to follow when the element is renamed. Matched by
    /// <see cref="SymbolLookup.DeclarationKey"/>, because the element is a different object in every compilation.
    /// </summary>
    /// <param name="target">The element.</param>
    public IEnumerable<IInterlisDefinition> NamedAfter(IReferenceTarget target)
    {
        var key = SymbolLookup.DeclarationKey(target);
        return key == null
            ? []
            : Declared().OfType<IInterlisDefinition>().Where(definition => definition.NameSource() is { } source && SymbolLookup.DeclarationKey(source) == key);
    }

    /// <summary>
    /// The models of the file that declares <paramref name="target"/>, as this document's compilation knows them:
    /// the files that can name the element are those defining or importing one of these models.
    /// </summary>
    /// <param name="target">The element.</param>
    public IEnumerable<ModelDef> DeclaringModels(IReferenceTarget target)
    {
        var declaringFile = target.NameLocations.FirstOrDefault()?.SourceUri;
        return declaringFile == null ? [] : Compilation.Environment.Content.Values.Where(model => model.SourceUri == declaringFile);
    }

    /// <summary>
    /// The places this document writes the name of <paramref name="target"/>: where it is used, where it qualifies
    /// another name (<c>Topic</c> in <c>Model.Topic.ClassA</c>) and where it qualifies a role (<c>Assoc</c> in
    /// <c>Role[Assoc]</c>), each as the span of that one name. Matched by <see cref="SymbolLookup.DeclarationKey"/>,
    /// because the element is a different object in every compilation.
    /// </summary>
    /// <param name="target">The element.</param>
    public IEnumerable<Location> Occurrences(IReferenceTarget target)
    {
        var key = SymbolLookup.DeclarationKey(target);
        return key == null
            ? []
            : References
                .Where(reference => SymbolLookup.DeclarationKey(reference.Target) == key)
                .Select(reference => new Location { Uri = Uri, Range = new Range(reference.OccurenceStart, reference.OccurenceEnd) });
    }

    /// <summary>The elements the document's own models declare (see <see cref="AstExtensions.ReferenceTargets"/>).</summary>
    private IEnumerable<IReferenceTarget> Declared()
        => Compilation.ForDocument(Uri).Content.Values.SelectMany(model => model.ReferenceTargets());

    private static bool Contains(RangePosition range, Position position)
        => range.Start.ToOmnisharpPosition() <= position && range.End.ToOmnisharpPosition() >= position;
}
