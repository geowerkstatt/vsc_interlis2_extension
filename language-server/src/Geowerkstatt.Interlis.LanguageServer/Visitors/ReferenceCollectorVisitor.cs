using Geowerkstatt.Interlis.Compiler.AST;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using System.Diagnostics.CodeAnalysis;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors
{
    public record ReferenceDefinition(
        Position Start,
        Position End,
        IInterlisDefinition Target
    );

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
            var location = reference.ReferenceLocation;
            var target = reference.Target;

            if (location is null || target is null) return new List<ReferenceDefinition>();

            return new List<ReferenceDefinition> {
                new ReferenceDefinition(location.Start.ToOmnisharpPosition(), location.End.ToOmnisharpPosition(), target)
            };
        }
    }
}
