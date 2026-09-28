import type { EdgeSingular, ElementDefinition, Position, StylesheetJson } from "cytoscape";
import type { FcoseFixedNodeConstraint, FcoseLayoutOptions } from "cytoscape-fcose";
import {
  GraphAttribute,
  GraphConstraint,
  GraphDocument,
  GraphEdge,
  GraphEdgeEnd,
  GraphEdgeKind,
  GraphGroup,
  GraphGroupKind,
  GraphNode,
  GraphRelationship,
} from "./graphDocument";

// Model-to-cytoscape mapping (elements, stylesheet, layout options). Only the class boxes touch the
// DOM: they are built as SVG elements.

export const FONT_FAMILY = '"Segoe UI", "Helvetica Neue", Arial, sans-serif';
const NAME_FONT_SIZE = 13;
const ATTRIBUTE_FONT_SIZE = 12;
const STEREOTYPE_FONT_SIZE = 11;
const ROW_HEIGHT = 17;
const PADDING_X = 10;
const PADDING_Y = 6;
const MIN_NODE_WIDTH = 90;
const MAX_ROW_CHARS = 48;

export const STROKE = "#4b4f6b";
const HIGHLIGHT = "#d9480f";
const TEXT = "#1f2233";
const MUTED = "#55597a";
const EXTERNAL_FILL = "#f7f7f7";
/** Box fills by kind for the file the diagram is about: saturated enough to tell the kinds apart at a glance. */
const OWN_FILL: Record<GraphNode["kind"], string> = {
  class: "#cfe0ff",
  structure: "#ffe2bd",
  view: "#c9efd5",
  association: "#ecd5f6",
  domain: "#fff0b3",
  external: EXTERNAL_FILL,
};
/** Muted fills for the boxes of imported models, so the file's own boxes stand out. */
const IMPORTED_FILL: Record<GraphNode["kind"], string> = {
  class: "#eef0f7",
  structure: "#eef0f7",
  view: "#eef7f0",
  association: "#f7f3ea",
  domain: "#f7f5ea",
  external: EXTERNAL_FILL,
};
export const FRAME_STROKE = "#8b8fb3";
export const FRAME_TINT = "#6366f1";
export const IMPORTED_STROKE = "#a9acbd";
export const IMPORTED_TINT = "#9ca3af";
export const TOPIC_PADDING = 28; // compound padding on every side, also used to project frame bounds
export const MODEL_PADDING = 36;

// fCoSE measures an edge between the two box borders, so these are free spaces between boxes.
const INHERITANCE_EDGE_LENGTH = 40; // subclasses gather tightly around their parent
const RELATION_EDGE_LENGTH = 100; // associations, attribute links, view sources
const FRAME_EDGE_LENGTH = 120; // between two frames related by EXTENDS or IMPORTS
const NODE_REPULSION = 8000;
const NODE_SEPARATION = 100; // spectral phase: separation between unrelated boxes
export const FIT_PADDING = 40;

export const LOOP_DIRECTION_DEG = 45; // self-associations leave through the top-right corner
export const LOOP_SWEEP_DEG = -60; // angle between a loop's two control points
const MAX_BEND = 4000;
const MIN_LOOP_SIZE = 20;

/**
 * A manual nudge of one edge, kept per edge so it survives re-renders. Both forms are relative to
 * the nodes, so a bent edge keeps its shape when the classes move. `line` is cytoscape's
 * unbundled-bezier parametrization: `distance` perpendicular to the source→target line and
 * `weight` along it. `loop` is the direction and size of a self-association's loop.
 */
export type EdgeBend =
  | { kind: "line"; distance: number; weight: number }
  | { kind: "loop"; direction: number; size: number };

function clamp(value: number, min: number, max: number): number {
  if (!Number.isFinite(value)) return min;
  return Math.min(max, Math.max(min, value));
}

