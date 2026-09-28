using Geowerkstatt.Interlis.Compiler.AST;
using Geowerkstatt.Interlis.Compiler.AST.Expression;
using Geowerkstatt.Interlis.Compiler.AST.Types;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// INTERLIS AST visitor that builds a <see cref="GraphDocument"/>: a renderer-neutral graph of
/// every model in the environment (the requested file and its imports) with model and topic frames,
/// boxes for classes, structures, views, domains and attributed associations, and the relations
/// between them. The output carries no layout or markup, so the webview can lay it out interactively.
/// </summary>
internal class GraphDocumentVisitor : Interlis24AstBaseVisitor<object?>
{
    private const string ExternalIdPrefix = "external:";
    private const string ColorMetaAttribute = "geow.uml.color";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<ModelDef> models = new();
    private readonly List<(TopicDef Topic, ModelDef Model)> topics = new();
    private readonly List<(InterlisDefinition Definition, string GroupId)> members = new();
    private readonly List<ConstraintsBlockDef> constraintBlocks = new();
    private readonly ILogger<GraphDocumentVisitor> logger;
    private readonly DocumentationLocalization locale;
    private readonly string? sourceUri;

    private ModelDef? currentModel;
    private TopicDef? currentTopic;

    /// <param name="sourceUri">
    /// URI of the file the graph was requested for, as stored in <see cref="ModelDef.SourceUri"/>; models read
    /// from any other file are flagged as imported. <c>null</c> flags nothing.
    /// </param>
    public GraphDocumentVisitor(ILogger<GraphDocumentVisitor> logger, DocumentationLocalization? locale = null, string? sourceUri = null)
    {
        this.logger = logger;
        this.locale = locale ?? DocumentationLocalization.For(DocumentationLocalization.German);
        this.sourceUri = sourceUri;
    }

    public override object? VisitModelDef([NotNull] ModelDef modelDef)
    {
        if (modelDef == InternalModel.Interlis)
        {
            return null;
        }

        currentModel = modelDef;
        models.Add(modelDef);
        base.VisitModelDef(modelDef);
        currentModel = null;
        return null;
    }

    public override object? VisitTopicDef([NotNull] TopicDef topicDef)
    {
        if (currentModel is not null)
        {
            topics.Add((topicDef, currentModel));
        }

        currentTopic = topicDef;
        base.VisitTopicDef(topicDef);
        currentTopic = null;
        return null;
    }

    public override object? VisitClassDef([NotNull] ClassDef classDef) => AddMember(classDef);

    public override object? VisitViewDef([NotNull] ViewDef viewDef) => AddMember(viewDef);

    public override object? VisitAssociationDef([NotNull] AssociationDef associationDef) => AddMember(associationDef);

    public override object? VisitDomainDef([NotNull] DomainDef domainDef) => AddMember(domainDef);

    public override object? VisitConstraintsBlockDef([NotNull] ConstraintsBlockDef constraintsBlockDef)
    {
        constraintBlocks.Add(constraintsBlockDef);
        return DefaultResult;
    }

    /// <summary>Records a definition with its frame (topic, or model for model-level definitions); its content is read later.</summary>
    private object? AddMember(InterlisDefinition definition)
    {
        var container = (IInterlisDefinition?)currentTopic ?? currentModel;
        if (container is not null)
        {
            members.Add((definition, GetId(container)));
        }

        return DefaultResult;
    }

