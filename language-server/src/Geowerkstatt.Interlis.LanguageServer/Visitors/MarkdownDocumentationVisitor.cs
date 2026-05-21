using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor to generate markdown documentation.
/// </summary>
internal class MarkdownDocumentationVisitor : Interlis24AstBaseVisitor<object>
{
    private readonly StringBuilder documentation = new StringBuilder();
    private readonly DocumentationOptions config;
    private readonly DocumentationLocalization locale;
    private bool useHtml;

    public MarkdownDocumentationVisitor(DocumentationOptions? config = null)
    {
        this.config = config ?? new DocumentationOptions();
        this.locale = DocumentationLocalization.For(this.config.Language);
    }

    /// <summary>
    /// Escapes a leaf value for the output context currently being written.
    /// Class/attribute names come from the user's .ili file and the column
    /// headers from workspace settings; neither is trusted to be free of
    /// HTML or Markdown metacharacters.
    /// </summary>
    private string EscapeText(string? value) => useHtml ? HtmlEscape(value) : MarkdownEscape(value);

    private static string HtmlEscape(string? value) =>
        (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");

    private static string MarkdownEscape(string? value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("`", "\\`")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

    /// <summary>
    /// Generates markdown documentation for the given model.
    /// </summary>
    /// <param name="modelDef">The INTERLIS model.</param>
    public override object? VisitModelDef([NotNull] ModelDef modelDef)
    {
        if (modelDef == InternalModel.Interlis)
        {
            return null;
        }

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
            var mode = config.AbstractClassAttributes;
            if (mode == DocumentationOptions.AbstractClassAttributesInline
                || mode == DocumentationOptions.AbstractClassAttributesInlineAbstractOnly)
            {
                var abstractOnly = mode == DocumentationOptions.AbstractClassAttributesInlineAbstractOnly;
                var inheritedAttributes = CollectInheritedAttributes(classDef, abstractOnly);
                foreach (var attr in inheritedAttributes)
                {
                    VisitInheritedAttributeDef(attr);
                }
            }

            // Then visit this class's own attributes
            base.VisitClassDef(classDef);
            VisitRelatedAssociations(classDef);
        }

        var emptyPlaceholder = EscapeText(config.EmptyClassPlaceholder);

        if (useHtml)
        {
            var headerStart = documentation.Length;
            documentation.Append("<table>");
            documentation.Append($"<thead><tr><th>{EscapeText(config.AttributeNameHeader)}</th><th>{EscapeText(config.CardinalityHeader)}</th><th>{EscapeText(config.TypeHeader)}</th></tr></thead>");
            documentation.Append("<tbody>");
            var bodyStart = documentation.Length;
            VisitTableBody();
            if (documentation.Length == bodyStart)
            {
                documentation.Length = headerStart;
                documentation.Append($"<p><em>{emptyPlaceholder}</em></p>");
            }
            else
            {
                documentation.Append("</tbody></table>");
            }
        }
        else
        {
            var escapedName = EscapeText(classDef.Name);
            var className = classDef.Properties.Contains(Property.Abstract)
                ? $"*{escapedName}*"
                : escapedName;
            documentation.AppendLine($"### {className}");
            var headerStart = documentation.Length;
            documentation.AppendLine($"| {EscapeText(config.AttributeNameHeader)} | {EscapeText(config.CardinalityHeader)} | {EscapeText(config.TypeHeader)} |");
            documentation.AppendLine("| --- | --- | --- |");
            var bodyStart = documentation.Length;
            VisitTableBody();
            if (documentation.Length == bodyStart)
            {
                documentation.Length = headerStart;
                documentation.AppendLine($"_{emptyPlaceholder}_");
            }
            documentation.AppendLine();
        }

        return null;
    }

    /// <summary>
    /// Collects attributes inherited from ancestor classes, walking the full
    /// <c>EXTENDS</c> chain so transitive ancestors contribute too. When
    /// <paramref name="abstractParentsOnly"/> is <c>true</c>, only ancestors
    /// marked <see cref="Property.Abstract"/> contribute.
    /// </summary>
    private List<AttributeDef> CollectInheritedAttributes(ClassDef classDef, bool abstractParentsOnly)
    {
        var inherited = new List<AttributeDef>();
        var current = classDef.Extends?.Target;

        while (current != null)
        {
            if (!abstractParentsOnly || current.Properties.Contains(Property.Abstract))
            {
                var attrs = current.Content.Values
                    .OfType<AttributeDef>()
                    .ToList();

                inherited.InsertRange(0, attrs);
            }

            current = current.Extends?.Target;
        }

        return inherited;
    }

    /// <summary>
    /// Generates a markdown table row for an inherited attribute.
    /// Adds "(inherited)" marker to the attribute name.
    /// </summary>
    private void VisitInheritedAttributeDef(AttributeDef attributeDef)
    {
        var cardinality = CalculateCardinality(attributeDef.TypeDef.Cardinality);
        var inheritedSuffix = EscapeText(locale.InheritedSuffix);

        if (useHtml)
        {
            documentation.Append($"<tr><td>{EscapeText(attributeDef.Name)} <em>{inheritedSuffix}</em></td><td>{cardinality}</td><td>");
            VisitTypeName(attributeDef.TypeDef);
            documentation.Append("</td></tr>");
        }
        else
        {
            documentation.Append($"| {EscapeText(attributeDef.Name)} *{inheritedSuffix}* | {cardinality} | ");
            VisitTypeName(attributeDef.TypeDef);
            documentation.AppendLine(" |");
        }
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
            documentation.Append($"<tr><td>{EscapeText(attributeDef.Name)}</td><td>{cardinality}</td><td>");
            VisitTypeName(attributeDef.TypeDef);
            documentation.Append("</td></tr>");
        }
        else
        {
            documentation.Append($"| {EscapeText(attributeDef.Name)} | {cardinality} | ");
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
            var min = cardinality.Min?.ToString() ?? "*";
            var max = cardinality.Max?.ToString() ?? "*";
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
            NumericType numericType => numericType.Min != null && numericType.Max != null ? $"{numericType.Min}..{numericType.Max}" : locale.NumericLabel,
            BooleanType => "Boolean",
            BlackboxType blackboxType => blackboxType.Kind switch
            {
                BlackboxType.BlackboxTypeKind.Binary => $"Blackbox ({locale.BlackboxBinarySuffix})",
                BlackboxType.BlackboxTypeKind.Xml => $"Blackbox ({locale.BlackboxXmlSuffix})",
                _ => "Blackbox",
            },
            EnumerationType enumerationType => FormatEnumerationValues(enumerationType.Values),
            EnumerationAllOfType allOfType => FormatQualifiedPath(allOfType.TargetEnumeration?.Path),
            FormattedType formattedType => FormatFormattedType(formattedType),
            SurfaceType surfaceType => FormatGeometryName(surfaceType),
            PolyLineType polyLineType => FormatPolyLineName(polyLineType),
            CoordType coordType => FormatCoordName(coordType),
            TypeRef typeRef => typeRef.Extends?.Path.Last(),
            RoleType roleType => string.Join(", ", roleType.Targets.Select(target => target.Value?.Path.Last()).Where(target => target is not null)),
            _ => type?.ToString(),
        };

        // Enumeration formatting deliberately emits its own nested <i></i>
        // markup and its values are grammar-constrained identifiers, so it is
        // written verbatim. Every other branch yields leaf data (type names,
        // qualified paths, FORMAT min/max literals) that must be escaped for
        // the current output context.
        if (type is EnumerationType)
        {
            documentation.Append(typeName);
        }
        else
        {
            documentation.Append(EscapeText(typeName));
        }
    }

