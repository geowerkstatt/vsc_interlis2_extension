import type { Core, EdgeSingular, NodeSingular, Position } from "cytoscape";
import {
  element,
  FONT_FAMILY,
  FRAME_STROKE,
  FRAME_TINT,
  IMPORTED_STROKE,
  IMPORTED_TINT,
  STROKE,
  SVG_NS,
} from "./cytoscapeGraph";
import type { Attributes, RelationEdgeData } from "./cytoscapeGraph";

/**
 * Builds an editable SVG of the current cytoscape graph from its model (node positions, compound
 * bounding boxes, edge endpoints and control points) instead of capturing the canvas. The result
 * uses plain shapes and text with presentation attributes, so it opens and edits cleanly in vector
 * editors such as Inkscape. (cytoscape-svg was tried first, but its canvas2svg core rasterizes the
 * class boxes into PNG images.)
 */

const MARGIN = 40;
const ARROW_LENGTH = 14;
const ARROW_HALF_WIDTH = 7;
const LABEL_OFFSET = 10;
const LABEL_LINE_HEIGHT = 12;
const END_LABEL_DISTANCE = 32;
const FRAME_LABEL_HEIGHT = 18;
const BOX_IMAGE_PREFIX = "data:image/svg+xml;utf8,";

type ArrowShape = "triangle" | "diamond" | "vee";

function unit(from: Position, to: Position): Position {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const length = Math.hypot(dx, dy) || 1;
  return { x: dx / length, y: dy / length };
}

/** Arrow head with its tip at `tip`, pointing along `direction`: UML triangle, diamond or open arrow. */
function arrowHead(doc: Document, tip: Position, direction: Position, shape: ArrowShape, filled: boolean) {
  const back = { x: tip.x - direction.x * ARROW_LENGTH, y: tip.y - direction.y * ARROW_LENGTH };
  const mid = { x: tip.x - direction.x * (ARROW_LENGTH / 2), y: tip.y - direction.y * (ARROW_LENGTH / 2) };
  const normal = { x: -direction.y * ARROW_HALF_WIDTH, y: direction.x * ARROW_HALF_WIDTH };
  const p = (point: Position) => `${round(point.x)},${round(point.y)}`;
  if (shape === "vee") {
    return element(doc, "polyline", {
      points: [{ x: back.x + normal.x, y: back.y + normal.y }, tip, { x: back.x - normal.x, y: back.y - normal.y }]
        .map(p)
        .join(" "),
      fill: "none",
      stroke: STROKE,
      "stroke-width": 1.2,
      "stroke-linejoin": "round",
    });
  }
  const points =
    shape === "triangle"
      ? [tip, { x: back.x + normal.x, y: back.y + normal.y }, { x: back.x - normal.x, y: back.y - normal.y }]
      : [tip, { x: mid.x + normal.x, y: mid.y + normal.y }, back, { x: mid.x - normal.x, y: mid.y - normal.y }];
  return element(doc, "polygon", {
    points: points.map(p).join(" "),
    fill: filled ? STROKE : "#ffffff",
    stroke: STROKE,
    "stroke-width": 1.2,
    "stroke-linejoin": "round",
  });
}

function round(value: number): number {
  return Math.round(value * 100) / 100;
}

/** Centred label; several lines (role above multiplicity) become tspans. */
function label(doc: Document, at: Position, text: string, italic: boolean) {
  const lines = text.split("\n");
  const el = element(doc, "text", {
    x: round(at.x),
    y: round(at.y + 4 - ((lines.length - 1) * LABEL_LINE_HEIGHT) / 2),
    "font-size": 11,
    "font-style": italic ? "italic" : "normal",
    fill: "#33364a",
    "text-anchor": "middle",
    "paint-order": "stroke",
    stroke: "#ffffff",
    "stroke-width": 3,
    "stroke-linejoin": "round",
  });
  lines.forEach((line, index) => {
    el.appendChild(element(doc, "tspan", { x: round(at.x), dy: index === 0 ? 0 : LABEL_LINE_HEIGHT }, line));
  });
  return el;
}

