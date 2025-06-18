using System.Diagnostics.CodeAnalysis;
using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Types;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

internal class LinterDocumentationVisitor : Interlis24AstBaseVisitor<List<Diagnostic>>
{
    private readonly Dictionary<string, string> _editorConfig;
    private readonly LinterRuleContext _ruleContext;

    public LinterDocumentationVisitor()
    {
        _editorConfig = EditorConfigLoader.LoadFromWorkspace();
        _ruleContext = new LinterRuleContext { EditorConfig = _editorConfig };
    }

    protected override List<Diagnostic>? AggregateResult(List<Diagnostic>? aggregate, List<Diagnostic>? nextResult)
    {
        if (aggregate is null) return nextResult ?? [];
        if (nextResult is null) return aggregate;
        aggregate.AddRange(nextResult);
        return aggregate;
    }

    public override List<Diagnostic> VisitDomainDef([NotNull] DomainDef domainDef)
    {
        var diagnostics = base.VisitDomainDef(domainDef) ?? [];
        VisitTypeDef(diagnostics, domainDef.TypeDef);
        return diagnostics;
    }

    public override List<Diagnostic> VisitAttributeDef([NotNull] AttributeDef attributeDef)
    {
        var diagnostics = base.VisitAttributeDef(attributeDef) ?? [];
        VisitTypeDef(diagnostics, attributeDef.TypeDef);
        return diagnostics;
    }

    private List<Diagnostic> VisitTypeDef(List<Diagnostic> diagnostics, TypeDef typeDef)
    {
        if (typeDef is BooleanType)
        {
            var rule = LinterRules.All.Find(r => r.Id == "interlis.boolean-type");
            if (rule != null && LinterRules.IsRuleEnabled(rule.Id, _ruleContext))
            {
                var description = rule.Description;
                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = description,
                    Range = MapGeoWRangePosition(typeDef.SourceRange),
                    Code = rule.Id,
                });
            }
        }

        return diagnostics;
    }

    private Range MapGeoWRangePosition(RangePosition? rangePosition)
    {
        if (rangePosition == null)
        {
            return new Range(new Position(0, 0), new Position(0, 0));
        }

        return new Range(new Position(rangePosition.Start.Line, rangePosition.Start.Character), new Position(rangePosition.End.Line, rangePosition.End.Character));
    }
}