    private static string FormatGeometryName(SurfaceType surface)
    {
        var prefix = surface.IsMultiGeometry ? "Multi" : "";
        var kind = surface.IsCoverage ? "Area" : "Surface";
        return prefix + kind;
    }

    private static string FormatPolyLineName(PolyLineType polyLine)
    {
        var prefix = polyLine.IsMultiGeometry ? "Multi" : "";
        return prefix + "Polyline";
    }

    private static string FormatCoordName(CoordType coord)
    {
        var prefix = coord.IsMultiGeometry ? "Multi" : "";
        return prefix + "Coord";
    }

    private static string FormatFormattedType(FormattedType formattedType)
    {
        // Grammar (Interlis24Parser.g4):
        //   formattedType
        //     : BASED ON basedOn=definitionRef formatDef (min=string '..' max=string)?
        //     | domainRef=definitionRef min=string '..' max=string
        // The second alternative (e.g. `FORMAT INTERLIS.XMLDate "..." .. "..."`) populates
        // FormatBaseType, not BasedOn, so we have to read both.
        var name = "Format";
        if (formattedType.BasedOn != null)
        {
            name = FormatQualifiedPath(formattedType.BasedOn.Path);
        }
        else if (formattedType.FormatBaseType != null)
        {
            name = FormatQualifiedPath(formattedType.FormatBaseType.Path);
        }
        if (!string.IsNullOrEmpty(formattedType.Min) && !string.IsNullOrEmpty(formattedType.Max))
        {
            return $"{name} \"{formattedType.Min}\"..\"{formattedType.Max}\"";
        }
        return name;
    }

    private static string FormatQualifiedPath(IEnumerable<string>? path)
    {
        if (path == null) return "?";
        var joined = string.Join(".", path);
        return string.IsNullOrEmpty(joined) ? "?" : joined;
    }

    private static string FormatEnumerationValues(EnumerationValuesList enumerationValues, int depth = 0)
    {
        var (formatStart, formatEnd) = depth switch
        {
            0 => ("", ""),
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
        documentation.Append(EscapeText(typeName));

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
