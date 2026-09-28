// Graph JSON contract; mirrors GraphDocument.cs in the language server.

export interface GraphDocument {
  groups: GraphGroup[];
  nodes: GraphNode[];
  edges: GraphEdge[];
}

export type GraphGroupKind = "model" | "topic";

/** A model or topic frame. Only frames with content are part of the document. */
export interface GraphGroup {
  id: string;
  name: string;
  kind: GraphGroupKind;
  /** Id of the model frame a topic belongs to; absent for models. */
  parent?: string;
  /** True for models read from another file than the one the graph was requested for (and their topics). */
  isImported: boolean;
  /** Location of the model's file when known; topics share their model's file. */
  uri?: string;
  /** Zero-based line of the name in that file. */
  line?: number;
}

export type GraphNodeKind = "class" | "structure" | "view" | "association" | "domain" | "external";

export interface GraphNode {
  id: string;
  name: string;
  /** Id of the containing topic or, for model-level definitions, model; absent for external stubs. */
  group?: string;
  kind: GraphNodeKind;
  isAbstract: boolean;
  isExternal: boolean;
  color?: string;
  /** Zero-based line of the definition in its model's file. */
  line?: number;
  /** The type a domain defines, formatted like an attribute type; absent for every other kind. */
  type?: string;
  /** Own attributes; empty for domains. */
  attributes: GraphAttribute[];
  /** Own constraints and those of `CONSTRAINTS OF` blocks targeting the definition, or the value restrictions of a domain; empty for stubs. */
  constraints: GraphConstraint[];
}

export interface GraphAttribute {
  name: string;
  type: string;
  /** UML multiplicity such as `0..*`; absent for the implicit `1`. */
  cardinality?: string;
}

export interface GraphConstraint {
  /** E.g. `Set Constraint` or `Unique Constraint`. */
  kind: string;
  /** Declared name, or the `Constraint<n>` the compiler synthesizes for an anonymous one. */
  name: string;
}

export type GraphRelationship = "association" | "aggregation" | "composition";

export interface GraphEdgeEnd {
  /** Role name at this end; absent when the edge name already names it or the end has no role. */
  role?: string;
  /** Multiplicity at this end; absent when the end has none. */
  cardinality?: string;
  /** The diamond is drawn at this end for aggregation and composition. */
  relationship: GraphRelationship;
}

export type GraphEdgeKind =
  /** Source EXTENDS target; between boxes or between topic frames. */
  | "inheritance"
  /** Plain binary association drawn as one line; both ends carry role, multiplicity and relationship. */
  | "association"
  /** From an association box to the class one of its roles points at; the name is the role. */
  | "role"
  /** A REFERENCE TO attribute of the source pointing at the target. */
  | "reference"
  /** An attribute of the source whose values are instances of the structure target; composition at the source. */
  | "structure"
  /** An attribute of the source typed by the domain target, or a domain whose type builds on the domain target. */
  | "domain"
  /** The view source is formed from the target; the name is the formation kind. */
  | "derivation"
  /** The model frame source IMPORTS the model frame target. */
  | "import";

export interface GraphEdge {
  kind: GraphEdgeKind;
  source: string;
  target: string;
  name?: string;
  sourceEnd?: GraphEdgeEnd;
  targetEnd?: GraphEdgeEnd;
  /** For an association line: id of the topic or model it is declared in. */
  group?: string;
  /** For an association line: zero-based line of its declaration. */
  line?: number;
}
