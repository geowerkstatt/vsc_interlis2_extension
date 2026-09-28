import cytoscape, { Core, EdgeSingular, EventObject, NodeCollection, NodeSingular, Position } from "cytoscape";
import fcose from "cytoscape-fcose";
import layoutUtilities from "cytoscape-layout-utilities";
import { buildExportSvg } from "./cytoscapeExport";
import {
  BoxDisplay,
  buildClassBoxData,
  buildElements,
  buildLayoutOptions,
  createCanvasTextMeasurer,
  Displacement,
  EdgeBend,
  FIT_PADDING,
  framePadding,
  isImportedGroup,
  lineBend,
  loopBend,
  LOOP_SWEEP_DEG,
  projectFrame,
  RigidBody,
  separateRectangles,
  SourceLocation,
  STYLESHEET,
  TextMeasurer,
} from "./cytoscapeGraph";
import { GraphDocument, GraphNode, GraphNodeKind } from "./graphDocument";

cytoscape.use(fcose);
cytoscape.use(layoutUtilities); // required by fCoSE's packComponents

const FRAME_GAP = 40; // minimum free space between two frames after a drag or a layout
const CLASS_GAP = 30; // minimum free space between two boxes of a frame after a drag or a layout
const SEPARATION_ANIMATION_MS = 400;
const BEND_CORRECTIONS = 3; // refinement steps per pointer move while bending an edge
const BEND_TOLERANCE = 0.5; // px between the cursor and the curve that count as "good enough"
const BEND_HINT = "Drag to move this arrow, double-click to straighten it";
const MIN_ZOOM = 0.05;
const MAX_ZOOM = 8;
/** Zoom factor of one wheel notch, and of the zoom buttons. */
export const ZOOM_STEP = 1.25;
/** A wheel delta of this size or more (in pixels) is one notch of a mouse; smaller deltas come from a trackpad. */
const WHEEL_NOTCH_PX = 100;

/** The box kinds the filter tree has a node for; external stubs are always shown. */
export type FilterKindKey = "class" | "structure" | "view" | "association" | "domain";

export const FILTER_KIND_KEYS: FilterKindKey[] = ["class", "structure", "view", "association", "domain"];

/** What is shown of the boxes of one kind. */
export interface KindFilter extends BoxDisplay {
  /** The boxes themselves (for associations: the boxes and the lines). */
  visible: boolean;
  /** The arrows touching a box of this kind. */
  connections: boolean;
}

export interface DiagramFilters {
  kinds: Record<FilterKindKey, KindFilter>;
  /** The frames of imported models with everything inside them. */
  importedModels: boolean;
  /**
   * Also the boxes of imported models that no arrow connects to a box of the file itself (and the
   * frames that only hold such boxes); off, only what the file references directly stays.
   */
  indirect: boolean;
  /** IMPORTS arrows between model frames. */
  imports: boolean;
  /** EXTENDS arrows between topic frames. */
  topicExtensions: boolean;
}

/**
 * What the diagram shows at first: the classes, structures and associations of the file with their
 * attributes, like the former Mermaid diagram. Views, domains, constraints, the imported models
 * (and of those, at first only what the file references directly) and the arrows between frames
 * are there to be switched on.
 */
export function defaultFilters(): DiagramFilters {
  const kind = (visible: boolean): KindFilter => ({
    visible,
    attributes: true,
    cardinality: true,
    types: true,
    constraints: false,
    connections: true,
  });
  return {
    kinds: {
      class: kind(true),
      structure: kind(true),
      view: kind(false),
      association: kind(true),
      domain: kind(false),
    },
    importedModels: false,
    indirect: false,
    imports: false,
    topicExtensions: false,
  };
}

function isFilterKind(kind: unknown): kind is FilterKindKey {
  return FILTER_KIND_KEYS.includes(kind as FilterKindKey);
}

/** The display of boxes of `kind`; stubs are drawn like classes. */
export function displayOf(filters: DiagramFilters, kind: GraphNodeKind): BoxDisplay {
  return isFilterKind(kind) ? filters.kinds[kind] : filters.kinds.class;
}