/**
 * Bend that pulls the middle of an edge onto `point`. cytoscape draws a single control point as a
 * quadratic, whose midpoint sits halfway between the control point and the chord, so the control
 * point has to be offset twice as far. The perpendicular is (-uy, ux) to match cytoscape's
 * `vectorNormInverse`, which is the direction a positive `control-point-distance` moves along.
 */
export function lineBend(source: Position, target: Position, point: Position): EdgeBend {
  const dx = target.x - source.x;
  const dy = target.y - source.y;
  const length = Math.hypot(dx, dy) || 1;
  const ux = dx / length;
  const uy = dy / length;
  const controlX = 2 * point.x - (source.x + target.x) / 2;
  const controlY = 2 * point.y - (source.y + target.y) / 2;
  const rx = controlX - source.x;
  const ry = controlY - source.y;
  return {
    kind: "line",
    distance: clamp(rx * -uy + ry * ux, -MAX_BEND, MAX_BEND),
    weight: clamp((rx * ux + ry * uy) / length, -1, 2),
  };
}

/**
 * Bend that points a self-loop at `point`. cytoscape places a loop's two control points at
 * 1.4 × size around the node centre, LOOP_SWEEP_DEG apart and bisected by (direction − 90°), so
 * this puts their midpoint under the cursor; the caller refines the size against the drawn curve.
 */
export function loopBend(centre: Position, point: Position): EdgeBend {
  const dx = point.x - centre.x;
  const dy = point.y - centre.y;
  const spread = Math.cos((LOOP_SWEEP_DEG * Math.PI) / 360); // cos(sweep / 2)
  return {
    kind: "loop",
    direction: (Math.atan2(dy, dx) * 180) / Math.PI + 90,
    size: clamp(Math.hypot(dx, dy) / (1.4 * spread), MIN_LOOP_SIZE, MAX_BEND),
  };
}

export type TextMeasurer = (text: string, size: number, weight?: string, style?: string) => number;

/** Rough width used where no canvas is available (headless tests). */
export const approximateTextWidth: TextMeasurer = (text, size, weight = "normal") =>
  text.length * size * (weight === "bold" ? 0.6 : 0.55);

/** Builds a measurer backed by a 2D canvas, falling back to the approximation. */
export function createCanvasTextMeasurer(): TextMeasurer {
  const context = document.createElement("canvas").getContext("2d");
  if (!context) {
    return approximateTextWidth;
  }
  return (text, size, weight = "normal", style = "normal") => {
    context.font = `${style} ${weight} ${size}px ${FONT_FAMILY}`;
    return context.measureText(text).width;
  };
}

/** Where a definition lives, for click-to-reveal. */
export interface SourceLocation {
  /** File the definition was read from; absent when only the requested file is known. */
  uri?: string;
  /** Zero-based line in that file. */
  line: number;
}

export interface ClassNodeData {
  id: string;
  label: string;
  kind: GraphNode["kind"];
  /** Zero-based source line for click-to-reveal; absent for stubs. */
  line?: number;
  /** File of the model the definition belongs to, when the server knows it. */
  uri?: string;
  /** Id of the frame (topic or model) compound node. */
  parent?: string;
  width: number;
  height: number;
  /** Data URI of the rendered UML box. */
  image: string;
  tooltip: string;
}

export interface FrameNodeData {
  id: string;
  label: string;
  kind: GraphGroupKind;
  /** Id of the enclosing model frame; absent for models. */
  parent?: string;
  line?: number;
  uri?: string;
}

export interface RelationEdgeData {
  id: string;
  /** Identity that survives a re-render (unlike the index-based id), so manual bends can be kept. */
  key: string;
  source: string;
  target: string;
  kind: GraphEdgeKind;
  /** True when both ends are frames (topic EXTENDS, model IMPORTS). */
  frame: boolean;
  label: string;
  sourceLabel: string;
  targetLabel: string;
  sourceRel: GraphRelationship | "none";
  targetRel: GraphRelationship | "none";
  /** Ideal free space between the two boxes along this edge. */
  ideal: number;
  /**
   * control-point-step-size for a self-association. cytoscape places a loop's control points
   * 1.4 × this value from the node centre, so it must exceed the box's half diagonal or the loop
   * disappears behind the opaque box image.
   */
  loopSize: number;
  tooltip: string;
  /** Zero-based source line of an association line, for click-to-reveal; absent for other edges. */
  line?: number;
  /** File of the model the association belongs to, when the server knows it. */
  uri?: string;
}