    /// <summary>
    /// Builds the graph from the visited definitions. Targets that could not be resolved are represented
    /// by <c>external</c> stub nodes so their edges can still be drawn; targets that resolved to something
    /// the graph does not draw (the INTERLIS internal model, a plain association) yield no edge.
    /// </summary>
    public GraphDocument GetGraphDocument()
    {
        var knownIds = members
            .Where(member => member.Definition is ClassDef or ViewDef or DomainDef || (member.Definition is AssociationDef association && IsBox(association)))
            .Select(member => GetId(member.Definition))
            .ToHashSet(StringComparer.Ordinal);
        var stubs = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        // CONSTRAINTS OF blocks belong to the box of their target; the compiler continues the target's
        // constraint numbering in them, so their synthesized names do not clash with the box's own.
        var externalConstraints = constraintBlocks
            .Where(block => block.Target?.Target is IInterlisDefinition target && knownIds.Contains(GetId(target)))
            .GroupBy(block => GetId((IInterlisDefinition)block.Target!.Target!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(block => block.Constraints).ToList(), StringComparer.Ordinal);

        IEnumerable<GraphConstraint> ConstraintsOf(InterlisDefinition definition) =>
            ((IConstraintContainer)definition).Constraints
                .Concat(externalConstraints.GetValueOrDefault(GetId(definition)) ?? Enumerable.Empty<ConstraintDef>())
                .Select(constraint => new GraphConstraint(FormatConstraintKind(constraint), constraint.Name));

        // A domain restricts its values with named CONSTRAINTS (RefHB 3.8-8); they are not ConstraintDefs.
        IEnumerable<GraphConstraint> DomainConstraintsOf(DomainDef domain) =>
            domain.TypeDef.Constraints.Values.Select(constraint => new GraphConstraint("Domain Constraint", constraint.Name));

        string? Resolve<T>(Reference<T>? reference) where T : class, IReferenceTarget
        {
            if (reference is null)
            {
                return null;
            }

            if (reference.Target is IInterlisDefinition target)
            {
                var id = GetId(target);
                return knownIds.Contains(id) ? id : null;
            }

            if (reference.Path.Count == 0)
            {
                return null;
            }

            var stubId = ExternalIdPrefix + string.Join(".", reference.Path.Select(segment => segment.Name));
            stubs.TryAdd(stubId, CreateStub(stubId, reference.Path[^1].Name));
            return stubId;
        }

        // ANYCLASS / ANYSTRUCTURE name no single target, so they yield no edge.
        string? ResolveRestricted(RestrictedRef? restricted) =>
            restricted?.Value is RestrictedRef.DefinitionRef definitionRef ? Resolve(definitionRef.Reference) : null;

        void AddInheritance<T>(InterlisDefinition child, Reference<T>? extends) where T : class, IReferenceTarget
        {
            if (Resolve(extends) is { } parentId)
            {
                edges.Add(new GraphEdge("inheritance", GetId(child), parentId, null, null, null));
            }
        }

        // A domain a type builds on without extending it: the vertex type of a geometry, the format
        // base of a formatted type, or the enumeration an ALL OF takes its values from. Only domains
        // drawn as boxes yield an edge, so the INTERLIS internal types (BOOLEAN, XMLDate, ...) never do.
        string? ResolveUsedDomain(TypeDef type) => type switch
        {
            EnumerationValuesType enumerationValues => Resolve(enumerationValues.TargetEnumeration),
            FormattedType formatted => Resolve(formatted.FormatBaseType),
            ILineType lineType => Resolve(lineType.VertexType),
            _ => null,
        };

        void AddAttributeEdges(InterlisDefinition owner, IEnumerable<AttributeDef> attributes)
        {
            foreach (var attribute in attributes)
            {
                var end = new GraphEdgeEnd(null, FormatEndCardinality(attribute.TypeDef?.Cardinality), "association");
                switch (attribute.TypeDef)
                {
                    case ReferenceType referenceType when ResolveRestricted(referenceType.Target) is { } targetId:
                        edges.Add(new GraphEdge("reference", GetId(owner), targetId, attribute.Name, null, end));
                        break;
                    case ObjectType { Targets: [var structure] } when ResolveRestricted(structure) is { } targetId:
                        // Structure instances exist only as part of their owner, hence the composition at the source.
                        edges.Add(new GraphEdge("structure", GetId(owner), targetId, attribute.Name, new GraphEdgeEnd(null, null, "composition"), end));
                        break;
                    case { } type when (Resolve(type.Extends) ?? ResolveUsedDomain(type)) is { } targetId:
                        // Typed by a domain, directly (a TypeRef alias) or through a type built on one.
                        edges.Add(new GraphEdge("domain", GetId(owner), targetId, attribute.Name, null, end));
                        break;
                }
            }
        }

        void AddDerivations(ViewDef view)
        {
            var (kind, bases) = view.Formation switch
            {
                ProjectionView projection => ("projection", new[] { projection.Source }),
                JoinView join => ("join", join.Sources.Select(source => source.Viewable).ToArray()),
                UnionView union => ("union", union.Sources.ToArray()),
                AggregationView aggregation => ("aggregation", new[] { aggregation.Source }),
                InspectionView inspection => ("inspection", new[] { inspection.Source }),
                _ => ("", Array.Empty<BaseView>()),
            };
            foreach (var baseView in bases)
            {
                if (Resolve(baseView.Viewable) is { } baseId)
                {
                    edges.Add(new GraphEdge("derivation", GetId(view), baseId, kind, null, null));
                }
            }
        }

        void AddRoleEdges(AssociationDef association)
        {
            foreach (var role in Roles(association))
            {
                var roleType = (RoleType)role.TypeDef;
                foreach (var target in roleType.Targets)
                {
                    if (ResolveRestricted(target) is { } targetId)
                    {
                        edges.Add(new GraphEdge("role", GetId(association), targetId, role.Name, null, RoleEnd(role, includeRole: false)));
                    }
                }
            }
        }

        void AddAssociationEdge(AssociationDef association, string groupId)
        {
            var ends = Roles(association)
                .Select(role => (Id: ResolveRestricted(((RoleType)role.TypeDef).Targets.FirstOrDefault()), End: RoleEnd(role, includeRole: true)))
                .ToList();
            if (ends.Count != 2 || ends.Any(end => end.Id is null))
            {
                logger.LogWarning("Skip association '{Name}' – role target is not part of the graph", association.Name);
                return;
            }

            edges.Add(new GraphEdge("association", ends[0].Id!, ends[1].Id!, association.Name, ends[0].End, ends[1].End, groupId, GetLine(association)));
        }

        foreach (var (definition, groupId) in members)
        {
            switch (definition)
            {
                case ClassDef classDef:
                    nodes.Add(CreateNode(classDef, groupId, classDef.IsStructure ? "structure" : "class", classDef.Properties, Attributes(classDef), ConstraintsOf(classDef)));
                    AddInheritance(classDef, classDef.Extends);
                    AddAttributeEdges(classDef, Attributes(classDef));
                    break;
                case ViewDef viewDef:
                    nodes.Add(CreateNode(viewDef, groupId, "view", viewDef.Properties, Attributes(viewDef), ConstraintsOf(viewDef)));
                    AddInheritance(viewDef, viewDef.Extends);
                    AddAttributeEdges(viewDef, Attributes(viewDef));
                    AddDerivations(viewDef);
                    break;
                case AssociationDef associationDef when IsBox(associationDef):
                    nodes.Add(CreateNode(associationDef, groupId, "association", associationDef.Properties, NonRoleAttributes(associationDef), ConstraintsOf(associationDef)));
                    AddInheritance(associationDef, associationDef.Extends);
                    AddAttributeEdges(associationDef, NonRoleAttributes(associationDef));
                    AddRoleEdges(associationDef);
                    break;
                case AssociationDef associationDef:
                    AddAssociationEdge(associationDef, groupId);
                    break;
                case DomainDef domainDef:
                    nodes.Add(CreateNode(domainDef, groupId, "domain", domainDef.Properties, Array.Empty<AttributeDef>(), DomainConstraintsOf(domainDef), FormatType(domainDef.TypeDef)));
                    AddInheritance(domainDef, domainDef.TypeDef.Extends);
                    if (ResolveUsedDomain(domainDef.TypeDef) is { } usedId)
                    {
                        edges.Add(new GraphEdge("domain", GetId(domainDef), usedId, null, null, null));
                    }

                    break;
            }
        }

        nodes.AddRange(stubs.Values);

        // Frames: topics with content, and models with content or with such a topic. Empty ones would only add noise.
        var usedGroups = nodes.Select(node => node.Group).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var shownTopics = topics.Where(entry => usedGroups.Contains(GetId(entry.Topic))).ToList();
        var shownModels = models
            .Where(model => usedGroups.Contains(GetId(model)) || shownTopics.Any(entry => entry.Model == model))
            .ToList();
        var groups = new List<GraphGroup>();
        foreach (var model in shownModels)
        {
            groups.Add(new GraphGroup(GetId(model), model.Name, "model", null, IsImported(model), model.SourceUri, GetLine(model)));
        }

        foreach (var (topic, model) in shownTopics)
        {
            groups.Add(new GraphGroup(GetId(topic), topic.Name, "topic", GetId(model), IsImported(model), null, GetLine(topic)));
        }

        // Frame relations are only drawn between frames that exist; a frame cannot be stubbed.
        var groupIds = groups.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (topic, _) in shownTopics)
        {
            if (topic.Extends?.Target is TopicDef parent && groupIds.Contains(GetId(parent)))
            {
                edges.Add(new GraphEdge("inheritance", GetId(topic), GetId(parent), null, null, null));
            }
        }

        foreach (var model in shownModels)
        {
            foreach (var import in model.Imports.Values)
            {
                if (import.ModelDef.Target is ModelDef imported && groupIds.Contains(GetId(imported)))
                {
                    edges.Add(new GraphEdge("import", GetId(model), GetId(imported), null, null, null));
                }
            }
        }

        return new GraphDocument(groups, nodes, edges);
    }

