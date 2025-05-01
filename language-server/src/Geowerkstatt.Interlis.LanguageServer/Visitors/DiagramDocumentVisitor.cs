using Geowerkstatt.Interlis.Tools.AST;
using Geowerkstatt.Interlis.Tools.AST.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor to generate a Mermaid class diagram script.
/// Follows a two-pass approach within topics to handle Mermaid namespace limitations.
/// </summary>
public class DiagramDocumentVisitor : Interlis24AstBaseVisitor<object?>
{
    private readonly StringBuilder _mermaidScript = new();
    private readonly List<ClassDef> _classes = new();
    private readonly List<AssociationDef> _associations = new();
    private readonly ILogger<DiagramDocumentVisitor> _logger;

    private static readonly Dictionary<(long? Min, long? Max), string> MermaidCardinalityMap = new()
    {
        { (0, 1), "\"0..1\" " },
        { (1, 1), "\"1\" " },
        { (0, null), "\"*\"" },
        { (1, null), "\"1..*\" " }
    };

    public DiagramDocumentVisitor(ILogger<DiagramDocumentVisitor> logger)
    {
        _logger = logger;
        _mermaidScript.AppendLine("classDiagram");
        _mermaidScript.AppendLine("direction LR");
    }

    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        _mermaidScript.AppendLine($"namespace Topic_{topicDef.Name} {{");
        base.VisitTopicDef(topicDef);
        _mermaidScript.AppendLine("}");
        _mermaidScript.AppendLine();

        foreach (var classDefs in _classes)
        {
            if (classDefs.Extends?.Target?.Name is not null and var parent)
                _mermaidScript.AppendLine($"{classDefs.Name} --|> {parent}");

            if (classDefs.MetaAttributes.TryGetValue("geow.uml.color", out var color) &&
                !string.IsNullOrWhiteSpace(color))
            {
                _mermaidScript.AppendLine($"style {classDefs.Name} fill:{color},color:black,stroke:black");
            }

            foreach (var attr in classDefs.Content.Values.OfType<AttributeDef>())
                AppendAttributeDetails(attr);

            _mermaidScript.AppendLine();
        }

        foreach (var associationDef in _associations)
            AppendAssociationDetails(associationDef);

        _mermaidScript.AppendLine();

        _classes.Clear();
        _associations.Clear();

        return null;
    }

    public override object? VisitClassDef([NotNull] ClassDef classDef)
    {
        _mermaidScript.AppendLine($"  class {classDef.Name}");
        _classes.Add(classDef);

        return base.VisitClassDef(classDef);
    }

    public override object? VisitAssociationDef([NotNull] AssociationDef associationDef)
    {
        _associations.Add(associationDef);
        return base.VisitAssociationDef(associationDef);
    }

    private void AppendAttributeDetails(AttributeDef attributeDef)
    {
        if (attributeDef.Parent is ClassDef parentClass)
        {
            var typeString = VisitTypeDefInternal(attributeDef.TypeDef);
            _mermaidScript.AppendLine($"{parentClass.Name}: +{typeString} {attributeDef.Name}");
        }
    }


    private void AppendAssociationDetails(AssociationDef assoc)
    {
        var roles = assoc.Content.Values.OfType<AttributeDef>().ToList();
        if (roles.Count != 2)
        {
            _logger.LogWarning(
                "Skipping association '{Name}' because it has {Count} roles (only binary supported)",
                assoc.Name, roles.Count
            );
            return;
        }

        var (c1, rawCard1) = GetClassAndCardinality(roles[0]);
        var (c2, rawCard2) = GetClassAndCardinality(roles[1]);
        if (c1 is null || c2 is null || rawCard1 is null || rawCard2 is null)
        {
            _logger.LogWarning(
                "Skipping association '{Name}' due to missing class or cardinality: " +
                "first=({Class1},{Card1}), second=({Class2},{Card2})",
                assoc.Name,
                c1?.Name  ?? "<null>", rawCard1 ?? "<null>",
                c2?.Name  ?? "<null>", rawCard2 ?? "<null>"
            );
            return;
        }

        static string Normalize(string raw) => $"\"{raw.Trim().Trim('\"')}\" ";

        var card1 = Normalize(rawCard1);
        var card2 = Normalize(rawCard2);

        _mermaidScript.AppendLine(
            $"{c1.Name} {card1}--o {card2}{c2.Name} : {assoc.Name}"
        );
    }

    private string VisitTypeDefInternal(TypeDef? type)
    {
        if (type == null) return "?";
        return type switch
        {
            ReferenceType rt => rt.Target.Value?.Path.Last() ?? "?",
            TextType tt => tt.Length == null ? "Text" : $"Text [{tt.Length}]",
            NumericType nt => nt is { Min: not null, Max: not null } ? $"{nt.Min}..{nt.Max}" : "Numeric",
            BooleanType => "Boolean",
            BlackboxType bt => bt.Kind switch
            {
                BlackboxType.BlackboxTypeKind.Binary => "Blackbox (Binary)",
                BlackboxType.BlackboxTypeKind.Xml => "Blackbox (XML)",
                _ => "Blackbox"
            },
            EnumerationType et => $"({FormatEnumerationValues(et.Values)})",
            TypeRef tr => tr.Extends?.Path.Last() ?? "?",
            RoleType => "Role",
            _ => type.GetType().Name
        };
    }

    private static string FormatEnumerationValues(EnumerationValuesList enumerationValues)
    {
        return string.Join(", ", enumerationValues.Select(v => v.Name));
    }

    private static (ClassDef? classDef, string? Cardinality) GetClassAndCardinality(AttributeDef? attribute)
    {
        if (attribute?.TypeDef is not RoleType roleType || roleType.Cardinality is null)
            return (null, null);
        var classDef = roleType.Targets.FirstOrDefault()?.Value?.Target as ClassDef;
        var cardinalityTuple = (roleType.Cardinality.Min, roleType.Cardinality.Max);
        MermaidCardinalityMap.TryGetValue(cardinalityTuple, out string? cardinality);
        return (classDef, cardinality);
    }

    public string GetDiagramDocument()
    {
        return _mermaidScript.ToString();
    }
}
