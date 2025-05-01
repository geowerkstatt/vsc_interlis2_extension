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

     private void AppendAssociationDetails(AssociationDef associationDef)
    {
        var roles = associationDef.Content.Values.OfType<AttributeDef>().ToList();

        if (roles.Count == 2)
        {
            var (firstClass, firstCardinality) = GetClassAndCardinality(roles[0]);
            var (secondClass, secondCardinality) = GetClassAndCardinality(roles[1]);

            if (firstClass != null && secondClass != null && firstCardinality != null && secondCardinality != null)
            {
                string secondCard = secondCardinality.EndsWith(" ") ? secondCardinality : secondCardinality + " ";
                // Append association outside namespace block
                mermaidScript.AppendLine($"{firstClass.Name} {firstCardinality}--o {secondCard}{secondClass.Name} : {associationDef.Name}");
            }
            else { /* Log warning? */ }
        }
        else { /* Log warning for non-binary? */ }
    }


    private string VisitTypeDefInternal(TypeDef? type)
    {
        if (type == null) return "?";
        return type switch
        {
            ReferenceType rt => rt.Target.Value?.Path.Last() ?? "?",
            TextType tt => tt.Length == null ? "Text" : $"Text [{tt.Length}]",
            NumericType nt => nt.Min != null && nt.Max != null ? $"{nt.Min}..{nt.Max}" : "Numeric",
            BooleanType => "Boolean",
            BlackboxType bt => bt.Kind switch {
                BlackboxType.BlackboxTypeKind.Binary => "Blackbox (Binary)",
                BlackboxType.BlackboxTypeKind.Xml => "Blackbox (XML)",
                _ => "Blackbox",
            },
            EnumerationType et => $"({FormatEnumerationValues(et.Values)})",
            TypeRef tr => tr.Extends?.Path.Last() ?? "?",
            RoleType => "Role",
            _ => type.GetType().Name,
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
        return mermaidScript.ToString();
    }
}
