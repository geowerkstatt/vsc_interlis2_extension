/* eslint-env browser */
/* global mermaid, acquireVsCodeApi */

(() => {
  "use strict";

  // ---- State ----
  let mermaidInitialized = false;
  let lastMermaidCode = "";
  let originalViewBox = null;
  let currentViewBox = null;
  let zoomLevel = 1;
  let isPanning = false;
  let panStart = { x: 0, y: 0 };

  // ---- DOM Elements ----
  const container = document.getElementById("mermaid-graph");
  const downloadButton = document.getElementById("download-svg");
  const copyButton = document.getElementById("copy-code");
  const helpOverlay = document.getElementById("help-overlay");
  const helpButton = document.getElementById("help-button");
  const closeHelpButton = document.getElementById("close-help");

  // ----- Helpers -----
  const debounce = (fn, delay) => {
    let timer;
    return (...args) => {
      clearTimeout(timer);
      timer = setTimeout(() => fn(...args), delay);
    };
  };

  const getSvgElement = () => container.querySelector("svg");

  const updateViewBox = ({ x, y, w, h }) => {
    const svg = getSvgElement();
    if (!svg) {
      return;
    }
    svg.setAttribute("viewBox", `${x} ${y} ${w} ${h}`);
  };

  const resetPanZoom = () => {
    originalViewBox = null;
    currentViewBox = null;
    zoomLevel = 1;
    isPanning = false;
    container.style.cursor = "default";
  };

  // ----- Mermaid Initialization -----
  function initMermaid() {
    if (mermaidInitialized) return;
    mermaid.initialize({
      startOnLoad: false,
      theme: "forest",
      flowchart: { curve: "basis", nodeSpacing: 50, rankSpacing: 50 },
      securityLevel: "strict",
    });
    mermaidInitialized = true;
    console.log("Mermaid initialized");
  }

  // ----- Diagram Rendering -----
  async function renderDiagram(diagramCode) {
    if (!diagramCode) {
      container.innerHTML = "<div>Could not load diagram.</div>";
      resetPanZoom();
      return;
    }

    initMermaid();
    container.textContent = "Rendering...";
    lastMermaidCode = diagramCode;

    try {
      const id = `mermaid-${Date.now()}`; //Unique Render ID to avoid conflicts (defensive programming)
      const { svg } = await mermaid.render(id, diagramCode);
      container.innerHTML = svg;

      const svgElement = getSvgElement();
      const viewBox = svgElement.getAttribute("viewBox");
      if (!viewBox) {
        throw new Error("No viewBox on SVG");
      }

      const [, , width, height] = viewBox.split(" ").map(Number);
      originalViewBox = { w: width, h: height };
      currentViewBox = { x: 0, y: 0, w: width, h: height };
      zoomLevel = 1;
      updateViewBox(currentViewBox);
      container.style.cursor = "grab";
      console.log("Render complete");
    } catch (err) {
      console.error("Render error:", err);
      container.textContent = `Error rendering diagram: ${err.message}`;
      resetPanZoom();
    }
  }

  const debouncedRender = debounce(renderDiagram, 150);

  // ----- Event Handlers -----
  function handleMessage({ data }) {
    if (data.type === "update") {
      debouncedRender(data.text);
    }
  }

  function handleWheel(e) {
    const svgElement = getSvgElement();
    if (!svgElement || !currentViewBox || !originalViewBox) {
      return;
    }
    e.preventDefault();

    const rect = svgElement.getBoundingClientRect();
    if (!rect.width || !rect.height) {
      return;
    }

    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;
    const svgX = currentViewBox.x + (mouseX / rect.width) * currentViewBox.w;
    const svgY = currentViewBox.y + (mouseY / rect.height) * currentViewBox.h;

    const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
    const newZoom = Math.min(5, Math.max(0.2, zoomLevel * factor));
    if (newZoom === zoomLevel) {
      return;
    }

    const newW = originalViewBox.w / newZoom;
    const newH = originalViewBox.h / newZoom;
    const newX = svgX - (mouseX / rect.width) * newW;
    const newY = svgY - (mouseY / rect.height) * newH;

    zoomLevel = newZoom;
    Object.assign(currentViewBox, { x: newX, y: newY, w: newW, h: newH });
    updateViewBox(currentViewBox);
  }

  function handleMouseDown(e) {
    if (e.button !== 0 || !currentViewBox) {
      return;
    }
    const svgEl = getSvgElement();

    if (!svgEl) {
      return;
    }
    container.style.cursor = "grabbing";
    isPanning = true;
    panStart = { x: e.clientX, y: e.clientY };
  }

  function handleMouseMove(e) {
    if (!isPanning || !currentViewBox) {
      return;
    }
    e.preventDefault();
    const svgElement = getSvgElement();
    if (!svgElement) {
      isPanning = false;
      return;
    }

    const rect = svgElement.getBoundingClientRect();
    if (!rect.width || !rect.height) {
      return;
    }

    const diagramX = e.clientX - panStart.x;
    const diagramY = e.clientY - panStart.y;
    const scale = Math.max(currentViewBox.w / rect.width, currentViewBox.h / rect.height);

    currentViewBox.x -= diagramX * scale;
    currentViewBox.y -= diagramY * scale;
    updateViewBox(currentViewBox);

    panStart = { x: e.clientX, y: e.clientY };
  }

  function handleMouseUp(e) {
    if (e.button === 0) {
      isPanning = false;
      container.style.cursor = "grab";
    }
  }

  function handleDoubleClick() {
    if (!originalViewBox || !currentViewBox) {
      return;
    }
    currentViewBox = { x: 0, y: 0, w: originalViewBox.w, h: originalViewBox.h };
    zoomLevel = 1;
    updateViewBox(currentViewBox);
  }

  function handleDownload() {
    const svgEl = getSvgElement();
    if (!svgEl || !originalViewBox) {
      return;
    }

    const clone = svgEl.cloneNode(true);

    const { width, height } = originalViewBox;
    clone.setAttribute("viewBox", `0 0 ${width} ${height}`);

    clone.removeAttribute("width");
    clone.removeAttribute("height");

    const svgString = new XMLSerializer().serializeToString(clone);
    const blob = new Blob([svgString], { type: "image/svg+xml" });
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = "diagram.svg";
    link.click();

    URL.revokeObjectURL(url);
  }

  function handleCopy() {
    navigator.clipboard.writeText(lastMermaidCode).then(() => {
      const text = copyButton.textContent;
      copyButton.textContent = "Copied!";
      setTimeout(() => {
        copyButton.textContent = text;
      }, 2000);
    });
  }

  function handleHelpOpen() {
    helpOverlay.style.visibility = "visible";
  }

  function handleHelpClose() {
    helpOverlay.style.visibility = "hidden";
  }

  function postWebviewLoaded() {
    if (typeof acquireVsCodeApi !== "undefined") {
      acquireVsCodeApi().postMessage({ type: "webviewLoaded" });
    }
  }

  // ----- Attach Listeners -----
  function attachEvents() {
    window.addEventListener("message", handleMessage);
    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
    container.addEventListener("wheel", handleWheel, { passive: false });
    container.addEventListener("mousedown", handleMouseDown);
    container.addEventListener("dblclick", handleDoubleClick);
    copyButton.addEventListener("click", handleCopy);
    helpButton.addEventListener("click", handleHelpOpen);
    downloadButton.addEventListener("click", handleDownload);
    closeHelpButton.addEventListener("click", handleHelpClose);
  }

  // ----- Init -----
  function init() {
    attachEvents();
    initMermaid();
    postWebviewLoaded();
  }

  init();
})();
