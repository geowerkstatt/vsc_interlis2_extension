using Geowerkstatt.Interlis.Compiler.AST;
using System.Diagnostics.CodeAnalysis;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors
{
    internal class ReferenceCollectorVisitor : Interlis24AstBaseVisitor<List<(RangePosition position, IInterlisDefinition target)>>
    {
        protected override List<(RangePosition position, IInterlisDefinition target)>? AggregateResult(List<(RangePosition position, IInterlisDefinition target)>? aggregate, List<(RangePosition position, IInterlisDefinition target)>? nextResult)
        {
            if (aggregate is null) return nextResult;
            if (nextResult is null) return aggregate;

            aggregate.AddRange(nextResult);
            return aggregate;
        }

        public override List<(RangePosition position, IInterlisDefinition target)>? VisitReference<T>([NotNull] Reference<T> reference)
        {
            base.VisitReference(reference);
            var location = reference.ReferenceLocation;
            var target = reference.Target;

            if (location is null || target is null) return new List<(RangePosition position, IInterlisDefinition target)>();

            return new List<(RangePosition position, IInterlisDefinition target)> { (location, target) };
        }
    }
}
