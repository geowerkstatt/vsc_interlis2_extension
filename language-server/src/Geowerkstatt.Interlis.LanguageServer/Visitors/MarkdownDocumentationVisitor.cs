using Geowerkstatt.Interlis.Tools.AST;
using Geowerkstatt.Interlis.Tools.AST.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor to generate markdown documentation.
/// </summary>
public class MarkdownDocumentationVisitor : Interlis24AstBaseVisitor<object>
{
    private readonly StringBuilder documentation = new StringBuilder();
    private bool useHtml;

    /// <summary>
    /// Generates markdown documentation for the given model.
    /// </summary>
    /// <param name="modelDef">The INTERLIS model.</param>
    public override object? VisitModelDef([NotNull] ModelDef modelDef)
    {
        documentation.AppendLine($"# {modelDef.Name}");
        return base.VisitModelDef(modelDef);
    }

    /// <summary>
    /// Generates markdown documentation for the given topic.
    /// </summary>
    /// <param name="modelDef">The INTERLIS topic.</param>
    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        documentation.AppendLine($"## {topicDef.Name}");
        return base.VisitTopicDef(topicDef);
    }

    /// <summary>
    /// Generates markdown documentation for the given class.
    /// Lists all attributes and associations of the class as a table.
    /// </summary>
    /// <param name="modelDef">The INTERLIS class.</param>
    public override object? VisitClassDef([NotNull] ClassDef classDef)
    {
        void VisitTableBody()
        {
            base.VisitClassDef(classDef);
            VisitRelatedAssociations(classDef);
        }

        if (useHtml)
        {
            documentation.Append("<table>");
            documentation.Append("<thead><tr><th>Attributname</th><th>Kardinalität</th><th>Typ</th></tr></thead>");
            documentation.Append("<tbody>");
            VisitTableBody();
            documentation.Append("</tbody></table>");
        }
        else
        {
            documentation.AppendLine($"### {classDef.Name}");
            documentation.AppendLine("| Attributname | Kardinalität | Typ |");
            documentation.AppendLine("| --- | --- | --- |");
            VisitTableBody();
            documentation.AppendLine();
        }

        return null;
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

    /// <summary>
    /// Generates a markdown table row for the given attribute.
    /// </summary>
    /// <param name="attributeDef">The attribute.</param>
    public override object? VisitAttributeDef([NotNull] AttributeDef attributeDef)
    {
        var cardinality = CalculateCardinality(attributeDef.TypeDef.Cardinality);

        if (useHtml)
        {
            documentation.Append($"<tr><td>{attributeDef.Name}</td><td>{cardinality}</td><td>");
            VisitTypeName(attributeDef.TypeDef);
            documentation.Append("</td></tr>");
        }
        else
        {
            documentation.Append($"| {attributeDef.Name} | {cardinality} | ");
            VisitTypeName(attributeDef.TypeDef);
            documentation.AppendLine(" |");
        }

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

    private void VisitTypeName(TypeDef? type)
    {
        if (type is ReferenceType referenceType)
        {
            VisitReferenceType(referenceType);
            return;
        }

        var typeName = type switch
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
            EnumerationType enumerationType => FormatEnumerationValues(enumerationType.Values),
            TypeRef typeRef => typeRef.Extends?.Path.Last(),
            RoleType roleType => string.Join(", ", roleType.Targets.Select(target => target.Value?.Path.Last()).Where(target => target is not null)),
            _ => type?.ToString(),
        };
        documentation.Append(typeName);
    }

    private static string FormatEnumerationValues(EnumerationValuesList enumerationValues, int depth = 0)
    {
        var (formatStart, formatEnd) = depth switch
        {
            0 => ("<b>", "</b>"),
            1 => ("", ""),
            _ => ("<i>", "</i>"),
        };
        var formattedValues = enumerationValues.Select(v => $"{formatStart}{v.Name}{formatEnd}{(v.SubValues.Count == 0 ? "" : " " + FormatEnumerationValues(v.SubValues, depth + 1))}");
        return $"({string.Join(", ", formattedValues)})";
    }

    /// <summary>
    /// Appends the name of the referenced type to the documentation.
    /// If the referenced type is a structure, its attributes and associations are also documented using an HTML table.
    /// </summary>
    /// <param name="referenceType">The referenced type.</param>
    private void VisitReferenceType(ReferenceType referenceType)
    {
        var reference = referenceType.Target.Value;
        var typeName = reference?.Path.Last();
        documentation.Append(typeName);

        if (reference?.Target is ClassDef classDef && classDef.IsStructure)
        {
            documentation.Append("<br/>");

            var didUseHtml = useHtml;
            useHtml = true;
            VisitClassDef(classDef);
            useHtml = didUseHtml;
        }
    }

    /// <summary>
    /// Returns the generated markdown documentation of the visited elements.
    /// </summary>
    /// <returns>The generated documentation.</returns>
    public string GetDocumentation()
    {
        return documentation.ToString();
    }
}