const LOOP_OVERHANG = 70; // how far a self-association loop reaches beyond the box corner

export const SVG_NS = "http://www.w3.org/2000/svg";

export type Attributes = Record<string, string | number>;

export function element<K extends keyof SVGElementTagNameMap>(
  doc: Document,
  tag: K,
  attributes: Attributes,
  text?: string
): SVGElementTagNameMap[K] {
  const el = doc.createElementNS(SVG_NS, tag);
  for (const [name, value] of Object.entries(attributes)) {
    el.setAttribute(name, String(value));
  }
  if (text !== undefined) el.textContent = text;
  return el;
}

function truncate(text: string): string {
  return text.length > MAX_ROW_CHARS ? `${text.slice(0, MAX_ROW_CHARS - 1)}…` : text;
}

/** Which parts of a box are drawn; the filters switch them off per kind. */
export interface BoxDisplay {
  /** The attribute compartment (for a domain: the type it defines). */
  attributes: boolean;
  /** The multiplicity in an attribute row. */
  cardinality: boolean;
  /** The type in an attribute row. */
  types: boolean;
  /** The constraint compartment. */
  constraints: boolean;
}

/** Display per box kind, so classes and structures can show different parts. */
export type BoxDisplayFor = (kind: GraphNode["kind"]) => BoxDisplay;

export function formatAttribute(attribute: GraphAttribute, display: BoxDisplay): string {
  const cardinality = display.cardinality && attribute.cardinality ? ` [${attribute.cardinality}]` : "";
  const type = display.types ? `: ${attribute.type}` : "";
  return `${attribute.name}${cardinality}${type}`;
}

/** Text shown at an edge end: the role above the multiplicity, whichever of the two exist. */
export function formatEnd(end: GraphEdgeEnd | undefined): string {
  if (!end) return "";
  return [end.role, end.cardinality].filter((part): part is string => !!part).join("\n");
}

export interface ClassBox {
  svg: string;
  width: number;
  height: number;
}

function stereotypesOf(node: GraphNode): string[] {
  const stereotypes: string[] = [];
  if (node.isAbstract) stereotypes.push("abstract");
  if (node.kind === "structure" || node.kind === "view" || node.kind === "association" || node.kind === "domain") {
    stereotypes.push(node.kind);
  }
  if (node.kind === "external" || node.isExternal) stereotypes.push("external");
  return stereotypes;
}

function fillOf(node: GraphNode, imported: boolean): string {
  if (node.color) return node.color;
  return (imported ? IMPORTED_FILL : OWN_FILL)[node.kind] ?? EXTERNAL_FILL;
}

export function formatConstraint(constraint: GraphConstraint): string {
  return `${constraint.kind}: ${constraint.name}`;
}

/** Body rows of a box: the attributes, or for a domain the type it defines. */
export function bodyRows(node: GraphNode, display: BoxDisplay): string[] {
  if (!display.attributes) return [];
  if (node.kind === "domain") return node.type ? [node.type] : [];
  return node.attributes.map((attribute) => formatAttribute(attribute, display));
}

/** Rows of the third compartment: the constraints of a class, structure, view or association. */
export function constraintRows(node: GraphNode, display: BoxDisplay): string[] {
  return display.constraints ? node.constraints.map(formatConstraint) : [];
}

/**
 * Renders a UML class box as standalone SVG markup, used as the node's background image: a header
 * with stereotype and name, the attributes, and the constraints as a third compartment, each as far
 * as `display` asks for. Boxes of imported models get the muted fills.
 */
