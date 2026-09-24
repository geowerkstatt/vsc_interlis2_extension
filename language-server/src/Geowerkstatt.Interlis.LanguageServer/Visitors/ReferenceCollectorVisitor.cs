using Geowerkstatt.Interlis.Compiler.AST;
using System.Diagnostics.CodeAnalysis;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// One written name that denotes an element: where it is written and what it denotes.
/// </summary>
/// <param name="OccurenceFile">The file the name is written in.</param>
/// <param name="OccurenceStart">The start of the name.</param>
/// <param name="OccurenceEnd">The end of the name.</param>
/// <param name="Target">The element the name denotes: a definition, or a meta object a basket declares.</param>
public record ReferenceDefinition(
    Uri OccurenceFile,
    Position OccurenceStart,
    Position OccurenceEnd,
    IReferenceTarget Target
);

/// <summary>
/// INTERLIS AST visitor to collect every written name that denotes an element: one occurrence per
/// <see cref="PathSegment"/> of a resolved reference rather than one per reference, so that in
/// <c>Model.Topic.ClassA</c> the name <c>Topic</c> is an occurrence of the topic and <c>ClassA</c> one of the
/// class, each with its own span. Navigation from a qualification and a rename that rewrites one name both need
/// that granularity.
/// </summary>
public class ReferenceCollectorVisitor : Interlis24AstBaseVisitor<List<ReferenceDefinition>>
{
    protected override List<ReferenceDefinition>? AggregateResult(List<ReferenceDefinition>? aggregate, List<ReferenceDefinition>? nextResult)
    {
        if (aggregate is null) return nextResult;
        if (nextResult is null) return aggregate;

        aggregate.AddRange(nextResult);
        return aggregate;
    }

    public override List<ReferenceDefinition>? VisitReference<T>([NotNull] Reference<T> reference)
    {
        base.VisitReference(reference);

        var occurrences = new List<ReferenceDefinition>();
        foreach (var name in reference.WrittenNames())
        {
            // A keyword step (THIS, PARENT, ...) has no target, an unresolved name none yet, and a name the source
            // never wrote (INTERLIS.NOOID behind NO OID) no span: none of them is a place to navigate from.
            if (name is { Range: { SourceUri: { } file } range, Target: { } target })
            {
                occurrences.Add(new ReferenceDefinition(new Uri(file), range.Start.ToOmnisharpPosition(), range.End.ToOmnisharpPosition(), target));
            }
        }

        return occurrences;
    }
}