    /// <summary>Serializes <see cref="GetGraphDocument"/> as camelCase JSON without null members.</summary>
    public string GetGraphJson() => JsonSerializer.Serialize(GetGraphDocument(), JsonOptions);

    /// <summary>
    /// Allows only a hex literal or a plain color name through to the webview, so a crafted
    /// geow.uml.color meta-attribute cannot inject markup or style into the rendered box.
    /// </summary>
    internal static bool IsSafeColor(string color) =>
        Regex.IsMatch(color, "^#[0-9A-Fa-f]{3,8}$") || Regex.IsMatch(color, "^[A-Za-z]{1,32}$");

    /// <summary>
    /// An association is drawn as a box (with one edge per role) unless it is a plain binary one:
    /// exactly two roles with a single target each and no attributes. Constraints do not make a
    /// box, so the constraints of a plain association are not shown.
    /// </summary>
    private static bool IsBox(AssociationDef association)
    {
        var roles = Roles(association);
        return roles.Count != 2
            || roles.Any(role => ((RoleType)role.TypeDef).Targets.Count != 1)
            || NonRoleAttributes(association).Any();
    }

    private static IEnumerable<AttributeDef> Attributes(IInterlisDefinitionContainer container) =>
        container.Content.Values.OfType<AttributeDef>();

