using Geowerkstatt.Interlis.Tools.AST;
using Geowerkstatt.Interlis.Tools.AST.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor to generate React Flow syntax script.
/// </summary>
internal class ReactFlowVisitor : Interlis24AstBaseVisitor<object?>
{
    private readonly List<ClassDef> classes = new();
    private readonly List<ClassDef> structures = new();
    private readonly List<AssociationDef> associations = new();
    private readonly object reactFlowObject = new();
    private readonly ILogger<ReactFlowVisitor> logger;

    public ReactFlowVisitor(ILogger<ReactFlowVisitor> logger)
    {
        this.logger = logger;
    }

    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        base.VisitTopicDef(topicDef);
        return null;
    }

    public override object? VisitClassDef([NotNull] ClassDef classDef)
    {
        if (classDef.IsStructure)
            structures.Add(classDef);
        else
            classes.Add(classDef);

        return DefaultResult;
    }

    public override object? VisitAssociationDef([NotNull] AssociationDef associationDef)
    {
        associations.Add(associationDef);
        return DefaultResult;
    }

    private void AppendAttributeDetailsToScript(ClassDef parentClass, AttributeDef attributeDef, Node nodeToAdd)
    {
        nodeToAdd.Data.Attributes.Add(attributeDef.Name);
    }

    private void AppendAssociationDetailsToScript(AssociationDef associationDef, List<Edge> edges)
    {
        var roles = associationDef.Content.Values
            .OfType<AttributeDef>()
            .Where(attribute => attribute.TypeDef is RoleType)
            .ToList();
        if (roles.Count != 2)
        {
            logger.LogWarning("Skip '{Name}' – needs exactly 2 role ends", associationDef.Name);
            return;
        }

        (ClassDef? classDef, string? cardinalityString, Cardinality.RelationshipType relType) MapRole(
            AttributeDef attribute)
        {
            var (classDef, cardinalityString) = GetClassAndCardinality(attribute);
            var roleType = ((RoleType)attribute.TypeDef!).Cardinality!.Type;
            return (classDef, cardinalityString, roleType);
        }

        var left = MapRole(roles[0]);
        var right = MapRole(roles[1]);

        if (left.classDef == null || right.classDef == null || left.cardinalityString == null ||
            right.cardinalityString == null)
        {
            logger.LogWarning("Skip association '{Name}' – unresolved role target or cardinality",
                associationDef.Name);
            return;
        }

      
        else if (string.Compare(left.classDef.Name, right.classDef.Name, StringComparison.Ordinal) > 0)
        {
            (left, right) = (right, left);
        }

        edges.Add(new Edge
        {
            Id = $"{left.classDef.Name}-{right.classDef.Name}",
            Source = left.classDef.Name,
            Target = right.classDef.Name
        });
    }

    private static string FormatNumericType(NumericType numericType)
    {
        string text;
        if (numericType.Min != null && numericType.Max != null)
        {
            string minStr = numericType.Min.ToString()!;
            string maxStr = numericType.Max.ToString()!;
            text = $"{minStr}..{maxStr}";
        }
        else
        {
            text = "Numeric";
        }

        var unitName = numericType.Unit?.Target?.Name ?? numericType.Unit?.Path.LastOrDefault();
        if (!string.IsNullOrEmpty(unitName))
            text += $" [{unitName}]";

        return text;
    }



    private static string FormatCardinality(Cardinality? cardinality)
    {
        if (cardinality is null)
        {
            return "\"*\" ";
        }

        var min = cardinality.Min?.ToString() ?? "*";
        var max = cardinality.Max?.ToString() ?? "*";
        return $"\"{(min == max ? min : $"{min}..{max}")}\" ";
    }

    private static (ClassDef? cls, string? card) GetClassAndCardinality(AttributeDef? attribute)
    {
        if (attribute?.TypeDef is not RoleType roleType)
        {
            return (null, null);
        }

        var classDef = roleType.Targets.FirstOrDefault()?.Value?.Target as ClassDef;
        var cardinality = FormatCardinality(roleType.Cardinality);
        return (classDef, cardinality);
    }

    public ReactflowResponse GetDiagramDocument()
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        foreach (var type in classes.Concat(structures))
        {
            var nodeToAdd = new Node();
        

            nodeToAdd.Id= type.Name;
            nodeToAdd.Data = new NodeData() { Title = type.Name };
            

            if (type.MetaAttributes.TryGetValue("geow.uml.color", out var color) && !string.IsNullOrWhiteSpace(color))
            {
                nodeToAdd.Style = new NodeStyle()
                {
                    Background = color,
                    Color = "black",
                    Border = "2px solid black"
                };
            }

            var stereo = type.IsStructure ? MermaidConstants.StructureStereotype : MermaidConstants.ClassStereotype;
            // mermaidScript.AppendLine($"{type.Name}: {stereo}");

            foreach (var attr in type.Content.Values.OfType<AttributeDef>())
                AppendAttributeDetailsToScript(type, attr, nodeToAdd);

            // mermaidScript.AppendLine();
            nodes.Add(nodeToAdd);
        }

        foreach (var associationDef in associations)
            AppendAssociationDetailsToScript(associationDef, edges);

        return new ReactflowResponse() {
            Nodes = nodes,
            Edges = edges,
        };
    }

    internal static class MermaidConstants
    {
        public const string ClassStereotype = "**#60;#60;CLASS#62;#62;**#8203;";
        public const string StructureStereotype = "**#60;#60;STRUCTURE#62;#62;**#8203;";
        public const string Colon = "#colon;";
        public const string LeftParenthesis = "#40;";
        public const string RightParenthesis = "#41;";
    }
}
