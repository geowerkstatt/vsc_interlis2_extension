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
internal class DiagramDocumentVisitor : Interlis24AstBaseVisitor<object?>
{
    private readonly List<ClassDef> classes = new();
    private readonly List<ClassDef> structures = new();
    private readonly List<AssociationDef> associations = new();
    private readonly StringBuilder mermaidScript = new();

    private readonly ILogger<DiagramDocumentVisitor> logger;

    public DiagramDocumentVisitor(ILogger<DiagramDocumentVisitor> logger)
    {
        this.logger = logger;
        mermaidScript.AppendLine("---");
        mermaidScript.AppendLine("  config:");
        mermaidScript.AppendLine("    class:");
        mermaidScript.AppendLine("      hideEmptyMembersBox: true");
        mermaidScript.AppendLine("---");
        mermaidScript.AppendLine("classDiagram");
        mermaidScript.AppendLine("direction LR");
    }

    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        int headerStart = mermaidScript.Length;
        mermaidScript.AppendLine($"namespace Topic_{topicDef.Name} {{");
        int afterHeader = mermaidScript.Length;
        base.VisitTopicDef(topicDef);
        bool hasContent = mermaidScript.Length > afterHeader;

        if (hasContent)
        {
            mermaidScript.AppendLine("}");
            mermaidScript.AppendLine();
        }
        else
        {
            mermaidScript.Length = headerStart;
        }

        return null;
    }

    public override object? VisitClassDef([NotNull] ClassDef classDef)
    {
        mermaidScript.AppendLine($"  class {classDef.Name}");

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

    private void AppendAttributeDetails(AttributeDef attributeDef)
    {
        if (attributeDef.Parent is ClassDef parentClass)
        {
            var typeString = VisitTypeDefInternal(attributeDef.TypeDef);
            mermaidScript.AppendLine($"{parentClass.Name}: +{typeString} {attributeDef.Name}");
        }
    }


    private void AppendAssociationDetails(AssociationDef associationDef)
    {
        var roles = associationDef.Content.Values.OfType<AttributeDef>().ToList();
        if (roles.Count != 2)
        {
            logger.LogWarning(
                "Skipping association '{Name}' because it has {Count} roles (only binary supported)",
                associationDef.Name, roles.Count
            );
            return;
        }

        var (class1, rawCardinality1) = GetClassAndCardinality(roles[0]);
        var (class2, rawCardinality2) = GetClassAndCardinality(roles[1]);
        if (class1 is null || class2 is null || rawCardinality1 is null || rawCardinality2 is null)
        {
            logger.LogWarning(
                "Skipping association '{Name}' due to missing class or cardinality: " +
                "first=({Class1},{Card1}), second=({Class2},{Card2})",
                associationDef.Name,
                class1?.Name ?? "<null>", rawCardinality1 ?? "<null>",
                class2?.Name ?? "<null>", rawCardinality2 ?? "<null>"
            );
            return;
        }

        // Clean up a raw cardinality (strip whitespace/quotes) and re-quote it with a trailing space for Mermaid syntax
        static string Normalize(string raw) => $"\"{raw.Trim().Trim('\"')}\" ";

        var cardinality1 = Normalize(rawCardinality1);
        var cardinality2 = Normalize(rawCardinality2);

        mermaidScript.AppendLine(
            $"{class1.Name} {cardinality1}--o {cardinality2}{class2.Name} : {associationDef.Name}"
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
        return mermaidScript.ToString();
    }
}