    private static List<AttributeDef> Roles(AssociationDef association) =>
        Attributes(association).Where(attribute => attribute.TypeDef is RoleType).ToList();

    private static IEnumerable<AttributeDef> NonRoleAttributes(AssociationDef association) =>
        Attributes(association).Where(attribute => attribute.TypeDef is not RoleType);

    private bool IsImported(ModelDef model) =>
        sourceUri is not null && !string.Equals(model.SourceUri, sourceUri, StringComparison.Ordinal);

    private GraphNode CreateNode(InterlisDefinition definition, string groupId, string kind, HashSet<Property> properties, IEnumerable<AttributeDef> attributes, IEnumerable<GraphConstraint> constraints, string? type = null)
    {
        string? color = null;
        if (definition.MetaAttributes.TryGetValue(ColorMetaAttribute, out var configuredColor) && !string.IsNullOrWhiteSpace(configuredColor))
        {
            if (IsSafeColor(configuredColor))
            {
                color = configuredColor;
            }
            else
            {
                logger.LogWarning("Ignoring unsafe geow.uml.color value on {Id}", GetId(definition));
            }
        }

        var rows = attributes
            .Select(attribute => new GraphAttribute(
                attribute.Name,
                FormatAttributeType(attribute),
                FormatAttributeCardinality(attribute.TypeDef?.Cardinality)))
            .ToList();

        return new GraphNode(
            GetId(definition),
            definition.Name,
            groupId,
            kind,
            properties.Contains(Property.Abstract),
            properties.Contains(Property.External),
            color,
            GetLine(definition),
            type,
            rows,
            constraints.ToList());
    }

    private static GraphNode CreateStub(string id, string name) =>
        new(id, name, null, "external", false, true, null, null, null, Array.Empty<GraphAttribute>(), Array.Empty<GraphConstraint>());

    private static string FormatConstraintKind(ConstraintDef constraint) => constraint switch
    {
        MandatoryConstraint => "Mandatory Constraint",
        SetConstraint => "Set Constraint",
        ExistenceConstraint => "Existence Constraint",
        UniquenessConstraint => "Unique Constraint",
        PlausibilityConstraint => "Plausibility Constraint",
        _ => "Constraint",
    };

    private static GraphEdgeEnd RoleEnd(AttributeDef role, bool includeRole)
    {
        var roleType = (RoleType)role.TypeDef;
        var relationship = roleType.Relationship switch
        {
            RoleType.RelationshipType.Aggregation => "aggregation",
            RoleType.RelationshipType.Composition => "composition",
            _ => "association",
        };
        return new GraphEdgeEnd(includeRole ? role.Name : null, roleType.Cardinality is { } cardinality ? FormatCardinality(cardinality) : "*", relationship);
    }

    private static string GetId(IInterlisDefinition definition) => definition.FullyQualifiedName;

    /// <summary>The line of the name, or of the declaration for an unnamed association (<c>ASSOCIATION =</c>).</summary>
    private static int? GetLine(IInterlisDefinition definition) =>
        definition.NameLocations.FirstOrDefault()?.Start.Line ?? definition.SourceRange?.Start.Line;