/** Straight line or quadratic/cubic bezier through cytoscape's rendered control points. */
function edgePath(source: Position, controls: Position[], target: Position): string {
  const p = (point: Position) => `${round(point.x)},${round(point.y)}`;
  if (controls.length === 0) return `M${p(source)} L${p(target)}`;
  if (controls.length === 1) return `M${p(source)} Q${p(controls[0])} ${p(target)}`;
  if (controls.length === 2) return `M${p(source)} C${p(controls[0])} ${p(controls[1])} ${p(target)}`;
  // Several control points (unbundled bezier): chain quadratic segments through their midpoints.
  let path = `M${p(source)}`;
  for (let i = 0; i < controls.length - 1; i++) {
    const next = { x: (controls[i].x + controls[i + 1].x) / 2, y: (controls[i].y + controls[i + 1].y) / 2 };
    path += ` Q${p(controls[i])} ${p(next)}`;
  }
  return `${path} Q${p(controls[controls.length - 1])} ${p(target)}`;
}

/** Point on the path a little way in from an end, used to place end labels. */
function alongEdge(end: Position, towards: Position, distance: number): Position {
  const direction = unit(end, towards);
  const normal = { x: -direction.y, y: direction.x };
  return {
    x: end.x + direction.x * distance + normal.x * LABEL_OFFSET,
    y: end.y + direction.y * distance + normal.y * LABEL_OFFSET,
  };
}

function safeControlPoints(edge: EdgeSingular): Position[] {
  try {
    const points = edge.controlPoints();
    return Array.isArray(points) ? points.filter((p) => Number.isFinite(p.x) && Number.isFinite(p.y)) : [];
  } catch {
    return [];
  }
}

function safeEndpoint(edge: EdgeSingular, end: "source" | "target"): Position {
  try {
    const point = end === "source" ? edge.sourceEndpoint() : edge.targetEndpoint();
    if (point && Number.isFinite(point.x) && Number.isFinite(point.y)) return point;
  } catch {
    // headless: no rendered geometry
  }
  return edge[end]().position();
}

function targetArrow(kind: RelationEdgeData["kind"]): ArrowShape | null {
  switch (kind) {
    case "inheritance":
      return "triangle";
    case "reference":
    case "role":
    case "structure":
    case "domain":
    case "derivation":
    case "import":
      return "vee";
    default:
      return null;
  }
}

function dashOf(data: RelationEdgeData): string | null {
  if (data.frame) return "8 5";
  if (data.kind === "derivation" || data.kind === "import") return "6 4";
  if (data.kind === "domain") return "2 3";
  return null;
}

