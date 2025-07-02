using Geowerkstatt.Interlis.Compiler.AST;
using System.Diagnostics.CodeAnalysis;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor to collect the names of the imported models.
/// </summary>
public class ModelImportVisitor : Interlis24AstBaseVisitor<ISet<string>>
{
    protected override ISet<string>? AggregateResult(ISet<string>? aggregate, ISet<string>? nextResult)
    {
        if (aggregate == null) return nextResult;
        if (nextResult == null) return aggregate;

        aggregate.UnionWith(nextResult);
        return aggregate;
    }

    public override ISet<string>? VisitModelDef([NotNull] ModelDef modelDef)
    {
        return modelDef.Imports.Values.Select(import => import.ModelDef.Path[0]).ToHashSet();
    }
}
