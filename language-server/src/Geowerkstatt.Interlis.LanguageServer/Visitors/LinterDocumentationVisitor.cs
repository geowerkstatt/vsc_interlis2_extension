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

    public LinterDocumentationVisitor(string? filePath = null)
    {
        _editorConfig = filePath != null ? EditorConfigLoader.Load(filePath) : new Dictionary<string, string>();
        _ruleContext = new LinterRuleContext { EditorConfig = _editorConfig };
    }

    protected override List<Diagnostic>? AggregateResult(List<Diagnostic>? aggregate, List<Diagnostic>? nextResult)
    {
        if (aggregate is null) return nextResult ?? [];
        if (nextResult is null) return aggregate;
        aggregate.AddRange(nextResult);
        return aggregate;
    }

    public override List<Diagnostic> VisitDomainDef(DomainDef? domainDef)
    {
        if (domainDef == null) return [];
        var diagnostics = base.VisitDomainDef(domainDef) ?? [];

        if (domainDef.TypeDef is BooleanType)
        {
            var rule = LinterRules.All.Find(r => r.Id == "interlis.boolean-type");
            if (rule != null && LinterRules.IsRuleEnabled(rule.Id, _ruleContext))
            {
                var description = rule.Description;
                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = description,
                    Range = MapGeoWRangePosition(domainDef.NameLocations),
                });
            }
        }

        return diagnostics;
    }

    public override List<Diagnostic> VisitAttributeDef(AttributeDef? attributeDef)
    {
        if (attributeDef == null) return [];
        var diagnostics = base.VisitAttributeDef(attributeDef) ?? [];

        if (attributeDef.TypeDef is BooleanType)
        {
            var rule = LinterRules.All.Find(r => r.Id == "interlis.boolean-type");
            if (rule != null && LinterRules.IsRuleEnabled(rule.Id, _ruleContext))
            {
                var description = rule.Description;
                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = description,
                    Range = MapGeoWRangePosition(attributeDef.NameLocations),
                });
            }
        }

        return diagnostics;
    }

    private Range MapGeoWRangePosition(ICollection<RangePosition> rangePosition)
    {
        if (rangePosition == null || rangePosition.Count != 1)
        {
            return new Range(new Position(0, 0), new Position(0, 0));
        }

        return new Range(new Position(rangePosition.First().Start.Line, rangePosition.First().Start.Character), new Position(rangePosition.First().End.Line, rangePosition.First().End.Character));
    }
}