export function buildClassBox(
  node: GraphNode,
  measure: TextMeasurer,
  imported: boolean,
  display: BoxDisplay
): ClassBox {
  const stereotypes = stereotypesOf(node);
  const stereotype = stereotypes.length ? `«${stereotypes.join(", ")}»` : undefined;
  const rows = bodyRows(node, display).map(truncate);
  const constraints = constraintRows(node, display).map(truncate);

  const headerHeight = PADDING_Y * 2 + (stereotype ? STEREOTYPE_FONT_SIZE + 2 : 0) + NAME_FONT_SIZE;
  const compartmentHeight = (count: number) => (count ? PADDING_Y * 2 + count * ROW_HEIGHT : 0);
  const bodyHeight = compartmentHeight(rows.length);
  const constraintsHeight = compartmentHeight(constraints.length);
  const width = Math.ceil(
    Math.max(
      MIN_NODE_WIDTH,
      measure(node.name, NAME_FONT_SIZE, "bold") + PADDING_X * 2,
      stereotype ? measure(stereotype, STEREOTYPE_FONT_SIZE) + PADDING_X * 2 : 0,
      ...rows.map((row) => measure(row, ATTRIBUTE_FONT_SIZE) + PADDING_X * 2),
      ...constraints.map((row) => measure(row, ATTRIBUTE_FONT_SIZE, "normal", "italic") + PADDING_X * 2)
    )
  );
  const height = headerHeight + bodyHeight + constraintsHeight;

  const dash = node.kind === "structure" ? "8 4" : node.kind === "external" ? "3 3" : undefined;
  const nameStyle = node.isAbstract || node.kind === "external" ? "italic" : "normal";

  const svg = element(document, "svg", {
    width,
    height,
    viewBox: `0 0 ${width} ${height}`,
    "font-family": FONT_FAMILY,
  });
  svg.appendChild(
    element(document, "rect", {
      x: 0.6,
      y: 0.6,
      width: width - 1.2,
      height: height - 1.2,
      rx: 4,
      fill: fillOf(node, imported),
      stroke: STROKE,
      "stroke-width": 1.2,
      ...(dash && { "stroke-dasharray": dash }),
    })
  );
  let baseline = PADDING_Y;
  if (stereotype) {
    baseline += STEREOTYPE_FONT_SIZE;
    svg.appendChild(
      element(
        document,
        "text",
        { x: width / 2, y: baseline, "text-anchor": "middle", "font-size": STEREOTYPE_FONT_SIZE, fill: MUTED },
        stereotype
      )
    );
    baseline += 2;
  }
  baseline += NAME_FONT_SIZE;
  svg.appendChild(
    element(
      document,
      "text",
      {
        x: width / 2,
        y: baseline,
        "text-anchor": "middle",
        "font-size": NAME_FONT_SIZE,
        "font-weight": 700,
        "font-style": nameStyle,
        fill: TEXT,
      },
      node.name
    )
  );
  /** A compartment below `top`: separator line, then one row per entry. */
  function compartment(top: number, entries: string[], style: string): void {
    if (!entries.length) return;
    svg.appendChild(
      element(document, "line", { x1: 0, x2: width, y1: top, y2: top, stroke: STROKE, "stroke-width": 1 })
    );
    entries.forEach((row, i) => {
      const y = top + PADDING_Y + (i + 1) * ROW_HEIGHT - 4;
      svg.appendChild(
        element(
          document,
          "text",
          { x: PADDING_X, y, "font-size": ATTRIBUTE_FONT_SIZE, "font-style": style, fill: TEXT },
          row
        )
      );
    });
  }
  compartment(headerHeight, rows, "normal");
  compartment(headerHeight + bodyHeight, constraints, "italic");

  return { svg: new XMLSerializer().serializeToString(svg), width, height };
}

