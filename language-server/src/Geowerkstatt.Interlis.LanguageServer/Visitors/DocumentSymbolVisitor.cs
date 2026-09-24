using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Types;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Diagnostics.CodeAnalysis;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// Builds the document symbol tree (textDocument/documentSymbol) of an INTERLIS environment: one symbol per
/// definition, nested like the definitions, spanning the declaration and selecting its name. Definitions the parser
/// could not give a name or a range (error recovery) contribute their children only.
/// </summary>
internal class DocumentSymbolVisitor : Interlis24AstBaseVisitor<List<DocumentSymbol>>
{
    protected override List<DocumentSymbol>? AggregateResult(List<DocumentSymbol>? aggregate, List<DocumentSymbol>? nextResult)
    {
        if (aggregate is null) return nextResult;
        if (nextResult is null) return aggregate;

        aggregate.AddRange(nextResult);
        return aggregate;
    }

    public override List<DocumentSymbol>? VisitModelDef([NotNull] ModelDef modelDef)
    {
        return modelDef == InternalModel.Interlis ? null : Symbol(modelDef, SymbolKind.Module, base.VisitModelDef(modelDef));
    }

    public override List<DocumentSymbol>? VisitTopicDef([NotNull] TopicDef topicDef) => Symbol(topicDef, SymbolKind.Namespace, base.VisitTopicDef(topicDef));

    public override List<DocumentSymbol>? VisitClassDef([NotNull] ClassDef classDef) => Symbol(classDef, classDef.IsStructure ? SymbolKind.Struct : SymbolKind.Class, base.VisitClassDef(classDef));

    public override List<DocumentSymbol>? VisitAssociationDef([NotNull] AssociationDef associationDef) => Symbol(associationDef, SymbolKind.Interface, base.VisitAssociationDef(associationDef));

    public override List<DocumentSymbol>? VisitViewDef([NotNull] ViewDef viewDef) => Symbol(viewDef, SymbolKind.Object, base.VisitViewDef(viewDef));

    public override List<DocumentSymbol>? VisitAttributeDef([NotNull] AttributeDef attributeDef) => Symbol(attributeDef, attributeDef.TypeDef is RoleType ? SymbolKind.Field : SymbolKind.Property, base.VisitAttributeDef(attributeDef));

    public override List<DocumentSymbol>? VisitDomainDef([NotNull] DomainDef domainDef) => Symbol(domainDef, domainDef.TypeDef is EnumerationType ? SymbolKind.Enum : SymbolKind.TypeParameter, base.VisitDomainDef(domainDef));

    public override List<DocumentSymbol>? VisitUnitDef([NotNull] UnitDef unitDef) => Symbol(unitDef, SymbolKind.Constant, base.VisitUnitDef(unitDef));

    public override List<DocumentSymbol>? VisitFunctionDef([NotNull] FunctionDef functionDef) => Symbol(functionDef, SymbolKind.Function, base.VisitFunctionDef(functionDef));

    public override List<DocumentSymbol>? VisitParameterDef([NotNull] ParameterDef parameterDef) => Symbol(parameterDef, SymbolKind.Variable, base.VisitParameterDef(parameterDef));

    public override List<DocumentSymbol>? VisitGraphicDef([NotNull] GraphicDef graphicDef) => Symbol(graphicDef, SymbolKind.Object, base.VisitGraphicDef(graphicDef));

    public override List<DocumentSymbol>? VisitMetaDataBasketDef([NotNull] MetaDataBasketDef metaDataBasketDef) => Symbol(metaDataBasketDef, SymbolKind.Object, base.VisitMetaDataBasketDef(metaDataBasketDef));

    public override List<DocumentSymbol>? VisitContextDef([NotNull] ContextDef contextDef) => Symbol(contextDef, SymbolKind.Namespace, base.VisitContextDef(contextDef));

    public override List<DocumentSymbol>? VisitLineFormTypeDef([NotNull] LineFormTypeDef lineFormTypeDef) => Symbol(lineFormTypeDef, SymbolKind.TypeParameter, base.VisitLineFormTypeDef(lineFormTypeDef));

    public override List<DocumentSymbol>? VisitConstraintsBlockDef([NotNull] ConstraintsBlockDef constraintsBlockDef) => Symbol(constraintsBlockDef, SymbolKind.Namespace, base.VisitConstraintsBlockDef(constraintsBlockDef));

    public override List<DocumentSymbol>? VisitMandatoryConstraint([NotNull] MandatoryConstraint mandatoryConstraint) => Symbol(mandatoryConstraint, SymbolKind.Event, base.VisitMandatoryConstraint(mandatoryConstraint));

    public override List<DocumentSymbol>? VisitPlausibilityConstraint([NotNull] PlausibilityConstraint plausibilityConstraint) => Symbol(plausibilityConstraint, SymbolKind.Event, base.VisitPlausibilityConstraint(plausibilityConstraint));

    public override List<DocumentSymbol>? VisitExistenceConstraint([NotNull] ExistenceConstraint existenceConstraint) => Symbol(existenceConstraint, SymbolKind.Event, base.VisitExistenceConstraint(existenceConstraint));

    public override List<DocumentSymbol>? VisitUniquenessConstraint([NotNull] UniquenessConstraint uniquenessConstraint) => Symbol(uniquenessConstraint, SymbolKind.Event, base.VisitUniquenessConstraint(uniquenessConstraint));

    public override List<DocumentSymbol>? VisitSetConstraint([NotNull] SetConstraint setConstraint) => Symbol(setConstraint, SymbolKind.Event, base.VisitSetConstraint(setConstraint));

    /// <summary>
    /// The symbol of <paramref name="definition"/> with the given <paramref name="children"/>, or just the children
    /// when the definition has no name or no range to show.
    /// </summary>
    private static List<DocumentSymbol>? Symbol(IInterlisDefinition definition, SymbolKind kind, List<DocumentSymbol>? children)
    {
        if (string.IsNullOrEmpty(definition.Name) || definition.SourceRange is not { } range)
        {
            return children;
        }

        var selection = definition.NameLocations.FirstOrDefault() ?? range;
        return
        [
            new DocumentSymbol
            {
                Name = definition.Name,
                Kind = kind,
                Range = range.ToOmnisharpRange(),
                SelectionRange = selection.ToOmnisharpRange(),
                Children = children is { Count: > 0 } ? new Container<DocumentSymbol>(children) : null,
            },
        ];
    }
}