function kindOf(node: NodeSingular): string {
  return String(node.data("kind") ?? "");
}

/** Whether the arrows of a box of this kind are switched on; frames and stubs never block an arrow. */
function connectionsOf(filters: DiagramFilters, node: NodeSingular): boolean {
  const kind = kindOf(node);
  return isFilterKind(kind) ? filters.kinds[kind].connections : true;
}

/** Imported boxes that an arrow connects to a box of the file itself (one hop, in either direction). */
function directlyReferenced(cy: Core): Set<string> {
  const direct = new Set<string>();
  cy.edges().forEach((edge) => {
    if (edge.data("frame")) return;
    const source = edge.source();
    const target = edge.target();
    if (source.hasClass("class") && target.hasClass("class")) {
      if (!source.hasClass("imported") && target.hasClass("imported")) direct.add(target.id());
      if (!target.hasClass("imported") && source.hasClass("imported")) direct.add(source.id());
    }
  });
  return direct;
}

/**
 * Hides what the filters exclude. Hiding uses `display: none`, so a hidden element is left out of
 * the view, of a fresh layout and of the SVG export, while everything keeps its place: showing it
 * again restores the picture. Boxes go first (by kind, then by the imported-model rules), frames
 * follow their content, and an arrow is shown when both of its ends have their connections
 * switched on; cytoscape hides the arrows of hidden boxes by itself.
 */
export function applyFilters(cy: Core, filters: DiagramFilters): void {
  const direct = filters.importedModels && !filters.indirect ? directlyReferenced(cy) : null;
  cy.nodes(".class").forEach((node) => {
    const kind = kindOf(node);
    let shown = isFilterKind(kind) ? filters.kinds[kind].visible : true;
    if (node.hasClass("imported")) {
      shown = shown && filters.importedModels && (direct === null || direct.has(node.id()));
    }
    node.style("display", shown ? "element" : "none");
  });
  cy.nodes(".frame").forEach((frame) => {
    const hasContent = frame.descendants(".class").some((box) => box.style("display") !== "none");
    frame.style("display", hasContent ? "element" : "none");
  });
  cy.edges().forEach((edge) => {
    let shown: boolean;
    if (edge.data("frame")) {
      shown = edge.data("kind") === "import" ? filters.imports : filters.topicExtensions;
    } else {
      shown = connectionsOf(filters, edge.source()) && connectionsOf(filters, edge.target());
      if (edge.data("kind") === "association") {
        // A plain association is a line without a box, so the association node of the tree governs it.
        shown = shown && filters.kinds.association.visible && filters.kinds.association.connections;
      }
    }
    edge.style("display", shown ? "element" : "none");
  });
}

/** Redraws every box of `doc` for the display the filters ask for, keeping its position. */
export function refreshBoxes(cy: Core, doc: GraphDocument, measure: TextMeasurer, filters: DiagramFilters): void {
  const groups = new Map(doc.groups.map((group) => [group.id, group] as const));
  const nodes = new Map(doc.nodes.map((node) => [node.id, node] as const));
  cy.startBatch();
  cy.nodes(".class").forEach((element) => {
    const node: GraphNode | undefined = nodes.get(element.id());
    if (!node) return;
    element.data(buildClassBoxData(node, measure, isImportedGroup(groups, node.group), displayOf(filters, node.kind)));
  });
  cy.endBatch();
}

export interface CytoscapeRendererCallbacks {
  /** Invoked when the user clicks a box, frame or association line that has a known source line. */
  onReveal(location: SourceLocation): void;
}

