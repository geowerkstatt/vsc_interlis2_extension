/* eslint-env browser */
/* global mermaid, acquireVsCodeApi */

const vscode = acquireVsCodeApi();

const container = document.getElementById("mermaid-graph");
let mermaidInitialized = false;
let renderTimer;
let lastMermaidCode = "";

function initializeMermaid() {
  if (mermaidInitialized) {
    return;
  }

  mermaid.initialize({
    startOnLoad: false,
    theme: "forest", // Or 'dark', 'neutral', 'default'
    themeCSS: `
      .node rect {
        rx: 8;
        ry: 8;
      }
      .edgeLabel text {
        font-size: 12px;
      }
      svg {
        max-width: 100%;
        height: auto;
      }
    `,
    flowchart: {
      curve: "basis",
      nodeSpacing: 50,
      rankSpacing: 50,
      useMaxWidth: true,
      wrappingWidth: 140,
    },
    securityLevel: "strict", // Or 'loose', 'antiscript'
  });
  mermaidInitialized = true;

  console.log("Mermaid Initialized");
}

window.addEventListener("message", (event) => {
  const message = event.data;
  if (message.type === "update") {
    console.log("Received update message");
    clearTimeout(renderTimer);
    lastMermaidCode = message.text;
    initializeMermaid();
    renderTimer = setTimeout(() => renderDiagram(message.text), 150); // Debounce
  }
});

//zoom state
let originalViewBox = null; // Stores { w, h } from the initial render
let currentViewBox = null; // Stores current { x, y, w, h }
let zoomLevel = 2;

//pan state
let isPanning = false;
let panStartX = 0;
let panStartY = 0;

async function renderDiagram(code) {
  if (!code) {
    console.log("No diagram code present in message.");
    container.innerHTML = "<div>Could not load diagram.</div>";
    resetPanZoomState();
    return;
  }
  initializeMermaid();

  console.log("Rendering diagram...");
  container.innerHTML = "Rendering...";

  try {
    // Unique ID prevents collisions if multiple renders happen quickly
    const diagramId = `mermaid-diagram-${Date.now()}`;
    const { svg } = await mermaid.render(diagramId, code);
    container.innerHTML = svg;
    const svgEl = container.querySelector("svg");

    if (!svgEl) {
      throw new Error("Mermaid rendering failed to produce an SVG element.");
    }

    // --- ViewBox Handling ---
    let viewBox = svgEl.getAttribute("viewBox");
    let width, height;
    if (viewBox) {
      // Use viewBox if present
      [, , width, height] = viewBox.split(" ").map(Number); // Extract width/height only
    } else {
      throw new Error("No viewBox found for mermaid diagram found.");
    }

    // --- State Reset on Render ---
    originalViewBox = { w: width, h: height };
    currentViewBox = { x: 0, y: 0, w: width, h: height }; // Start view at 0,0 with original size
    zoomLevel = 1;
    svgEl.setAttribute("viewBox", `0 0 ${width} ${height}`); // Ensure it's set initially
    container.style.cursor = "grab"; // Reset cursor

    console.log("Rendering complete. Initial viewBox:", `0 0 ${width} ${height}`);
  } catch (err) {
    console.error("Mermaid rendering error:", err);
    container.innerHTML = `Error rendering Mermaid diagram:\n${err.message}`;
    resetPanZoomState(); // Reset state on error
  }
}

function resetPanZoomState() {
  originalViewBox = null;
  currentViewBox = null;
  zoomLevel = 1;
  isPanning = false;
  container.style.cursor = "default";
}

container.addEventListener(
  "wheel",
  (e) => {
    const svgEl = container.querySelector("svg");
    // Need original AND current viewBox state to zoom correctly
    if (!svgEl || !currentViewBox || !originalViewBox) {
      console.log("Zoom skipped: Missing SVG or viewBox state");
      return;
    }

    e.preventDefault();

    const rect = svgEl.getBoundingClientRect();

    // Avoid division by zero
    if (rect.width === 0 || rect.height === 0) {
      return;
    }

    // Mouse position relative to SVG container
    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;

    // Convert mouse position to SVG coordinates using the current viewBox
    const svgX = currentViewBox.x + (mouseX / rect.width) * currentViewBox.w;
    const svgY = currentViewBox.y + (mouseY / rect.height) * currentViewBox.h;

    // Zoom in or out
    const zoomFactor = e.deltaY < 0 ? 1.1 : 1 / 1.1;

    // Calculate new target zoom level and clamp it
    const newZoomLevel = Math.min(5, Math.max(0.2, zoomLevel * zoomFactor));

    // Prevent tiny zooms if already at boundary
    if (newZoomLevel === zoomLevel) {
      return;
    }

    // Calculate new viewBox dimensions based on original dimensions and new zoom level
    const newW = originalViewBox.w / newZoomLevel;
    const newH = originalViewBox.h / newZoomLevel;

    // Calculate new viewBox origin (x, y) to keep the point under the mouse stationary
    const newX = svgX - (mouseX / rect.width) * newW;
    const newY = svgY - (mouseY / rect.height) * newH;

    // Update state
    zoomLevel = newZoomLevel;
    currentViewBox.x = newX;
    currentViewBox.y = newY;
    currentViewBox.w = newW;
    currentViewBox.h = newH;

    // Apply the new viewBox
    svgEl.setAttribute("viewBox", `${newX} ${newY} ${newW} ${newH}`);
  },
  { passive: false } // Needed to prevent default scroll
);

