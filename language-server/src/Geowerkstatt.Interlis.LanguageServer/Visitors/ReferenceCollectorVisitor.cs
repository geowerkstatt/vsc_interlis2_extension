using Geowerkstatt.Interlis.Compiler.AST;
using System.Diagnostics.CodeAnalysis;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

public record ReferenceDefinition(
    Uri OccurenceFile,
    Position OccurenceStart,
    Position OccurenceEnd,
    Uri TargetFile,
    IInterlisDefinition Target
);

/// <summary>
/// INTERLIS AST visitor to collect all resolved references.
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

        var occurenceUri = GetRootUriForTarget(reference.Source);
        var occurenceLocation = reference.ReferenceLocation;

        var target = reference.Target;
        var targetUri = GetRootUriForTarget(target);

        if (occurenceUri is null
            || occurenceLocation is null
            || target is null
            || targetUri is null)
        {
            return new List<ReferenceDefinition>();
        }

        return new List<ReferenceDefinition> {
            new ReferenceDefinition(
                occurenceUri,
                occurenceLocation.Start.ToOmnisharpPosition(),
                occurenceLocation.End.ToOmnisharpPosition(),
                targetUri,
                target)
        };
    }

    private static Uri? GetRootUriForTarget(IInterlisDefinition? target)
    {
        if (target is null) return null;

        while (target?.Parent != null && target is not ModelDef)
        {
            target = target.Parent;
        }

        var modelDef = target as ModelDef ?? throw new InvalidOperationException("Could not find ModelDef in tree");
        return modelDef.SourceUri is not null ? new Uri(modelDef.SourceUri) : null;
    }
}