    /// <summary>UML multiplicity such as <c>0..*</c>, or <c>2</c> when both bounds are equal.</summary>
    private static string FormatCardinality(Cardinality cardinality)
    {
        var min = cardinality.Min?.ToString() ?? "*";
        var max = cardinality.Max?.ToString() ?? "*";
        return min == max ? min : $"{min}..{max}";
    }

    /// <summary>Multiplicity at the far end of an attribute edge: <c>1</c> for MANDATORY, <c>0..1</c> for optional, the collection bounds for BAG/LIST.</summary>
    private static string FormatEndCardinality(Cardinality? cardinality) =>
        cardinality is null ? "1" : FormatCardinality(cardinality);

    /// <summary>Multiplicity in an attribute row; <c>null</c> for the implicit <c>1</c>.</summary>
    private static string? FormatAttributeCardinality(Cardinality? cardinality) =>
        cardinality is null || (cardinality.Min == 1 && cardinality.Max == 1) ? null : FormatCardinality(cardinality);

    /// <summary>
    /// A view attribute defined by a path (<c>attr := Base->Attr</c>) has no declared type; it shows the path instead.
    /// </summary>
    private string FormatAttributeType(AttributeDef attribute) =>
        attribute.TypeDef is UndefinedType or AttributePathType && attribute.Values.FirstOrDefault() is PathExpression path
            ? FormatPath(path)
            : FormatType(attribute.TypeDef);

    private string FormatType(TypeDef? type)
    {
        return type switch
        {
            null => "?",
            ReferenceType referenceType => referenceType.Target.Value.GetTargetName() ?? "?",
            TextType textType => textType.Length is { } length ? $"Text[{length}]" : "Text",
            NumericType numericType => FormatNumericType(numericType),
            BooleanType => "Boolean",
            BlackboxType blackboxType => blackboxType.Kind switch
            {
                BlackboxType.BlackboxTypeKind.Binary => $"Blackbox({locale.BlackboxBinarySuffix})",
                BlackboxType.BlackboxTypeKind.Xml => $"Blackbox({locale.BlackboxXmlSuffix})",
                _ => "Blackbox",
            },
            EnumerationType enumerationType => $"Enum({string.Join(", ", enumerationType.Values.Select(value => value.Name))})",
            EnumerationValuesType enumerationValues => enumerationValues.TargetEnumeration?.Path.LastOrDefault()?.Name ?? "?",
            FormattedType formatted => formatted.BasedOn?.Path.LastOrDefault()?.Name
                                       ?? formatted.FormatBaseType?.Path.LastOrDefault()?.Name
                                       ?? "Format",
            SurfaceType surface => (surface.IsMultiGeometry ? "Multi" : "") + (surface.IsCoverage ? "Area" : "Surface"),
            PolyLineType polyLine => (polyLine.IsMultiGeometry ? "Multi" : "") + "Polyline",
            CoordType coord => (coord.IsMultiGeometry ? "Multi" : "") + "Coord",
            TypeRef typeRef => typeRef.Extends?.Path.LastOrDefault()?.Name ?? "?",
            ObjectType objectType => FormatTargetNames(objectType.Targets),
            UnresolvedNamedType unresolved => unresolved.Target.Value.GetTargetName() ?? "?",
            AttributePathType pathType => pathType.Of is { } path ? FormatPath(path) : "?",
            UndefinedType => "?",
            RoleType => "Role",
            _ => type.GetType().Name,
        };
    }

    private static string FormatPath(PathExpression path)
    {
        var elements = path.Reference.Path.Select(segment => segment.Name);
        var text = string.Join("->", elements);
        return text.Length > 0 ? text : "?";
    }

    private string FormatNumericType(NumericType numericType)
    {
        var text = numericType.Min != null && numericType.Max != null
            ? $"{numericType.Min}..{numericType.Max}"
            : locale.NumericLabel;

        var unitName = numericType.Unit?.Target?.Name ?? numericType.Unit?.Path.LastOrDefault()?.Name;
        return string.IsNullOrEmpty(unitName) ? text : $"{text} [{unitName}]";
    }

    private static string FormatTargetNames(IEnumerable<RestrictedRef> targets)
    {
        var names = string.Join(", ", targets.Select(target => target.Value.GetTargetName()).Where(name => name is not null));
        return names.Length > 0 ? names : "?";
    }
}
