namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

/// <summary>
/// Renderer-neutral class graph of an INTERLIS file and the models it imports: model and topic
/// frames, boxes for classes, structures, views, domains and attributed associations, and the
/// relations between them. Serialized to JSON for the interactive diagram renderer in the webview
/// (see <c>src/assets/cytoscapeRenderer.ts</c>).
/// </summary>
internal sealed record GraphDocument(
    IReadOnlyList<GraphGroup> Groups,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

/// <summary>A model or topic, rendered as a frame around its content. Only frames with content are emitted.</summary>
/// <param name="Id">Fully qualified name, referenced by <see cref="GraphNode.Group"/>, <see cref="Parent"/> and frame edges.</param>
/// <param name="Name">Simple name shown as the frame label.</param>
/// <param name="Kind"><c>model</c> or <c>topic</c>.</param>
/// <param name="Parent">Id of the model frame a topic belongs to; <c>null</c> for models.</param>
/// <param name="IsImported">Whether the model comes from another file than the one the graph was requested for (inherited by its topics).</param>
/// <param name="Uri">Location of the file the model was read from, when known; <c>null</c> for topics, which share their model's file.</param>
/// <param name="Line">Zero-based line of the name in that file, used to jump to the definition.</param>
internal sealed record GraphGroup(
    string Id,
    string Name,
    string Kind,
    string? Parent,
    bool IsImported,
    string? Uri,
    int? Line);

/// <summary>A class, structure, view, domain or attributed-association box.</summary>
/// <param name="Id">Fully qualified name; unique within the document.</param>
/// <param name="Name">Simple name shown in the box header.</param>
/// <param name="Group">Id of the containing topic or, for model-level definitions, model; <c>null</c> for stubs.</param>
/// <param name="Kind"><c>class</c>, <c>structure</c>, <c>view</c>, <c>association</c>, <c>domain</c>, or <c>external</c> for a stub standing in for an unresolved definition.</param>
/// <param name="IsAbstract">Whether the definition carries the ABSTRACT property.</param>
/// <param name="IsExternal">Whether the definition carries the EXTERNAL property.</param>
/// <param name="Color">Validated <c>geow.uml.color</c> meta-attribute, or <c>null</c>.</param>
/// <param name="Line">Zero-based line of the name in the model's file, used to jump to the definition; <c>null</c> for stubs.</param>
/// <param name="Type">The type a domain defines, formatted like an attribute type; <c>null</c> for every other kind.</param>
/// <param name="Attributes">Own (non-inherited) attributes in declaration order; roles are left out of association boxes, domains have none.</param>
/// <param name="Constraints">Own constraints in declaration order, followed by those of <c>CONSTRAINTS OF</c> blocks targeting the definition; for a domain its value restrictions; empty for stubs.</param>
internal sealed record GraphNode(
    string Id,
    string Name,
    string? Group,
    string Kind,
    bool IsAbstract,
    bool IsExternal,
    string? Color,
    int? Line,
    string? Type,
    IReadOnlyList<GraphAttribute> Attributes,
    IReadOnlyList<GraphConstraint> Constraints);

/// <param name="Cardinality">UML multiplicity such as <c>0..*</c> or <c>2</c>; <c>null</c> for the implicit <c>1</c>.</param>
internal sealed record GraphAttribute(string Name, string Type, string? Cardinality);

/// <param name="Kind">The constraint kind as shown in the box, e.g. <c>Set Constraint</c>, <c>Unique Constraint</c> or <c>Domain Constraint</c>.</param>
/// <param name="Name">The constraint name used in messages: the declared one, or the <c>Constraint&lt;n&gt;</c> the compiler synthesizes for an anonymous constraint; domain constraints are always named.</param>
internal sealed record GraphConstraint(string Kind, string Name);

/// <summary>A relation between two nodes or two frames.</summary>
/// <param name="Kind">
/// <list type="bullet">
/// <item><c>inheritance</c>: <see cref="Source"/> EXTENDS <see cref="Target"/>; between boxes (a domain extending a domain included) or between topic frames.</item>
/// <item><c>association</c>: a binary association without attributes drawn as one line; both ends carry role, multiplicity and relationship.</item>
/// <item><c>role</c>: from an association box to the class one of its roles points at; <see cref="Name"/> is the role.</item>
/// <item><c>reference</c>: a <c>REFERENCE TO</c> attribute of <see cref="Source"/> pointing at <see cref="Target"/>.</item>
/// <item><c>structure</c>: an attribute of <see cref="Source"/> whose values are instances of the structure <see cref="Target"/>; composition at the source.</item>
/// <item><c>domain</c>: an attribute of <see cref="Source"/> typed by the domain <see cref="Target"/> (directly, by extending it, as the vertex type of a geometry, or as <c>ALL OF</c> an enumeration); from a domain box, its own type using another domain the same way.</item>
/// <item><c>derivation</c>: the view <see cref="Source"/> is formed from <see cref="Target"/>; <see cref="Name"/> is the formation kind.</item>
/// <item><c>import</c>: the model frame <see cref="Source"/> IMPORTS the model frame <see cref="Target"/>.</item>
/// </list>
/// </param>
/// <param name="Name">Association, role or attribute name, or the view formation kind; <c>null</c> for inheritance, imports and the type of a domain.</param>
/// <param name="SourceEnd">Role information at the source node; present on <c>association</c> and <c>structure</c> edges.</param>
/// <param name="TargetEnd">Multiplicity (and, for associations, role) at the target node; <c>null</c> for inheritance, derivation and imports.</param>
/// <param name="Group">For an <c>association</c> edge, the id of the topic or model the association is declared in, used to find its file; <c>null</c> otherwise.</param>
/// <param name="Line">For an <c>association</c> edge, the zero-based line of its declaration, used to jump to it; <c>null</c> otherwise.</param>
internal sealed record GraphEdge(
    string Kind,
    string Source,
    string Target,
    string? Name,
    GraphEdgeEnd? SourceEnd,
    GraphEdgeEnd? TargetEnd,
    string? Group = null,
    int? Line = null);

/// <param name="Role">Role name pointing at this end's class; <c>null</c> when the edge <see cref="GraphEdge.Name"/> already names it or the end has no role.</param>
/// <param name="Cardinality">Multiplicity at this end, e.g. <c>1</c> or <c>0..*</c>; <c>null</c> when the end has none.</param>
/// <param name="Relationship"><c>association</c>, <c>aggregation</c> or <c>composition</c>; the diamond is drawn at this end.</param>
internal sealed record GraphEdgeEnd(string? Role, string? Cardinality, string Relationship);