export interface CytoscapeRenderer {
  /** Renders the server-provided graph JSON. An empty string means the server could not produce one. */
  render(text: string, resetZoom: boolean): void;
  /** Forgets remembered positions and lays the current document out again from scratch. */
  resetLayout(): void;
  /** Runs the layout again from the current positions, so a hand-arranged diagram relaxes without being rebuilt. */
  settleLayout(): Promise<void>;
  /** Standalone SVG clone for download, or null when nothing is rendered. */
  getExportSvg(): SVGSVGElement | null;
  /** Zooms the whole visible diagram into the view, animated. */
  fitToView(): void;
  /** Multiplies the zoom level, keeping the centre of the view where it is. */
  zoomBy(factor: number): void;
  /**
   * Applies the filters: hides boxes, frames and arrows, and redraws the boxes. Nothing is re-arranged
   * except that boxes shown for the first time are placed and overlapping boxes are pushed apart.
   */
  setFilters(filters: DiagramFilters): void;
}

function bodyOf(node: NodeSingular, fixed: boolean): RigidBody {
  const bb = node.boundingBox({ includeLabels: false, includeOverlays: false });
  return { x1: bb.x1, y1: bb.y1, x2: bb.x2, y2: bb.y2, fixed };
}

function leavesOf(cy: Core, node: NodeSingular): NodeCollection {
  return node.isParent() ? node.descendants().filter((n) => !n.isParent()) : cy.collection().union(node);
}

/** The visible children of a frame, or the visible top-level elements for `null`. */
function visibleChildren(cy: Core, parent: NodeSingular | null): NodeSingular[] {
  const children: NodeSingular[] = [];
  (parent ? parent.children() : cy.nodes().orphans()).filter(":visible").forEach((n) => {
    children.push(n);
  });
  return children;
}

function gapFor(siblings: NodeSingular[]): number {
  return siblings.some((n) => n.isParent()) ? FRAME_GAP : CLASS_GAP;
}

function addShift(shifts: Map<string, Displacement>, cy: Core, node: NodeSingular, move: Displacement): void {
  if (Math.abs(move.dx) < 0.5 && Math.abs(move.dy) < 0.5) return;
  leavesOf(cy, node).forEach((leaf) => {
    const own = shifts.get(leaf.id()) ?? { dx: 0, dy: 0 };
    shifts.set(leaf.id(), { dx: own.dx + move.dx, dy: own.dy + move.dy });
  });
}

/**
 * Shifts that push every pair of overlapping siblings apart, in every frame and at the top level,
 * as rigid bodies: a frame's children are separated first, and the frame's outline after that is
 * what its own siblings make room for. fCoSE leaves a few boxes slightly overlapping after a fresh
 * layout (it stops on energy, not on overlap), and boxes change size when the filters change what
 * they show; this pass cleans up either without re-arranging anything else.
 */
export function separationShifts(cy: Core): Map<string, Displacement> {
  const shifts = new Map<string, Displacement>();
  function separate(parent: NodeSingular | null): RigidBody | null {
    const siblings = visibleChildren(cy, parent);
    if (!siblings.length) return null;
    const bodies = siblings.map((n) => (n.isParent() ? separate(n) : null) ?? bodyOf(n, false));
    const moves = separateRectangles(bodies, gapFor(siblings));
    siblings.forEach((sibling, index) => addShift(shifts, cy, sibling, moves[index]));
    // The projected frame yields to its siblings like any other body (projectFrame marks it fixed for settle).
    return parent ? { ...projectFrame(bodies, moves, framePadding(parent.data("kind"))), fixed: false } : null;
  }
  separate(null);
  return shifts;
}

/**
 * Interactive UML class graph drawn by cytoscape.js and laid out by fCoSE. Models and topics are
 * nested compound nodes (draggable frames), boxes are pre-rendered SVG images. fCoSE lays the graph
 * out once, then overlapping boxes are pushed apart; after a drop only rigid boxes move: the dropped
 * element's siblings make room for it, then its frame's siblings make room for the grown frame, and
 * so on up to the models. Arrows can be dragged aside as well; that bend is stored relative to the
 * two boxes, so it follows them.
 */