container.addEventListener("mousedown", (e) => {
  const svgEl = container.querySelector("svg");
  // Only pan on left-click
  if (!svgEl || !currentViewBox || e.button !== 0) {
    return;
  }

  isPanning = true;
  panStartX = e.clientX;
  panStartY = e.clientY;
});

window.addEventListener("mousemove", (e) => {
  if (!isPanning || !currentViewBox) {
    return;
  }
  e.preventDefault();

  const svgEl = container.querySelector("svg");

  if (!svgEl) {
    isPanning = false; // Stop panning if SVG disappears
    return;
  }

  const rect = svgEl.getBoundingClientRect();

  // Avoid division by zero
  if (rect.width === 0 || rect.height === 0) {
    return;
  }

  // Calculate delta in screen coordinates
  const dx = e.clientX - panStartX;
  const dy = e.clientY - panStartY;

  // compute the pixel→SVG scale in X and Y…
  const scale = Math.max(currentViewBox.w / rect.width, currentViewBox.h / rect.height);
  const svgDx = dx * scale;
  const svgDy = dy * scale;

  // Update current viewBox position (subtract delta because moving mouse right pans view left)
  const newX = currentViewBox.x - svgDx;
  const newY = currentViewBox.y - svgDy;

  // Update state
  currentViewBox.x = newX;
  currentViewBox.y = newY;

  // Apply the new viewBox
  svgEl.setAttribute("viewBox", `${newX} ${newY} ${currentViewBox.w} ${currentViewBox.h}`);

  // Update start position for the next mousemove event
  panStartX = e.clientX;
  panStartY = e.clientY;
});

window.addEventListener("mouseup", (e) => {
  if (isPanning && e.button === 0) {
    isPanning = false;
  }
});

// Reset view button
container.addEventListener("dblclick", () => {
  const svgEl = container.querySelector("svg");

  if (svgEl && originalViewBox && currentViewBox) {
    console.log("Resetting view on double-click");
    currentViewBox = { x: 0, y: 0, w: originalViewBox.w, h: originalViewBox.h };
    zoomLevel = 1;
    svgEl.setAttribute("viewBox", `0 0 ${originalViewBox.w} ${originalViewBox.h}`);
  }
});

if (typeof vscode !== "undefined") {
  vscode.postMessage({ type: "webviewLoaded" });
  console.log("Webview loaded, requesting initial content.");
} else {
  console.warn("vscode API not available. Running outside VS Code webview?");
}

// DOWNLOAD SVG BUTTON
document.getElementById("download-svg").addEventListener("click", () => {
  const svgEl = document.querySelector("#mermaid-graph svg");
  if (!svgEl) return;
  const serializer = new XMLSerializer();
  const svgStr = serializer.serializeToString(svgEl);
  const blob = new Blob([svgStr], { type: "image/svg+xml" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "diagram.svg";
  link.click();
  URL.revokeObjectURL(url);
});

// COPY MERMAID CODE BUTTON
const copyButton = document.getElementById("copy-code");
copyButton.addEventListener("click", () => {
  navigator.clipboard
    .writeText(lastMermaidCode)
    .then(() => {
      // give immediate user feedback
      const originalText = copyButton.textContent;
      copyButton.textContent = "Copied!";
      setTimeout(() => {
        copyButton.textContent = originalText;
      }, 2000);
    })
    .catch(() => {
      console.error("Copy to clipboard failed");
    });
});

// HELP OVERLAY
const helpOverlay = document.getElementById("help-overlay");
document.getElementById("help-button").addEventListener("click", () => {
  helpOverlay.style.visibility = "visible";
});
document.getElementById("close-help").addEventListener("click", () => {
  helpOverlay.style.visibility = "hidden";
});
