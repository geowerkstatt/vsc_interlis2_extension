using Geowerkstatt.Interlis.Compiler.AST;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using System.Diagnostics.CodeAnalysis;
using Geowerkstatt.Interlis.LanguageServer.Services;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors
{
    public record ReferenceDefinition(
        Uri OccurenceFile,
        Position OccurenceStart,
        Position OccurenceEnd,
        Uri? TargetFile,
        IInterlisDefinition Target
    );

    public class ReferenceCollectorVisitor(ExternalImportFileService externalImportFileService) : Interlis24AstBaseVisitor<List<ReferenceDefinition>>
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
            var occurenceUri = GetFileUriFromEnvironment(reference);
            var occurenceLocation = reference.ReferenceLocation;
            var target = reference.Target;

            if (target is null)
            {
                return new List<ReferenceDefinition>();
            }

            var targetUri = GetRootUriForTarget(target);
            if (occurenceUri is null
                ||occurenceLocation is null
                || target is null
                || targetUri is null)
                return new List<ReferenceDefinition>();

            return new List<ReferenceDefinition> {
                new ReferenceDefinition(
                    occurenceUri,                    
                    occurenceLocation.Start.ToOmnisharpPosition(),
                    occurenceLocation.End.ToOmnisharpPosition(),
                    targetUri,
                    target)
            };
        }

        private Uri? GetFileUriFromEnvironment<T>(Reference<T> reference) where T : class, IInterlisDefinition
            => GetRootUriForTarget(reference.Source);

        private Uri? GetRootUriForTarget(IInterlisDefinition? target)
        {
            while (target?.Parent != null && target is not ModelDef)
            {
                target = target.Parent;
            }

            var modelDef = target as ModelDef ?? throw new InvalidOperationException("Could not find ModelDef in tree");

            if (modelDef.SourceUri is null) return null;
            var uri = new Uri(modelDef.SourceUri);

            if (uri.IsFile)
            {
                return uri;
            }
            else
            {
                return externalImportFileService.GetModelUriAsync(modelDef).Result;
            }          
        }
    }
}
