using Geowerkstatt.Interlis.Tools.AST;
using Geowerkstatt.Interlis.Tools.AST.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

public class MarkdownDocumentationVisitor : Interlis24AstBaseVisitor<object>
{
    private readonly StringBuilder documentation = new StringBuilder();

    public override object? VisitModelDef([NotNull] ModelDef modelDef)
    {
        documentation.AppendLine($"# {modelDef.Name}");
        return base.VisitModelDef(modelDef);
    }

    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        documentation.AppendLine($"## {topicDef.Name}");
        return base.VisitTopicDef(topicDef);
    }

    public override object? VisitClassDef([NotNull] ClassDef classDef)
    {
        documentation.AppendLine($"### {classDef.Name}");
        documentation.AppendLine("| Attributname | Kardinalität | Typ |");
        documentation.AppendLine("| --- | --- | --- |");
        var result = base.VisitClassDef(classDef);
        VisitRelatedAssociations(classDef);
        documentation.AppendLine();

        return result;
    }

    private void VisitRelatedAssociations(ClassDef classDef)
    {
        var associations = classDef.Parent?.Content.Values.OfType<AssociationDef>() ?? [];
        foreach (var association in associations)
        {
            var left = association.Content.Values.FirstOrDefault();
            var right = association.Content.Values.LastOrDefault();
            if (left is AttributeDef leftAttribute && right is AttributeDef rightAttribute)
            {
                VisitRelatedAssociation(classDef, leftAttribute, rightAttribute);
                VisitRelatedAssociation(classDef, rightAttribute, leftAttribute);
            }
        }
    }

    private void VisitRelatedAssociation(ClassDef classDef, AttributeDef left, AttributeDef right)
    {
        if (left?.TypeDef is RoleType leftRoleType)
        {
            var leftClass = leftRoleType.Targets.FirstOrDefault()?.Value?.Target as ClassDef;
            if (leftClass == classDef)
            {
                VisitAttributeDef(right);
            }
        }
    }

    public override object? VisitAttributeDef([NotNull] AttributeDef attributeDef)
    {
        var cardinality = CalculateCardinality(attributeDef.TypeDef.Cardinality);
        var type = GetTypeName(attributeDef.TypeDef);

        documentation.AppendLine($"| {attributeDef.Name} | {cardinality} | {type} |");
        return base.VisitAttributeDef(attributeDef);
    }

    public override object? VisitAssociationDef([NotNull] AssociationDef associationDef)
    {
        // Skip associations, they are handled in VisitClassDef
        return null;
    }

    private static string CalculateCardinality(Cardinality? cardinality)
    {
        if (cardinality != null)
        {
            var min = cardinality.Min?.ToString() ?? "n";
            var max = cardinality.Max?.ToString() ?? "n";
            return min == max ? min : $"{min}..{max}";
        }

        return "";
    }

    private static string? GetTypeName(TypeDef? type)
    {
        return type switch
        {
            TextType textType => textType.Length == null ? "Text" : $"Text [{textType.Length}]",
            NumericType numericType => numericType.Min != null && numericType.Max != null ? $"{numericType.Min}..{numericType.Max}" : "Numerisch",
            BooleanType => "Boolean",
            BlackboxType blackboxType => blackboxType.Kind switch
            {
                BlackboxType.BlackboxTypeKind.Binary => "Blackbox (Binär)",
                BlackboxType.BlackboxTypeKind.Xml => "Blackbox (XML)",
                _ => "Blackbox",
            },
            EnumerationType enumerationType => $"({string.Join(", ", enumerationType.Values.Select(v => v.Name))})",
            ReferenceType referenceType => referenceType.Target.Value?.Path.Last(),
            TypeRef typeRef => typeRef.Extends?.Path.Last(),
            RoleType roleType => string.Join(", ", roleType.Targets.Select(target => target.Value?.Path.Last()).Where(target => target is not null)),
            _ => type?.ToString(),
        };
    }

    public string GetDocumentation()
    {
        return documentation.ToString();
    }
}
