using Geowerkstatt.Interlis.Compiler;
using Geowerkstatt.Interlis.Compiler.AST;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geowerkstatt.Interlis.LanguageServer.Visitors;

[TestClass]
public class GraphDocumentVisitorTests
{
    private static InterlisEnvironment Compile(string ili) => new InterlisReader().ReadFile(new StringReader(ili));

    private static GraphDocumentVisitor Visit(InterlisEnvironment environment, string? sourceUri = null)
    {
        var visitor = new GraphDocumentVisitor(NullLogger<GraphDocumentVisitor>.Instance, sourceUri: sourceUri);
        visitor.VisitInterlisEnvironment(environment);
        return visitor;
    }

    private static GraphDocument BuildGraph(string ili) => Visit(Compile(ili)).GetGraphDocument();

    private static GraphNode NodeById(GraphDocument graph, string id) =>
        graph.Nodes.Single(node => node.Id == id);

    private static GraphGroup GroupById(GraphDocument graph, string id) =>
        graph.Groups.Single(group => group.Id == id);

    private static List<GraphEdge> EdgesOfKind(GraphDocument graph, string kind) =>
        graph.Edges.Where(edge => edge.Kind == kind).ToList();

    [TestMethod]
    public void SingleClass_InTopic_ProducesModelTopicAndNode()
    {
        var graph = BuildGraph(@"
INTERLIS 2.4;
MODEL M (en) AT ""x"" VERSION ""1"" =
  TOPIC T =
    CLASS A = END A;
  END T;
END M.");

        Assert.AreEqual(2, graph.Groups.Count);
        var model = GroupById(graph, "M");
        Assert.AreEqual("M", model.Name);
        Assert.AreEqual("model", model.Kind);
        Assert.IsNull(model.Parent);
        Assert.IsFalse(model.IsImported);
        Assert.AreEqual(2, model.Line, "Line must point at the MODEL declaration (zero-based).");

        var topic = GroupById(graph, "M.T");
        Assert.AreEqual("T", topic.Name);
        Assert.AreEqual("topic", topic.Kind);
        Assert.AreEqual("M", topic.Parent);
        Assert.AreEqual(3, topic.Line);

        var node = graph.Nodes.Single();
        Assert.AreEqual("M.T.A", node.Id);
        Assert.AreEqual("A", node.Name);
        Assert.AreEqual("M.T", node.Group);
        Assert.AreEqual("class", node.Kind);
        Assert.IsFalse(node.IsAbstract);
        Assert.AreEqual(4, node.Line, "Line must point at the CLASS declaration (zero-based).");
        Assert.AreEqual(0, graph.Edges.Count);
    }

    [TestMethod]
    public void ModelLevelStructure_IsGroupedInTheModel()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              STRUCTURE S =
                id : TEXT*10;
              END S;
            END M.");

        Assert.AreEqual("model", graph.Groups.Single().Kind);
        var node = NodeById(graph, "M.S");
        Assert.AreEqual("structure", node.Kind);
        Assert.AreEqual("M", node.Group);
    }

    [TestMethod]
    public void TopicExtends_ProducesInheritanceEdgeBetweenFrames()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC Base =
                CLASS A (ABSTRACT) = END A;
              END Base;
              TOPIC Derived EXTENDS Base =
                CLASS B EXTENDS A = END B;
              END Derived;
              TOPIC Orphan EXTENDS Empty =
                CLASS C = END C;
              END Orphan;
              TOPIC Empty = END Empty;
            END M.");

        CollectionAssert.AreEquivalent(new[] { "M", "M.Base", "M.Derived", "M.Orphan" }, graph.Groups.Select(group => group.Id).ToArray());