export function buildExportSvg(cy: Core): SVGSVGElement | null {
  if (cy.elements(":visible").empty()) return null;
  const doc = document.implementation.createDocument(SVG_NS, "svg", null);
  const svg = doc.documentElement as unknown as SVGSVGElement;

  // ":visible" everywhere below, so the file shows exactly what the panel shows.
  const bounds = cy.elements(":visible").boundingBox({ includeLabels: true, includeOverlays: false });
  const width = Math.ceil(bounds.w + MARGIN * 2);
  const height = Math.ceil(bounds.h + MARGIN * 2);
  // createDocument already declares the SVG namespace on the root; a second xmlns would break parsers.
  svg.setAttribute("width", String(width));
  svg.setAttribute("height", String(height));
  svg.setAttribute("viewBox", `${round(bounds.x1 - MARGIN)} ${round(bounds.y1 - MARGIN)} ${width} ${height}`);
  svg.setAttribute("font-family", FONT_FAMILY);
  svg.appendChild(
    element(doc, "rect", { x: round(bounds.x1 - MARGIN), y: round(bounds.y1 - MARGIN), width, height, fill: "#ffffff" })
  );

  // Frames: outer models first so nested topics are drawn on top of them.
  const frames = element(doc, "g", { id: "frames" });
  const frameNodes: NodeSingular[] = [];
  cy.nodes(".frame:visible").forEach((frame: NodeSingular) => {
    frameNodes.push(frame);
  });
  frameNodes.sort((a, b) => a.ancestors().length - b.ancestors().length);
  for (const frame of frameNodes) {
    const isModel = frame.hasClass("model");
    const imported = frame.hasClass("imported");
    const bb = frame.boundingBox({ includeLabels: false, includeOverlays: false });
    const group = element(doc, "g", { id: frame.id() });
    const rect: Attributes = {
      x: round(bb.x1),
      y: round(bb.y1),
      width: round(bb.w),
      height: round(bb.h),
      rx: 8,
      fill: imported ? IMPORTED_TINT : FRAME_TINT,
      "fill-opacity": isModel ? 0.03 : 0.06,
      stroke: imported ? IMPORTED_STROKE : FRAME_STROKE,
      "stroke-width": isModel ? 1.4 : 1,
    };
    if (!isModel) rect["stroke-dasharray"] = "6 4";
    group.appendChild(element(doc, "rect", rect));
    group.appendChild(
      element(
        doc,
        "text",
        {
          x: round(bb.x1 + bb.w / 2),
          y: round(bb.y1 + FRAME_LABEL_HEIGHT + (isModel ? 2 : 0)),
          "font-size": isModel ? 13 : 12,
          "font-weight": 600,
          fill: imported ? "#7a7d90" : "#55597a",
          "letter-spacing": "0.04em",
          "text-anchor": "middle",
        },
        String(frame.data("label") ?? "")
      )
    );
    frames.appendChild(group);
  }
  svg.appendChild(frames);

  // Edges: path, arrow heads, labels.
  const edges = element(doc, "g", { id: "relations" });
  cy.edges(":visible").forEach((edge: EdgeSingular) => {
    const data = edge.data() as RelationEdgeData;
    const source = safeEndpoint(edge, "source");
    const target = safeEndpoint(edge, "target");
    const controls = safeControlPoints(edge);
    const group = element(doc, "g", { id: data.id });
    const path: Attributes = {
      d: edgePath(source, controls, target),
      fill: "none",
      stroke: STROKE,
      "stroke-width": data.kind === "inheritance" || data.frame ? 1.5 : 1.3,
    };
    const dash = dashOf(data);
    if (dash) path["stroke-dasharray"] = dash;
    group.appendChild(element(doc, "path", path));

    const towardsTarget = unit(controls.length ? controls[controls.length - 1] : source, target);
    const towardsSource = unit(controls.length ? controls[0] : target, source);
    const arrow = targetArrow(data.kind);
    if (arrow) {
      group.appendChild(arrowHead(doc, target, towardsTarget, arrow, false));
    }
    if (data.targetRel === "aggregation" || data.targetRel === "composition") {
      group.appendChild(arrowHead(doc, target, towardsTarget, "diamond", data.targetRel === "composition"));
    }
    if (data.sourceRel === "aggregation" || data.sourceRel === "composition") {
      group.appendChild(arrowHead(doc, source, towardsSource, "diamond", data.sourceRel === "composition"));
    }

    if (data.label) {
      let midpoint: Position;
      try {
        midpoint = edge.midpoint();
      } catch {
        midpoint = { x: (source.x + target.x) / 2, y: (source.y + target.y) / 2 };
      }
      const normal = { x: -towardsTarget.y, y: towardsTarget.x };
      group.appendChild(
        label(
          doc,
          { x: midpoint.x - normal.x * LABEL_OFFSET, y: midpoint.y - normal.y * LABEL_OFFSET },
          data.label,
          true
        )
      );
    }
    if (data.sourceLabel) {
      group.appendChild(
        label(doc, alongEdge(source, controls[0] ?? target, END_LABEL_DISTANCE), data.sourceLabel, false)
      );
    }
    if (data.targetLabel) {
      const last = controls[controls.length - 1] ?? source;
      group.appendChild(label(doc, alongEdge(target, last, END_LABEL_DISTANCE), data.targetLabel, false));
    }
    edges.appendChild(group);
  });
  svg.appendChild(edges);

  // Boxes: the pre-rendered markup, placed at the node position.
  const classes = element(doc, "g", { id: "classes" });
  cy.nodes(".class:visible").forEach((node: NodeSingular) => {
    const image = String(node.data("image") ?? "");
    if (!image.startsWith(BOX_IMAGE_PREFIX)) return;
    const box = new DOMParser().parseFromString(
      decodeURIComponent(image.slice(BOX_IMAGE_PREFIX.length)),
      "image/svg+xml"
    );
    const position = node.position();
    const group = element(doc, "g", {
      id: node.id(),
      transform: `translate(${round(position.x - node.width() / 2)} ${round(position.y - node.height() / 2)})`,
    });
    if (node.hasClass("imported")) group.setAttribute("opacity", "0.75");
    Array.from(box.documentElement.childNodes).forEach((child) => group.appendChild(doc.importNode(child, true)));
    classes.appendChild(group);
  });
  svg.appendChild(classes);

  return svg;
}