export function createCytoscapeRenderer(
  container: HTMLDivElement,
  callbacks: CytoscapeRendererCallbacks
): CytoscapeRenderer {
  const measure = createCanvasTextMeasurer();

  const message = document.createElement("div");
  message.className = "graph-message";
  message.hidden = true;
  container.appendChild(message);

  const mount = document.createElement("div");
  mount.className = "cy-mount";
  container.appendChild(mount);

  let lastJson = "";
  let lastDoc: GraphDocument | null = null;
  let fitPending = true;
  let layoutRun = 0;
  let bendingEdge: EdgeSingular | null = null;
  let filters = defaultFilters();
  /** Manual edge routing, kept by stable edge key so it survives re-renders while the file is edited. */
  const rememberedBends = new Map<string, EdgeBend>();
  /**
   * Ids of the boxes that have a place. The others are new in the file or have been hidden by the
   * filters since the last fresh layout; they wait at the origin until they are shown.
   */
  const placed = new Set<string>();

  const cy = cytoscape({
    container: mount,
    style: STYLESHEET,
    minZoom: MIN_ZOOM,
    maxZoom: MAX_ZOOM,
    // Wheel zoom is handled below: cytoscape's own handler samples and clamps the first wheel
    // events and then scales the step down for a notched mouse, which leaves about 3% per notch.
    userZoomingEnabled: false,
    boxSelectionEnabled: false,
    autounselectify: true,
  });

  mount.addEventListener(
    "wheel",
    (event: WheelEvent) => {
      event.preventDefault();
      if (bendingEdge) return;
      // Lines (Firefox) and pages count as notches; pixels are one notch per WHEEL_NOTCH_PX, so a
      // trackpad zooms in proportion to the swipe and a mouse by ZOOM_STEP per notch.
      const notches =
        event.deltaMode === WheelEvent.DOM_DELTA_PIXEL
          ? Math.max(-1, Math.min(1, event.deltaY / WHEEL_NOTCH_PX))
          : Math.sign(event.deltaY);
      if (notches === 0) return;
      const rect = mount.getBoundingClientRect();
      zoomTo(cy.zoom() * Math.pow(ZOOM_STEP, -notches), {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
      });
    },
    { passive: false }
  );

  // "tap" fires for a click without a drag, so dragging an arrow aside does not jump anywhere.
  cy.on("tap", "node.class, node.frame, edge", (event: EventObject) => {
    const element = event.target as NodeSingular | EdgeSingular;
    const line = element.data("line");
    if (typeof line === "number") {
      callbacks.onReveal({ uri: element.data("uri") ?? undefined, line });
    }
  });
  cy.on("dbltap", (event: EventObject) => {
    if (event.target === cy) {
      fitToView(true);
    }
  });
  cy.on("mouseover", "node.class", (event: EventObject) => {
    setHighlight(event.target as NodeSingular);
    mount.title = (event.target as NodeSingular).data("tooltip") ?? "";
  });
  cy.on("mouseout", "node.class", () => {
    setHighlight(null);
    mount.title = "";
  });
  cy.on("mouseover", "edge", (event: EventObject) => {
    const tooltip = String(event.target.data("tooltip") ?? "");
    mount.title = tooltip ? `${tooltip}\n${BEND_HINT}` : BEND_HINT;
    mount.style.cursor = typeof event.target.data("line") === "number" ? "pointer" : "grab";
  });
  cy.on("mouseout", "edge", () => {
    mount.title = "";
    mount.style.cursor = "";
  });
  // Edges are not grabbable in cytoscape, so a drag that starts on one is turned into a bend here.
  cy.on("tapstart", "edge", (event: EventObject) => {
    const mouse = event.originalEvent as MouseEvent | undefined;
    if (mouse && typeof mouse.button === "number" && mouse.button !== 0) return;
    bendingEdge = event.target as EdgeSingular;
    bendingEdge.addClass("bending");
    cy.userPanningEnabled(false); // the gesture bends the arrow instead of panning the view
  });
  cy.on("tapdrag", (event: EventObject) => {
    if (bendingEdge) bendEdgeTowards(bendingEdge, event.position);
  });
  cy.on("tapend", stopBending);
  window.addEventListener("mouseup", stopBending); // the pointer may be released outside the canvas
  cy.on("dbltap", "edge", (event: EventObject) => {
    clearBend(event.target as EdgeSingular);
  });
  // "dragfreeon" fires only for the node the user grabbed; plain "dragfree" also fires for every
  // box carried along inside a dragged frame, which ran the settling once per box.
  cy.on("dragfreeon", "node", (event: EventObject) => {
    settle(event.target as NodeSingular);
  });

  function showMessage(text: string): void {
    message.textContent = text;
    message.hidden = false;
    mount.hidden = true;
  }

  function hideMessage(): void {
    message.hidden = true;
    mount.hidden = false;
  }

  function setHighlight(node: NodeSingular | null): void {
    cy.elements().removeClass("dim highlight");
    if (!node) return;
    const keep = node.closedNeighborhood().union(cy.nodes(".frame"));
    cy.elements().not(keep).addClass("dim");
    node.connectedEdges().addClass("highlight");
  }

  function fitToView(animate: boolean): void {
    const shown = cy.elements(":visible");
    if (shown.empty()) return;
    if (animate) {
      cy.animate({ fit: { eles: shown, padding: FIT_PADDING }, duration: 350, easing: "ease-out" });
    } else {
      cy.fit(shown, FIT_PADDING);
    }
  }

  /** Sets the zoom level, clamped, keeping the given point of the canvas (in pixels) fixed. */
  function zoomTo(level: number, renderedPosition: Position): void {
    cy.zoom({ level: Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, level)), renderedPosition });
  }

  function zoomBy(factor: number): void {
    zoomTo(cy.zoom() * factor, { x: cy.width() / 2, y: cy.height() / 2 });
  }

  function displayFor(kind: GraphNodeKind): BoxDisplay {
    return displayOf(filters, kind);
  }

  function stopBending(): void {
    if (!bendingEdge) return;
    bendingEdge.removeClass("bending");
    bendingEdge = null;
    cy.userPanningEnabled(true);
  }

  function bendKey(edge: EdgeSingular): string {
    return String(edge.data("key") ?? edge.id());
  }

  function applyBend(edge: EdgeSingular, bend: EdgeBend): void {
    edge.style(
      bend.kind === "loop"
        ? {
            "curve-style": "unbundled-bezier",
            "control-point-distances": `${bend.size}`,
            "control-point-weights": "0.5",
            "loop-direction": `${bend.direction}deg`,
            "loop-sweep": `${LOOP_SWEEP_DEG}deg`,
          }
        : {
            "curve-style": "unbundled-bezier",
            "control-point-distances": `${bend.distance}`,
            "control-point-weights": `${bend.weight}`,
          }
    );
  }

  /** Drops a manual bend, so the edge is routed by the stylesheet again. */
  function clearBend(edge: EdgeSingular): void {
    edge.removeStyle("curve-style control-point-distances control-point-weights loop-direction loop-sweep");
    rememberedBends.delete(bendKey(edge));
  }

  /** The point on the drawn curve that should end up under the cursor; null without a renderer. */
  function curveMidpoint(edge: EdgeSingular): Position | null {
    try {
      const midpoint = edge.midpoint();
      return midpoint && Number.isFinite(midpoint.x) && Number.isFinite(midpoint.y) ? midpoint : null;
    } catch {
      return null;
    }
  }

  function distanceToCurve(edge: EdgeSingular, point: Position): number {
    const midpoint = curveMidpoint(edge);
    return midpoint ? Math.hypot(point.x - midpoint.x, point.y - midpoint.y) : Number.POSITIVE_INFINITY;
  }

  /** One Newton step towards `point`, using how far the curve midpoint still misses it. */
  function correctBend(edge: EdgeSingular, bend: EdgeBend, point: Position): EdgeBend | null {
    const midpoint = curveMidpoint(edge);
    if (!midpoint) return null;
    const rx = point.x - midpoint.x;
    const ry = point.y - midpoint.y;

    if (bend.kind === "loop") {
      // The direction already points at the cursor, so only the size is off.
      const centre = edge.source().position();
      const wanted = Math.hypot(point.x - centre.x, point.y - centre.y);
      const current = Math.hypot(midpoint.x - centre.x, midpoint.y - centre.y);
      return current < 1 || wanted < 1 ? null : { ...bend, size: bend.size * (wanted / current) };
    }

    const source = edge.sourceEndpoint();
    const target = edge.targetEndpoint();
    const dx = target.x - source.x;
    const dy = target.y - source.y;
    const length = Math.hypot(dx, dy) || 1;
    const ux = dx / length;
    const uy = dy / length;
    // The curve midpoint moves half as far as the control point does.
    return {
      kind: "line",
      distance: bend.distance + 2 * (rx * -uy + ry * ux),
      weight: bend.weight + (2 * (rx * ux + ry * uy)) / length,
    };
  }

  /**
   * Routes `edge` so its middle sits under `point`. The first estimate assumes the edge still
   * leaves the boxes where it does now, but the endpoints slide along the borders as the curve
   * bends, so the result is corrected against the curve cytoscape actually drew.
   */
  function bendEdgeTowards(edge: EdgeSingular, point: Position): void {
    const isLoop = edge.source().same(edge.target());
    let bend = isLoop
      ? loopBend(edge.source().position(), point)
      : lineBend(edge.sourceEndpoint(), edge.targetEndpoint(), point);
    applyBend(edge, bend);

    let missed = distanceToCurve(edge, point);
    for (let step = 0; step < BEND_CORRECTIONS && missed > BEND_TOLERANCE; step++) {
      const corrected = correctBend(edge, bend, point);
      if (!corrected) break;
      applyBend(edge, corrected);
      const stillMissed = distanceToCurve(edge, point);
      if (!(stillMissed < missed)) {
        applyBend(edge, bend); // the step made it worse: keep the better shape
        break;
      }
      bend = corrected;
      missed = stillMissed;
    }
    rememberedBends.set(bendKey(edge), bend);
  }

  /**
   * Runs fCoSE on what is visible, then the overlap pass. The `fixed` boxes stay where they are, and
   * fCoSE is skipped when no other box is left to move.
   */
  async function runLayout(randomize: boolean, fixed: NodeCollection = cy.collection()): Promise<void> {
    const run = ++layoutRun;
    // Only what is on screen takes part, so a layout after filtering arranges what the user sees.
    const shown = cy.elements(":visible");
    const boxes = shown.nodes(".class");
    boxes.forEach((n) => {
      placed.add(n.id());
    });
    if (boxes.not(fixed).nonempty()) {
      const layout = shown.layout(
        buildLayoutOptions({
          randomize,
          fixedNodeConstraint: fixed.map((n) => ({ nodeId: n.id(), position: { ...n.position() } })),
        })
      );
      const finished = layout.promiseOn("layoutstop");
      layout.run();
      await finished;
      if (run !== layoutRun) return;
    }
    await applyShifts(separationShifts(cy));
    if (run === layoutRun && fitPending) {
      fitPending = false;
      fitToView(true);
    }
  }

  /**
   * Places the visible boxes that have no place yet around the placed ones, which stay where they
   * are, so neither an edit nor a filter re-arranges what the user sees; without a placed box, lays
   * everything out afresh.
   */
  function placeNewBoxes(): Promise<void> {
    const fixed = cy.nodes(".class:visible").filter((n) => placed.has(n.id()));
    return runLayout(fixed.empty(), fixed);
  }

  /** Moves the boxes by their shifts as one animation; resolves once they have arrived. */
  function applyShifts(shifts: Map<string, Displacement>): Promise<void> {
    const animations: Promise<unknown>[] = [];
    shifts.forEach((shift, id) => {
      const leaf = cy.getElementById(id);
      const position = leaf.position();
      animations.push(
        leaf
          .animation({
            position: { x: position.x + shift.dx, y: position.y + shift.dy },
            duration: SEPARATION_ANIMATION_MS,
            easing: "ease-out",
          })
          .play()
          .promise()
      );
    });
    return Promise.all(animations).then(() => undefined);
  }

  /**
   * After a drop: the dropped element stays put and its visible siblings yield as rigid bodies
   * where they now overlap it (fCoSE would also drag unrelated neighbours around through compound
   * gravity and long-range repulsion). The enclosing frame then has a new outline, so its siblings
   * yield to that in turn, up to the models. All shifts play as one animation; a moved frame keeps
   * its interior exactly as it is.
   */
  function settle(dropped: NodeSingular): void {
    const shifts = new Map<string, Displacement>();
    let anchor: NodeSingular = dropped;
    let anchorBody = bodyOf(dropped, true);
    for (;;) {
      const parent = anchor.parent();
      const parentNode = parent.empty() ? null : (parent[0] as NodeSingular);
      const siblings = visibleChildren(cy, parentNode);
      const bodies = siblings.map((n) => (n.same(anchor) ? anchorBody : bodyOf(n, false)));
      const moves = separateRectangles(bodies, gapFor(siblings));
      siblings.forEach((sibling, index) => addShift(shifts, cy, sibling, moves[index]));
      if (!parentNode) break;
      // The frame after its children moved (cytoscape pads the children's bounds on every side).
      anchorBody = projectFrame(bodies, moves, framePadding(parentNode.data("kind")));
      anchor = parentNode;
    }

    void applyShifts(shifts);
  }

  function render(text: string, resetZoom: boolean): void {
    if (!text) {
      showMessage("Could not load diagram.");
      return;
    }

    let doc: GraphDocument;
    try {
      doc = JSON.parse(text) as GraphDocument;
    } catch (err: any) {
      console.error("Render error:", err);
      showMessage(`Error rendering diagram: ${err.message}`);
      return;
    }

    hideMessage();
    stopBending(); // the edge being dragged is about to be replaced
    lastJson = text;
    lastDoc = doc;
    cy.resize(); // the mount may have been hidden (zero size) until now

    const previous = new Map<string, Position>();
    cy.nodes(".class").forEach((n) => {
      if (placed.has(n.id())) previous.set(n.id(), { ...n.position() });
    });
    placed.clear();
    cy.startBatch();
    cy.elements().remove();
    cy.add(buildElements(doc, measure, displayFor));
    cy.nodes(".class").forEach((n) => {
      const position = previous.get(n.id());
      if (position) {
        n.position(position);
        placed.add(n.id());
      }
    });
    cy.endBatch();
    cy.edges().forEach((edge) => {
      const bend = rememberedBends.get(bendKey(edge));
      if (bend) applyBend(edge, bend);
    });
    applyFilters(cy, filters);

    fitPending = fitPending || resetZoom;
    void placeNewBoxes();
  }

  function getExportSvg(): SVGSVGElement | null {
    return buildExportSvg(cy);
  }

  /** Relaxes the current arrangement: fCoSE continues from where the boxes are, keeping bends and the view. */
  async function settleLayout(): Promise<void> {
    if (!lastJson) return;
    await runLayout(false);
  }

  /** Fresh fCoSE run from random positions, dropping manual bends too, then fit. */
  function resetLayout(): void {
    if (!lastJson) return;
    placed.clear();
    rememberedBends.clear();
    render(lastJson, true);
  }

  // No re-layout: a filter change stays reversible, and manual moves survive it.
  function setFilters(next: DiagramFilters): void {
    filters = next;
    if (lastDoc) refreshBoxes(cy, lastDoc, measure, filters);
    applyFilters(cy, filters);
    void placeNewBoxes();
  }

  return {
    render,
    resetLayout,
    settleLayout,
    setFilters,
    getExportSvg,
    fitToView: () => fitToView(true),
    zoomBy,
  };
}