        var inheritance = EdgesOfKind(graph, "inheritance");
        Assert.AreEqual(2, inheritance.Count);
        Assert.IsTrue(inheritance.Any(edge => edge.Source == "M.Derived.B" && edge.Target == "M.Base.A"));
        Assert.IsTrue(inheritance.Any(edge => edge.Source == "M.Derived" && edge.Target == "M.Base"), "Topic EXTENDS is an edge between the two frames.");
        Assert.IsTrue(inheritance.All(edge => edge.Group is null && edge.Line is null), "Only association lines carry a source location.");
        Assert.IsFalse(inheritance.Any(edge => edge.Source == "M.Orphan"), "A parent topic without content has no frame, so the edge is dropped.");
    }

    [TestMethod]
    public void EmptyTopic_ProducesNoFrames()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC Empty = END Empty;
            END M.");

        Assert.AreEqual(0, graph.Groups.Count);
        Assert.AreEqual(0, graph.Nodes.Count);
    }

    [TestMethod]
    public void Attributes_CarryTypeAndCardinality()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                STRUCTURE B = END B;
                CLASS C =
                  id : MANDATORY TEXT*10;
                  many : LIST OF B;
                  exactlyTwo : LIST {2..2} OF B;
                  status : (active, inactive, removed);
                  flag : BOOLEAN;
                END C;
              END T;
            END M.");

        var attributes = NodeById(graph, "M.T.C").Attributes;
        CollectionAssert.AreEqual(
            new[] { "id", "many", "exactlyTwo", "status", "flag" },
            attributes.Select(attribute => attribute.Name).ToArray());

        Assert.AreEqual("Text[10]", attributes[0].Type);
        Assert.IsNull(attributes[0].Cardinality, "Implicit 1..1 must be omitted.");
        Assert.AreEqual("B", attributes[1].Type);
        Assert.AreEqual("0..*", attributes[1].Cardinality);
        Assert.AreEqual("2", attributes[2].Cardinality);
        Assert.AreEqual("Enum(active, inactive, removed)", attributes[3].Type);
        Assert.AreEqual("Boolean", attributes[4].Type);
    }

    [TestMethod]
    public void Constraints_AreListedWithKindAndName()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Item =
                  code : TEXT*10;
                  amount : 0 .. 100;
                  MANDATORY CONSTRAINT DEFINED(code);
                  SET CONSTRAINT GWR0456: amount > 0;
                  UNIQUE code;
                  !!@ name = Plausible
                  CONSTRAINT >= 80 % amount > 50;
                END Item;
                STRUCTURE Ref =
                  ref : REFERENCE TO Item;
                  EXISTENCE CONSTRAINT ref->code REQUIRED IN Item : code;
                END Ref;
                CLASS Plain = END Plain;
                CONSTRAINTS OF Item =
                  MANDATORY CONSTRAINT Later: DEFINED(amount);
                END;
              END T;
            END M.");

        CollectionAssert.AreEqual(
            new[]
            {
                "Mandatory Constraint: Constraint1",
                "Set Constraint: GWR0456",
                "Unique Constraint: Constraint3",
                "Plausibility Constraint: Plausible",
                "Mandatory Constraint: Later",
            },
            NodeById(graph, "M.T.Item").Constraints.Select(constraint => $"{constraint.Kind}: {constraint.Name}").ToArray(),
            "Own constraints in declaration order, then those of the CONSTRAINTS OF block.");
        var existence = NodeById(graph, "M.T.Ref").Constraints.Single();
        Assert.AreEqual("Existence Constraint", existence.Kind);
        Assert.AreEqual(0, NodeById(graph, "M.T.Plain").Constraints.Count);
        Assert.IsFalse(graph.Nodes.Any(node => node.Name.StartsWith("CONSTRAINTS OF")), "A CONSTRAINTS OF block is not a box of its own.");
    }

    [TestMethod]
    public void UnnamedAssociation_PointsAtItsDeclaration()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
                CLASS B = END B;
                ASSOCIATION =
                  RoleA -- A;
                  RoleB -- {0..*} B;
                END;
              END T;
            END M.");

        var edge = graph.Edges.Single();
        Assert.AreEqual("association", edge.Kind);
        Assert.AreEqual("RoleARoleB", edge.Name, "The compiler names an unnamed association after its roles.");
        Assert.AreEqual(6, edge.Line, "Without a name token, the ASSOCIATION keyword is the place to jump to.");
    }

    [TestMethod]
    public void Inheritance_ProducesEdgeFromChildToParent()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Base (ABSTRACT) = END Base;
                CLASS Derived EXTENDS Base = END Derived;
              END T;
            END M.");

        Assert.IsTrue(NodeById(graph, "M.T.Base").IsAbstract);

        var edge = graph.Edges.Single();
        Assert.AreEqual("inheritance", edge.Kind);
        Assert.AreEqual("M.T.Derived", edge.Source);
        Assert.AreEqual("M.T.Base", edge.Target);
        Assert.IsNull(edge.Name);
        Assert.IsNull(edge.SourceEnd);
        Assert.IsNull(edge.TargetEnd);
    }

    [TestMethod]
    public void PlainAssociation_ProducesEdgeWithRolesAndMultiplicities()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Whole = END Whole;
                CLASS Part  = END Part;
                ASSOCIATION Comp =
                  WholeRef -<#> {1} Whole;
                  PartRef  --   {0..*} Part;
                END Comp;
              END T;
            END M.");

        Assert.AreEqual(2, graph.Nodes.Count, "A plain association is a line, not a box.");
        var edge = graph.Edges.Single();
        Assert.AreEqual("association", edge.Kind);
        Assert.AreEqual("Comp", edge.Name);
        Assert.AreEqual("M.T.Whole", edge.Source);
        Assert.AreEqual("M.T.Part", edge.Target);
        Assert.AreEqual("M.T", edge.Group, "The line knows the frame of its declaration, like a box does.");
        Assert.AreEqual(6, edge.Line, "Line must point at the ASSOCIATION declaration (zero-based).");

        Assert.IsNotNull(edge.SourceEnd);
        Assert.AreEqual("WholeRef", edge.SourceEnd.Role);
        Assert.AreEqual("1", edge.SourceEnd.Cardinality);
        Assert.AreEqual("composition", edge.SourceEnd.Relationship);

        Assert.IsNotNull(edge.TargetEnd);
        Assert.AreEqual("PartRef", edge.TargetEnd.Role);
        Assert.AreEqual("0..*", edge.TargetEnd.Cardinality);
        Assert.AreEqual("association", edge.TargetEnd.Relationship);
    }

    [TestMethod]
    public void AssociationWithAttributes_BecomesBoxWithRoleEdges()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Whole = END Whole;
                CLASS Part  = END Part;
                ASSOCIATION Comp =
                  WholeRef -<#> {1} Whole;
                  PartRef  --   {0..*} Part;
                  Since : TEXT*10;
                END Comp;
              END T;
            END M.");

        var box = NodeById(graph, "M.T.Comp");
        Assert.AreEqual("association", box.Kind);
        Assert.AreEqual("M.T", box.Group);
        CollectionAssert.AreEqual(new[] { "Since" }, box.Attributes.Select(attribute => attribute.Name).ToArray(), "Roles are drawn as edges, not rows.");

        Assert.AreEqual(0, EdgesOfKind(graph, "association").Count);
        var roles = EdgesOfKind(graph, "role");
        Assert.AreEqual(2, roles.Count);

        var whole = roles.Single(edge => edge.Target == "M.T.Whole");
        Assert.AreEqual("M.T.Comp", whole.Source);
        Assert.AreEqual("WholeRef", whole.Name);
        Assert.IsNull(whole.SourceEnd);
        Assert.IsNotNull(whole.TargetEnd);
        Assert.IsNull(whole.TargetEnd.Role, "The role is the edge name.");
        Assert.AreEqual("1", whole.TargetEnd.Cardinality);
        Assert.AreEqual("composition", whole.TargetEnd.Relationship);

        var part = roles.Single(edge => edge.Target == "M.T.Part");
        Assert.AreEqual("PartRef", part.Name);
        Assert.AreEqual("0..*", part.TargetEnd?.Cardinality);
        Assert.AreEqual("association", part.TargetEnd?.Relationship);
    }

    [TestMethod]
    public void AssociationWithConstraint_StaysALine()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
                CLASS B = END B;
                ASSOCIATION AB =
                  RoleA -- {1} A;
                  RoleB -- {0..*} B;
                  MANDATORY CONSTRAINT DEFINED (RoleA);
                END AB;
              END T;
            END M.");

        Assert.IsFalse(graph.Nodes.Any(node => node.Id == "M.T.AB"), "Constraints are not shown, so they do not warrant a box.");
        Assert.AreEqual(0, EdgesOfKind(graph, "role").Count);
        Assert.AreEqual("AB", graph.Edges.Single(edge => edge.Kind == "association").Name);
    }

    [TestMethod]
    public void AssociationWithThreeRoles_BecomesBox()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
                CLASS B = END B;
                CLASS C = END C;
                ASSOCIATION ABC =
                  RoleA -- {1} A;
                  RoleB -- {1} B;
                  RoleC -- {0..1} C;
                END ABC;
              END T;
            END M.");

        Assert.AreEqual("association", NodeById(graph, "M.T.ABC").Kind);
        CollectionAssert.AreEquivalent(
            new[] { "M.T.A", "M.T.B", "M.T.C" },
            EdgesOfKind(graph, "role").Select(edge => edge.Target).ToArray());
    }

    [TestMethod]
    public void ReferenceAttribute_ProducesReferenceEdge()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS Target = END Target;
                STRUCTURE Pointer =
                  Required : MANDATORY REFERENCE TO Target;
                  Optional : REFERENCE TO (EXTERNAL) Target;
                END Pointer;
              END T;
            END M.");

        var references = EdgesOfKind(graph, "reference");
        Assert.AreEqual(2, references.Count);
        Assert.IsTrue(references.All(edge => edge.Source == "M.T.Pointer" && edge.Target == "M.T.Target"));

        var required = references.Single(edge => edge.Name == "Required");
        Assert.IsNull(required.SourceEnd);
        Assert.AreEqual("1", required.TargetEnd?.Cardinality);
        Assert.AreEqual("association", required.TargetEnd?.Relationship);
        Assert.AreEqual("0..1", references.Single(edge => edge.Name == "Optional").TargetEnd?.Cardinality);

        Assert.AreEqual("Target", NodeById(graph, "M.T.Pointer").Attributes[0].Type, "The attribute row stays in the box.");
    }

    [TestMethod]
    public void StructureAttribute_ProducesCompositionEdgeToTheStructure()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                STRUCTURE S = END S;
                CLASS Owner =
                  One : S;
                  Some : MANDATORY S;
                  Many : BAG {1..*} OF S;
                  Name : TEXT*10;
                END Owner;
              END T;
            END M.");

        var edges = EdgesOfKind(graph, "structure");
        CollectionAssert.AreEqual(new[] { "One", "Some", "Many" }, edges.Select(edge => edge.Name).ToArray());
        Assert.IsTrue(edges.All(edge => edge.Source == "M.T.Owner" && edge.Target == "M.T.S"));
        Assert.IsTrue(edges.All(edge => edge.SourceEnd?.Relationship == "composition"), "The owner side carries the composition diamond.");
        Assert.IsTrue(edges.All(edge => edge.SourceEnd?.Cardinality is null));
        CollectionAssert.AreEqual(new[] { "0..1", "1", "1..*" }, edges.Select(edge => edge.TargetEnd?.Cardinality).ToArray());
    }

    [TestMethod]
    public void View_IsNodeWithDerivationEdges()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A =
                  Name : MANDATORY TEXT*10;
                END A;
                VIEW V
                  PROJECTION OF A;
                  =
                  Label := A -> Name;
                END V;
              END T;
            END M.");

        var view = NodeById(graph, "M.T.V");
        Assert.AreEqual("view", view.Kind);
        Assert.AreEqual("M.T", view.Group);
        CollectionAssert.AreEqual(new[] { "Label" }, view.Attributes.Select(attribute => attribute.Name).ToArray());
        Assert.AreEqual("A->Name", view.Attributes[0].Type, "A path attribute shows its path.");

        var derivation = graph.Edges.Single(edge => edge.Kind == "derivation");
        Assert.AreEqual("M.T.V", derivation.Source);
        Assert.AreEqual("M.T.A", derivation.Target);
        Assert.AreEqual("projection", derivation.Name);
    }

    [TestMethod]
    public void UnionView_DerivesFromEverySource()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
                CLASS B = END B;
                VIEW U
                  UNION OF A, B;
                  =
                END U;
              END T;
            END M.");

        var derivations = EdgesOfKind(graph, "derivation");
        CollectionAssert.AreEquivalent(new[] { "M.T.A", "M.T.B" }, derivations.Select(edge => edge.Target).ToArray());
        Assert.IsTrue(derivations.All(edge => edge.Name == "union"));
    }

    [TestMethod]
    public void UnresolvedParent_ProducesExternalStub()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS D EXTENDS Other.Base = END D;
              END T;
            END M.");

        var edge = graph.Edges.Single();
        Assert.AreEqual("inheritance", edge.Kind);
        Assert.AreEqual("M.T.D", edge.Source);

        var stub = NodeById(graph, edge.Target);
        Assert.AreEqual("external", stub.Kind);
        Assert.AreEqual("Base", stub.Name);
        Assert.IsNull(stub.Group);
        Assert.IsNull(stub.Line);
    }

    [TestMethod]
    public void ImportedModel_IsDrawnWithItsFrameAndImportEdge()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL Base (en) AT ""x"" VERSION ""1"" =
              TOPIC Catalogues =
                CLASS Item (ABSTRACT) = END Item;
              END Catalogues;
            END Base.
            MODEL Main (en) AT ""x"" VERSION ""1"" =
              IMPORTS Base;
              TOPIC Data EXTENDS Base.Catalogues =
                CLASS Entry EXTENDS Base.Catalogues.Item = END Entry;
              END Data;
            END Main.");

        CollectionAssert.AreEquivalent(new[] { "Base", "Base.Catalogues", "Main", "Main.Data" }, graph.Groups.Select(group => group.Id).ToArray());
        Assert.AreEqual("Base", GroupById(graph, "Base.Catalogues").Parent);

        var inheritance = EdgesOfKind(graph, "inheritance");
        Assert.IsTrue(inheritance.Any(edge => edge.Source == "Main.Data.Entry" && edge.Target == "Base.Catalogues.Item"), "Cross-model parents resolve to real nodes, not stubs.");
        Assert.IsTrue(inheritance.Any(edge => edge.Source == "Main.Data" && edge.Target == "Base.Catalogues"));
        Assert.IsFalse(graph.Nodes.Any(node => node.Kind == "external"));

        var import = graph.Edges.Single(edge => edge.Kind == "import");
        Assert.AreEqual("Main", import.Source);
        Assert.AreEqual("Base", import.Target);
    }

    [TestMethod]
    public void ModelsFromOtherFiles_AreFlaggedAsImported()
    {
        var environment = Compile(@"
            INTERLIS 2.4;
            MODEL Base (en) AT ""x"" VERSION ""1"" =
              TOPIC Catalogues =
                CLASS Item = END Item;
              END Catalogues;
            END Base.
            MODEL Main (en) AT ""x"" VERSION ""1"" =
              IMPORTS Base;
              TOPIC Data =
                CLASS Entry = END Entry;
              END Data;
            END Main.");
        environment.Content["Base"].SourceUri = "file:///repo/Base.ili";
        environment.Content["Main"].SourceUri = "file:///work/Main.ili";

        var graph = Visit(environment, "file:///work/Main.ili").GetGraphDocument();

        Assert.IsTrue(GroupById(graph, "Base").IsImported);
        Assert.IsTrue(GroupById(graph, "Base.Catalogues").IsImported, "Topics inherit the flag of their model.");
        Assert.AreEqual("file:///repo/Base.ili", GroupById(graph, "Base").Uri);
        Assert.IsFalse(GroupById(graph, "Main").IsImported);
        Assert.IsFalse(GroupById(graph, "Main.Data").IsImported);
        Assert.AreEqual("file:///work/Main.ili", GroupById(graph, "Main").Uri);
        Assert.IsNull(GroupById(graph, "Main.Data").Uri, "Topics share the file of their model.");
    }

    [TestMethod]
    public void ImportedModelWithoutContent_HasNoFrame()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL Functions (en) AT ""x"" VERSION ""1"" =
              FUNCTION isValid (value : NUMERIC) : BOOLEAN;
            END Functions.
            MODEL Main (en) AT ""x"" VERSION ""1"" =
              IMPORTS Functions;
              TOPIC Data =
                CLASS Entry = END Entry;
              END Data;
            END Main.");

        CollectionAssert.AreEquivalent(new[] { "Main", "Main.Data" }, graph.Groups.Select(group => group.Id).ToArray());
        Assert.AreEqual(0, EdgesOfKind(graph, "import").Count, "Imports of models without a frame are not drawn.");
    }

    [TestMethod]
    public void ImportedDomains_AreDrawnInTheirModel()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL Domains (en) AT ""x"" VERSION ""1"" =
              DOMAIN
                Name = TEXT*10;
            END Domains.
            MODEL Main (en) AT ""x"" VERSION ""1"" =
              IMPORTS Domains;
              TOPIC Data =
                CLASS Entry =
                  Name : Domains.Name;
                END Entry;
              END Data;
            END Main.");

        CollectionAssert.AreEquivalent(new[] { "Domains", "Main", "Main.Data" }, graph.Groups.Select(group => group.Id).ToArray());
        Assert.AreEqual("Domains", NodeById(graph, "Domains.Name").Group, "A model-level domain sits in the model frame.");
        Assert.AreEqual(1, EdgesOfKind(graph, "import").Count);
        Assert.AreEqual("Name", NodeById(graph, "Main.Data.Entry").Attributes[0].Type);

        var edge = EdgesOfKind(graph, "domain").Single();
        Assert.AreEqual("Main.Data.Entry", edge.Source);
        Assert.AreEqual("Domains.Name", edge.Target);
        Assert.AreEqual("Name", edge.Name);
    }

    [TestMethod]
    public void Domains_AreBoxesWithTheirTypeAndInheritance()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                DOMAIN
                  Species = (Oak, Beech, Pine);
                  ForestType EXTENDS Species = (Mixed);
                  Label (ABSTRACT) = TEXT*20;
                  Coord2 = COORD 0.000 .. 1.000, 0.000 .. 1.000, ROTATION 2 -> 1;
                  Percent = 0 .. 100 CONSTRAINTS Low: THIS < 50, High: THIS >= 50;
              END T;
            END M.");

        Assert.AreEqual(5, graph.Nodes.Count);
        var species = NodeById(graph, "M.T.Species");
        Assert.AreEqual("domain", species.Kind);
        Assert.AreEqual("Species", species.Name);
        Assert.AreEqual("M.T", species.Group);
        Assert.AreEqual("Enum(Oak, Beech, Pine)", species.Type);
        Assert.AreEqual(0, species.Attributes.Count);
        Assert.AreEqual(5, species.Line, "Line must point at the domain name (zero-based).");

        Assert.AreEqual("Enum(Mixed)", NodeById(graph, "M.T.ForestType").Type);
        Assert.IsTrue(NodeById(graph, "M.T.Label").IsAbstract);
        Assert.AreEqual("Text[20]", NodeById(graph, "M.T.Label").Type);
        Assert.AreEqual("Coord", NodeById(graph, "M.T.Coord2").Type);
        Assert.IsNull(NodeById(graph, "M.T.Coord2").Color);
        Assert.AreEqual(0, species.Constraints.Count);
        CollectionAssert.AreEqual(
            new[] { "Domain Constraint: Low", "Domain Constraint: High" },
            NodeById(graph, "M.T.Percent").Constraints.Select(constraint => $"{constraint.Kind}: {constraint.Name}").ToArray());

        var edge = graph.Edges.Single();
        Assert.AreEqual("inheritance", edge.Kind);
        Assert.AreEqual("M.T.ForestType", edge.Source);
        Assert.AreEqual("M.T.Species", edge.Target);
    }

    [TestMethod]
    public void DomainTypedAttributes_ProduceDomainEdges()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                DOMAIN
                  Species = (Oak, Beech, Pine);
                  Coord2 = COORD 0.000 .. 1.000, 0.000 .. 1.000, ROTATION 2 -> 1;
                  Line = POLYLINE WITH (STRAIGHTS) VERTEX Coord2;
                CLASS Forest =
                  Kind : MANDATORY Species;
                  AnyKind : ALL OF Species;
                  Area : SURFACE WITH (STRAIGHTS) VERTEX Coord2 WITHOUT OVERLAPS > 0.001;
                  Flag : BOOLEAN;
                  Birthday : FORMAT INTERLIS.XMLDate ""1900-1-1"" .. ""2100-12-31"";
                END Forest;
              END T;
            END M.");

        Assert.IsFalse(graph.Nodes.Any(node => node.Kind == "external"), "INTERLIS internal types are neither drawn nor stubbed.");
        Assert.IsTrue(graph.Nodes.All(node => node.Kind != "domain" || node.Type is not null));
        Assert.IsNull(NodeById(graph, "M.T.Forest").Type, "Only domains carry a type.");

        var edges = EdgesOfKind(graph, "domain");
        CollectionAssert.AreEquivalent(
            new[] { "Kind→M.T.Species", "AnyKind→M.T.Species", "Area→M.T.Coord2", "→M.T.Coord2" },
            edges.Select(edge => $"{edge.Name}→{edge.Target}").ToArray());

        var kind = edges.Single(edge => edge.Name == "Kind");
        Assert.AreEqual("M.T.Forest", kind.Source);
        Assert.IsNull(kind.SourceEnd);
        Assert.AreEqual("1", kind.TargetEnd!.Cardinality, "MANDATORY attribute.");
        Assert.AreEqual("0..1", edges.Single(edge => edge.Name == "AnyKind").TargetEnd!.Cardinality);

        var vertex = edges.Single(edge => edge.Name is null);
        Assert.AreEqual("M.T.Line", vertex.Source, "A domain built on another domain links to it.");
        Assert.IsNull(vertex.TargetEnd);
        Assert.AreEqual(0, EdgesOfKind(graph, "inheritance").Count);
    }

    [TestMethod]
    public void ColorMetaAttribute_IsPassedThroughWhenSafe()
    {
        var graph = BuildGraph(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                !!@ geow.uml.color = ""#ffcc00""
                CLASS Safe = END Safe;
                !!@ geow.uml.color = ""red;stroke:black""
                CLASS Unsafe = END Unsafe;
              END T;
            END M.");

        Assert.AreEqual("#ffcc00", NodeById(graph, "M.T.Safe").Color);
        Assert.IsNull(NodeById(graph, "M.T.Unsafe").Color);
    }

    [TestMethod]
    public void Json_UsesCamelCaseAndOmitsNulls()
    {
        var json = Visit(Compile(@"
            INTERLIS 2.4;
            MODEL M (en) AT ""x"" VERSION ""1"" =
              TOPIC T =
                CLASS A = END A;
              END T;
            END M.")).GetGraphJson();

        StringAssert.Contains(json, "{\"id\":\"M\",\"name\":\"M\",\"kind\":\"model\",\"isImported\":false,\"line\":2}", "Null parent and uri must be omitted.");
        StringAssert.Contains(json, "{\"id\":\"M.T\",\"name\":\"T\",\"kind\":\"topic\",\"parent\":\"M\",\"isImported\":false,\"line\":3}");
        StringAssert.Contains(json, "\"kind\":\"class\"");
        StringAssert.Contains(json, "\"isAbstract\":false");
        Assert.IsFalse(json.Contains("\"color\""), "Null members must be omitted.");
        Assert.IsFalse(json.Contains("\"Kind\""), "Property names must be camelCase.");
    }
}