export function toDataUri(svg: string): string {
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

function idealLength(edge: GraphEdge, frame: boolean): number {
  if (frame) return FRAME_EDGE_LENGTH;
  return edge.kind === "inheritance" ? INHERITANCE_EDGE_LENGTH : RELATION_EDGE_LENGTH;
}

function describeEdge(edge: GraphEdge): string {
  switch (edge.kind) {
    case "inheritance":
      return `${edge.source} extends ${edge.target}`;
    case "import":
      return `${edge.source} imports ${edge.target}`;
    case "derivation":
      return `${edge.source} is a ${edge.name ?? "view"} of ${edge.target}`;
    case "reference":
      return `${edge.source}.${edge.name ?? "?"} references ${edge.target}`;
    case "structure":
      return `${edge.source}.${edge.name ?? "?"} contains ${edge.target}`;
    case "domain":
      return edge.name
        ? `${edge.source}.${edge.name} is typed by ${edge.target}`
        : `${edge.source} builds on ${edge.target}`;
    case "role":
      return `Role ${edge.name ?? "?"} of ${edge.source} → ${edge.target}`;
    default:
      // A plain association: its qualified name, like the tooltip of a box; the ends are drawn on the line.
      return edge.group && edge.name ? `${edge.group}.${edge.name}` : edge.name ?? `${edge.source} – ${edge.target}`;
  }
}

/** The parts of a box's node data that depend on how it is drawn; recomputed when the display changes. */
export type ClassBoxData = Pick<ClassNodeData, "width" | "height" | "image" | "tooltip">;

/** Renders the box of `node` and packs it into node data. */
export function buildClassBoxData(
  node: GraphNode,
  measure: TextMeasurer,
  imported: boolean,
  display: BoxDisplay
): ClassBoxData {
  const box = buildClassBox(node, measure, imported, display);
  const tooltip = [node.id];
  if (node.line !== undefined) tooltip.push("Click to jump to the definition");
  return { width: box.width, height: box.height, image: toDataUri(box.svg), tooltip: tooltip.join("\n") };
}

/** Whether the frame `groupId` belongs to an imported model (topics inherit the flag from their model). */
export function isImportedGroup(groups: Map<string, GraphGroup>, groupId: string | undefined): boolean {
  return groupId !== undefined && !!groups.get(groupId)?.isImported;
}

/**
 * Maps the language server's graph JSON to cytoscape elements: models and topics as nested
 * compound nodes, every box as a pre-rendered image node, relations as edges (frame relations
 * connect the compound nodes themselves).
 */
export function buildElements(
  doc: GraphDocument,
  measure: TextMeasurer,
  displayFor: BoxDisplayFor
): ElementDefinition[] {
  const elements: ElementDefinition[] = [];
  const groups = new Map(doc.groups.map((group) => [group.id, group] as const));
  const nodeIds = new Set(doc.nodes.map((node) => node.id));

  /** File of the model a frame belongs to, walking up from a topic. */
  function uriOf(groupId: string | undefined): string | undefined {
    for (let id = groupId; id; ) {
      const group = groups.get(id);
      if (!group) return undefined;
      if (group.uri) return group.uri;
      id = group.parent;
    }
    return undefined;
  }

  for (const group of doc.groups) {
    const data: FrameNodeData = {
      id: group.id,
      label: group.name,
      kind: group.kind,
      parent: group.parent && groups.has(group.parent) ? group.parent : undefined,
      line: group.line,
      uri: uriOf(group.id),
    };
    const classes = ["frame", group.kind];
    if (group.isImported) classes.push("imported");
    elements.push({ group: "nodes", data, classes: classes.join(" ") });
  }

  const halfDiagonals = new Map<string, number>();
  for (const node of doc.nodes) {
    const frame = node.group ? groups.get(node.group) : undefined;
    const imported = isImportedGroup(groups, node.group);
    const box = buildClassBoxData(node, measure, imported, displayFor(node.kind));
    halfDiagonals.set(node.id, Math.hypot(box.width, box.height) / 2);
    const classes = ["class", node.kind];
    if (node.isAbstract) classes.push("abstract");
    if (imported) classes.push("imported");
    const data: ClassNodeData = {
      id: node.id,
      label: node.name,
      kind: node.kind,
      line: node.line,
      uri: uriOf(node.group),
      parent: frame ? frame.id : undefined,
      ...box,
    };
    elements.push({ group: "nodes", data, classes: classes.join(" ") });
  }

  const edgeKeys = new Map<string, number>();
  doc.edges.forEach((edge, index) => {
    const known = (id: string) => nodeIds.has(id) || groups.has(id);
    if (!known(edge.source) || !known(edge.target)) {
      console.warn("Skipping edge with unknown endpoint:", edge);
      return;
    }
    const frame = groups.has(edge.source) || groups.has(edge.target);
    const base = `${edge.kind}|${edge.source}|${edge.target}|${edge.name ?? ""}`;
    const occurrence = edgeKeys.get(base) ?? 0;
    edgeKeys.set(base, occurrence + 1);
    const tooltip = [describeEdge(edge)];
    if (edge.line !== undefined) tooltip.push("Click to jump to the definition");
    const data: RelationEdgeData = {
      id: `edge-${index}`,
      key: occurrence === 0 ? base : `${base}|${occurrence}`,
      source: edge.source,
      target: edge.target,
      kind: edge.kind,
      frame,
      label: edge.name ?? "",
      sourceLabel: formatEnd(edge.sourceEnd),
      targetLabel: formatEnd(edge.targetEnd),
      sourceRel: edge.sourceEnd?.relationship ?? "none",
      targetRel: edge.targetEnd?.relationship ?? "none",
      ideal: idealLength(edge, frame),
      loopSize: ((halfDiagonals.get(edge.source) ?? 0) + LOOP_OVERHANG) / 1.4,
      tooltip: tooltip.join("\n"),
      line: edge.line,
      uri: uriOf(edge.group),
    };
    const classes: string[] = [edge.kind];
    if (frame) classes.push("frame-edge");
    elements.push({ group: "edges", data, classes: classes.join(" ") });
  });

  return elements;
}

/**
 * UML look: boxes are pre-rendered SVG images, topics are dashed and models solid compound frames.
 * Arrow heads: hollow triangle for inheritance, open arrow for every directed link (attribute,
 * role, domain type, view source, import), diamonds for aggregation and composition at the end
 * that owns. Domain links are dotted so they stay in the background of the class structure.
 */
export const STYLESHEET: StylesheetJson = [
  {
    selector: "node.class",
    style: {
      shape: "rectangle",
      width: "data(width)",
      height: "data(height)",
      "background-image": "data(image)",
      "background-fit": "none",
      "background-width": "100%",
      "background-height": "100%",
      "background-opacity": 0,
      "border-width": 0,
      label: "",
    },
  },
  { selector: "node.class.imported", style: { opacity: 0.75 } },
  {
    selector: "node.frame",
    style: {
      shape: "round-rectangle",
      "background-color": FRAME_TINT,
      "background-opacity": 0.06,
      "border-width": 1,
      "border-color": FRAME_STROKE,
      "border-style": "dashed",
      padding: `${TOPIC_PADDING}px`,
      label: "data(label)",
      "font-family": FONT_FAMILY,
      "font-size": 12,
      "font-weight": 600,
      color: MUTED,
      "text-valign": "top",
      "text-halign": "center",
      "text-margin-y": 20,
    },
  },
  {
    selector: "node.frame.model",
    style: {
      "background-opacity": 0.03,
      "border-width": 1.4,
      "border-style": "solid",
      padding: `${MODEL_PADDING}px`,
      "font-size": 13,
      "text-margin-y": 22,
    },
  },
  {
    selector: "node.frame.imported",
    style: {
      "background-color": IMPORTED_TINT,
      "border-color": IMPORTED_STROKE,
      color: "#7a7d90",
    },
  },
  {
    selector: "edge",
    style: {
      "curve-style": "bezier",
      width: 1.3,
      "line-color": STROKE,
      "arrow-scale": 1.3,
      "font-family": FONT_FAMILY,
      "font-size": 11,
      "font-style": "italic",
      color: "#33364a",
      "text-wrap": "wrap",
      "text-background-color": "#ffffff",
      "text-background-opacity": 1,
      "text-background-padding": "2px",
      label: "data(label)",
      "source-label": "data(sourceLabel)",
      "target-label": "data(targetLabel)",
      "source-text-offset": 32,
      "target-text-offset": 32,
    },
  },
  {
    // Self-associations leave through the top-right corner; see RelationEdgeData.loopSize.
    selector: "edge:loop",
    style: {
      "loop-direction": `${LOOP_DIRECTION_DEG}deg`,
      "loop-sweep": `${LOOP_SWEEP_DEG}deg`,
      "control-point-step-size": (edge: EdgeSingular) => edge.data("loopSize") as number,
    },
  },
  {
    selector: "edge.inheritance",
    style: {
      width: 1.5,
      "target-arrow-shape": "triangle",
      "target-arrow-fill": "hollow",
      "target-arrow-color": STROKE,
    },
  },
  {
    selector: "edge.reference, edge.role, edge.structure, edge.domain, edge.derivation, edge.import",
    style: {
      "target-arrow-shape": "vee",
      "target-arrow-color": STROKE,
    },
  },
  {
    selector: "edge.domain",
    style: {
      "line-style": "dotted",
    },
  },
  {
    selector: "edge.derivation, edge.import",
    style: {
      "line-style": "dashed",
      "line-dash-pattern": [6, 4],
    },
  },
  {
    selector: "edge.frame-edge",
    style: {
      width: 1.5,
      "curve-style": "straight",
      "line-style": "dashed",
      "line-dash-pattern": [8, 5],
    },
  },
  {
    selector: 'edge[sourceRel = "aggregation"]',
    style: { "source-arrow-shape": "diamond", "source-arrow-fill": "hollow", "source-arrow-color": STROKE },
  },
  {
    selector: 'edge[sourceRel = "composition"]',
    style: { "source-arrow-shape": "diamond", "source-arrow-fill": "filled", "source-arrow-color": STROKE },
  },
  {
    selector: 'edge[targetRel = "aggregation"]',
    style: { "target-arrow-shape": "diamond", "target-arrow-fill": "hollow", "target-arrow-color": STROKE },
  },
  {
    selector: 'edge[targetRel = "composition"]',
    style: { "target-arrow-shape": "diamond", "target-arrow-fill": "filled", "target-arrow-color": STROKE },
  },
  { selector: ".dim", style: { opacity: 0.2 } },
  {
    selector: "edge.highlight, edge.bending",
    style: { "line-color": HIGHLIGHT, width: 2.2, "target-arrow-color": HIGHLIGHT, "source-arrow-color": HIGHLIGHT },
  },
  { selector: "node:active", style: { "overlay-opacity": 0.08 } },
];

/** Compound padding cytoscape adds around a frame's children, by frame kind. */
export function framePadding(kind: GraphGroupKind | undefined): number {
  return kind === "model" ? MODEL_PADDING : TOPIC_PADDING;
}

export interface RigidBody {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  /** Stays where it is (the element the user just dropped). */
  fixed: boolean;
}

export interface Displacement {
  dx: number;
  dy: number;
}

const MAX_SEPARATION_ITERATIONS = 200;

/**
 * Pushes overlapping rectangles apart until every pair is at least `gap` apart, moving each one as
 * a whole. Pairs are resolved along the axis of smaller overlap; a fixed body never moves and its
 * partner takes the whole shift, otherwise both take half. Used to separate frames after a drag
 * without touching what is inside them.
 */
export function separateRectangles(bodies: RigidBody[], gap: number): Displacement[] {
  const shifts = bodies.map(() => ({ dx: 0, dy: 0 }));
  for (let iteration = 0; iteration < MAX_SEPARATION_ITERATIONS; iteration++) {
    let moved = false;
    for (let i = 0; i < bodies.length; i++) {
      for (let j = i + 1; j < bodies.length; j++) {
        const a = bodies[i];
        const b = bodies[j];
        if (a.fixed && b.fixed) continue;
        const sa = shifts[i];
        const sb = shifts[j];
        const overlapX = Math.min(a.x2 + sa.dx, b.x2 + sb.dx) - Math.max(a.x1 + sa.dx, b.x1 + sb.dx) + gap;
        const overlapY = Math.min(a.y2 + sa.dy, b.y2 + sb.dy) - Math.max(a.y1 + sa.dy, b.y1 + sb.dy) + gap;
        if (overlapX <= 0 || overlapY <= 0) continue;
        moved = true;
        const alongX = overlapX < overlapY;
        const centreA = alongX ? (a.x1 + a.x2) / 2 + sa.dx : (a.y1 + a.y2) / 2 + sa.dy;
        const centreB = alongX ? (b.x1 + b.x2) / 2 + sb.dx : (b.y1 + b.y2) / 2 + sb.dy;
        const direction = centreB >= centreA ? 1 : -1; // b moves in this direction, a the other way
        const amount = alongX ? overlapX : overlapY;
        const shareA = a.fixed ? 0 : b.fixed ? amount : amount / 2;
        const shareB = b.fixed ? 0 : a.fixed ? amount : amount / 2;
        if (alongX) {
          sa.dx -= shareA * direction;
          sb.dx += shareB * direction;
        } else {
          sa.dy -= shareA * direction;
          sb.dy += shareB * direction;
        }
      }
    }
    if (!moved) break;
  }
  return shifts;
}

/** Bounding box of `bodies` after `shifts`, grown by `padding` on every side. */
export function projectFrame(bodies: RigidBody[], shifts: Displacement[], padding: number): RigidBody {
  return {
    x1: Math.min(...bodies.map((b, i) => b.x1 + shifts[i].dx)) - padding,
    y1: Math.min(...bodies.map((b, i) => b.y1 + shifts[i].dy)) - padding,
    x2: Math.max(...bodies.map((b, i) => b.x2 + shifts[i].dx)) + padding,
    y2: Math.max(...bodies.map((b, i) => b.y2 + shifts[i].dy)) + padding,
    fixed: true,
  };
}

export interface LayoutRequest {
  /** Fresh layout from random positions; false continues from the current positions. */
  randomize: boolean;
  /** Boxes that keep their position; fCoSE honours them only when not randomizing. */
  fixedNodeConstraint?: FcoseFixedNodeConstraint[];
}

/**
 * fCoSE options: compound-aware force layout. A fresh layout packs the (mostly disconnected)
 * models; an incremental run after a drag must not, because repacking moves every frame by
 * hundreds of pixels (measured headless on the Test1 model), so it only relaxes from the current
 * positions with the slow "proof" cooling fCoSE requires for randomize: false.
 */
export function buildLayoutOptions(request: LayoutRequest): FcoseLayoutOptions {
  return {
    name: "fcose",
    quality: request.randomize ? "default" : "proof",
    randomize: request.randomize,
    animate: true,
    animationDuration: request.randomize ? 700 : 450,
    animationEasing: "ease-out",
    fit: false,
    padding: FIT_PADDING,
    nodeDimensionsIncludeLabels: false,
    uniformNodeDimensions: false,
    packComponents: request.randomize,
    nodeRepulsion: () => NODE_REPULSION,
    idealEdgeLength: (edge: EdgeSingular) => edge.data("ideal") as number,
    edgeElasticity: () => 0.3,
    nestingFactor: 0.1,
    gravity: 0.25,
    numIter: request.randomize ? 2500 : 300,
    tile: request.randomize,
    tilingPaddingVertical: 30,
    tilingPaddingHorizontal: 30,
    gravityRangeCompound: 1.5,
    gravityCompound: 1,
    gravityRange: 3.8,
    initialEnergyOnIncremental: 0.3,
    nodeSeparation: NODE_SEPARATION,
    fixedNodeConstraint: request.fixedNodeConstraint,
  };
}
