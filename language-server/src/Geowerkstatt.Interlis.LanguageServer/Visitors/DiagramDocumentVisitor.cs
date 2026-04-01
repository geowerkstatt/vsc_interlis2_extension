using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Types;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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

    public DiagramDocumentVisitor(ILogger<DiagramDocumentVisitor> logger, String orientation)
    {
        this.logger = logger;
        mermaidScript.AppendLine("---");
        mermaidScript.AppendLine("  config:");
        mermaidScript.AppendLine("    class:");
        mermaidScript.AppendLine("      hideEmptyMembersBox: true");
        mermaidScript.AppendLine("---");
        mermaidScript.AppendLine("classDiagram");
        mermaidScript.AppendLine("direction " + orientation);
    }

    public override object? VisitModelDef([NotNull] ModelDef modelDef)
    {
        if (modelDef == InternalModel.Interlis)
        {
            return null;
        }

        return base.VisitModelDef(modelDef);
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
        var stereotypes = new List<string>();

        if (classDef.Properties.Contains(Property.Abstract))
        {
            stereotypes.Add("<<abstract>>");
        }

        if (classDef.IsStructure)
        {
            stereotypes.Add("<<structure>>");
        }

        if (classDef.Properties.Contains(Property.External))
            stereotypes.Add("<<external>>");

        // If we have stereotypes, use the nested syntax
        if (stereotypes.Any())
        {
            mermaidScript.AppendLine($"  class {classDef.Name}{{");
            foreach (var stereotype in stereotypes)
            {
                mermaidScript.AppendLine($"    {stereotype}");
            }
            mermaidScript.AppendLine("  }");
        }
        else
        {
            mermaidScript.AppendLine($"  class {classDef.Name}");
        }

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

    private void AppendAttributeDetailsToScript(ClassDef parentClass, AttributeDef attributeDef)
    {
        string bracket = "";
        var card = attributeDef.TypeDef?.Cardinality;
        if (card != null)
        {
            var min = card.Min ?? 0;
            var maxStr = card.Max?.ToString() ?? "*";

            if (card.Min == 1 && card.Max == 1)
            {
                bracket = "";
            }
            else if (card.Min == card.Max)
            {
                bracket = $"[{min}]";
            }
            else
            {
                bracket = $"[{min}..{maxStr}]";
            }
        }

        var typeName = VisitTypeDefInternal(attributeDef.TypeDef);
        mermaidScript.AppendLine(
            $"{parentClass.Name}: {attributeDef.Name}{bracket} {MermaidConstants.Colon} **{typeName}**#8203;"
        );
    }

    private void AppendAssociationDetailsToScript(AssociationDef associationDef)
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

        bool leftDiamond =
            left.relType is Cardinality.RelationshipType.Aggregation or Cardinality.RelationshipType.Composition;
        bool rightDiamond =
            right.relType is Cardinality.RelationshipType.Aggregation or Cardinality.RelationshipType.Composition;

        if (leftDiamond && !rightDiamond)
        {
            (left, right) = (right, left);
            (leftDiamond, rightDiamond) = (rightDiamond, leftDiamond);
        }
        else if (!leftDiamond && !rightDiamond &&
                 string.Compare(left.classDef.Name, right.classDef.Name, StringComparison.Ordinal) > 0)
        {
            (left, right) = (right, left);
        }

        string symbol = (leftDiamond, rightDiamond) switch
        {
            (false, false) => "--",
            (true, false) => "o--",
            (false, true) => "--o",
            _ => "o--o"
        };

        var extras = associationDef.Content.Values
            .OfType<AttributeDef>()
            .Where(a => a.TypeDef is not RoleType)
            .Select(a => a.Name);

        var label = extras.Any()
            ? $"{associationDef.Name} ({string.Join(",", extras)})"
            : associationDef.Name;

        mermaidScript.AppendLine(
            $"{left.classDef.Name} {left.cardinalityString} {symbol} {right.cardinalityString}{right.classDef.Name} : {label}"
        );
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

    private string VisitTypeDefInternal(TypeDef? type)
    {
        if (type == null) return "?";
        return type switch
        {
            ReferenceType rt => rt.Target.Value?.Path.Last() ?? "?",
            TextType tt => tt.Length is { } len ? $"Text[{len}]" : "Text",
            NumericType nt => FormatNumericType(nt),
            BooleanType => "Boolean",
            BlackboxType bt => bt.Kind switch
            {
                BlackboxType.BlackboxTypeKind.Binary => "Blackbox(Binary)",
                BlackboxType.BlackboxTypeKind.Xml => "Blackbox(XML)",
                _ => "Blackbox"
            },
            EnumerationType et =>
                $"Enum{MermaidConstants.LeftParenthesis}{FormatEnumerationValues(et.Values)}{MermaidConstants.RightParenthesis}",
            TypeRef tr => tr.Extends?.Path.Last() ?? "?",
            RoleType => "Role",
            _ => type.GetType().Name
        };
    }

    private static string FormatEnumerationValues(EnumerationValuesList enumerationValues)
    {
        return string.Join(", ", enumerationValues.Select(v => v.Name));
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

    private static string CardinalitySuffix(TypeDef? type)
    {
        if (type?.Cardinality is not { } cardinality
            || cardinality.Min is not long min
            || cardinality.Max is not long max
            || min != max
            || min <= 1)
            return string.Empty;

        return $" ×{min}";
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

    public string GetDiagramDocument()
    {
        foreach (var type in classes.Concat(structures))
        {
            if (type.Extends is { } extRef && extRef.Path.Any())
            {
                string parentSimple = extRef.Path.Last();
                string parentLabel =
                    extRef.Target != null ? parentSimple : $"`{parentSimple} #60;#60;EXTERNAL#62;#62;`";

                mermaidScript.AppendLine($"{type.Name} --|> {parentLabel}");
            }

            if (type.MetaAttributes.TryGetValue("geow.uml.color", out var color) && !string.IsNullOrWhiteSpace(color))
            {
                mermaidScript.AppendLine($"style {type.Name} fill:{color},color:black,stroke:black");
            }

            foreach (var attr in type.Content.Values.OfType<AttributeDef>())
                AppendAttributeDetailsToScript(type, attr);

            mermaidScript.AppendLine();
        }

        foreach (var associationDef in associations)
            AppendAssociationDetailsToScript(associationDef);

        mermaidScript.AppendLine();

        foreach (var structure in structures)
            mermaidScript.AppendLine($"style {structure.Name} fill:,stroke-dasharray:10 10");

        return mermaidScript.ToString();
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
