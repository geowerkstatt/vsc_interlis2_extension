import { Core } from "cytoscape";

/**
 * Scrollbars along the right and bottom edge of the canvas, drawn like the editor's. The track
 * stands for everything there is to see: the visible diagram with the fit padding around it, and
 * the view itself where it is panned beyond. The thumb is the part in view, so it also tells how
 * far the diagram is zoomed in. Dragging a thumb pans, a click beside it pages towards the click.
 * cytoscape has no scrollbars of its own, so they follow every frame it renders.
 */

/** Smallest thumb in pixels, so it stays grabbable when zoomed far in. */
const MIN_THUMB = 20;
/** Share of the view that a click on the track pages by. */
const PAGE = 0.9;

type Axis = "x" | "y";

/** A stretch along one axis, in model coordinates. */
interface Span {
  start: number;
  end: number;
}

interface Bar {
  axis: Axis;
  track: HTMLDivElement;
  thumb: HTMLDivElement;
  /** While the thumb is dragged: the range at the start, kept so the thumb stays under the pointer. */
  drag: { range: Span; pointer: number; viewStart: number } | null;
}

const length = (span: Span) => span.end - span.start;

export function attachScrollbars(cy: Core, container: HTMLElement, padding: number): void {
  const bars = (["x", "y"] as Axis[]).map(createBar);
  let frame = 0;

  function scheduleUpdate(): void {
    if (frame) return;
    frame = requestAnimationFrame(() => {
      frame = 0;
      bars.forEach(update);
    });
  }

  function viewOf(axis: Axis): Span {
    const extent = cy.extent();
    return axis === "x" ? { start: extent.x1, end: extent.x2 } : { start: extent.y1, end: extent.y2 };
  }

  /** The stretch the track stands for; null without anything to show. */
  function rangeOf(axis: Axis, view: Span): Span | null {
    const shown = cy.elements(":visible");
    if (shown.empty()) return null;
    const box = shown.boundingBox({});
    const pad = padding / cy.zoom();
    const [start, end] = axis === "x" ? [box.x1, box.x2] : [box.y1, box.y2];
    return { start: Math.min(start - pad, view.start), end: Math.max(end + pad, view.end) };
  }

  function trackLength(bar: Bar): number {
    return bar.axis === "x" ? bar.track.clientWidth : bar.track.clientHeight;
  }

  /** Pans so the view starts at `start` along the axis of the bar. */
  function scrollTo(axis: Axis, start: number): void {
    const pan = cy.pan();
    cy.pan({ ...pan, [axis]: -start * cy.zoom() });
  }

  function update(bar: Bar): void {
    const view = viewOf(bar.axis);
    const range = bar.drag?.range ?? rangeOf(bar.axis, view);
    // Hidden while everything fits; a pixel of slack keeps rounding from showing a bar.
    bar.track.hidden = !range || length(range) - length(view) < 1 / cy.zoom();
    if (!range || bar.track.hidden) return;
    const total = trackLength(bar);
    const size = Math.max(MIN_THUMB, (total * length(view)) / length(range));
    const offset = ((total - size) * (view.start - range.start)) / (length(range) - length(view));
    bar.thumb.style[bar.axis === "x" ? "left" : "top"] = `${offset}px`;
    bar.thumb.style[bar.axis === "x" ? "width" : "height"] = `${size}px`;
  }

  function createBar(axis: Axis): Bar {
    const track = document.createElement("div");
    track.className = `scrollbar scrollbar-${axis}`;
    track.hidden = true;
    const thumb = document.createElement("div");
    thumb.className = "scrollbar-thumb";
    track.appendChild(thumb);
    container.appendChild(track);
    const bar: Bar = { axis, track, thumb, drag: null };
    const pointerOf = (event: PointerEvent) => (axis === "x" ? event.clientX : event.clientY);

    thumb.addEventListener("pointerdown", (event) => {
      if (event.button !== 0) return;
      event.preventDefault();
      event.stopPropagation();
      const view = viewOf(axis);
      const range = rangeOf(axis, view);
      if (!range) return;
      bar.drag = { range, pointer: pointerOf(event), viewStart: view.start };
      thumb.setPointerCapture(event.pointerId);
      thumb.classList.add("dragging");
    });
    thumb.addEventListener("pointermove", (event) => {
      const drag = bar.drag;
      if (!drag) return;
      const view = viewOf(axis);
      const travel = trackLength(bar) - thumb.getBoundingClientRect()[axis === "x" ? "width" : "height"];
      if (travel <= 0) return;
      const moved = ((pointerOf(event) - drag.pointer) / travel) * (length(drag.range) - length(view));
      const start = Math.min(Math.max(drag.viewStart + moved, drag.range.start), drag.range.end - length(view));
      scrollTo(axis, start);
    });
    const endDrag = () => {
      bar.drag = null;
      thumb.classList.remove("dragging");
      scheduleUpdate();
    };
    thumb.addEventListener("pointerup", endDrag);
    thumb.addEventListener("pointercancel", endDrag);

    track.addEventListener("pointerdown", (event) => {
      if (event.button !== 0 || event.target !== track) return;
      event.preventDefault();
      const view = viewOf(axis);
      const range = rangeOf(axis, view);
      if (!range) return;
      const thumbRect = thumb.getBoundingClientRect();
      const direction = pointerOf(event) < (axis === "x" ? thumbRect.left : thumbRect.top) ? -1 : 1;
      const start = view.start + direction * PAGE * length(view);
      scrollTo(axis, Math.min(Math.max(start, range.start), range.end - length(view)));
    });
    return bar;
  }

  cy.on("render resize", scheduleUpdate);
}
