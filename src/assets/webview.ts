import mermaid from "mermaid";

interface ViewBox {
  x: number;
  y: number;
  w: number;
  h: number;
}

interface VSCodeApi {
  postMessage(message: any): void;
}

interface WebviewMessage {
  type: "update";
  text: string;
  resetZoom: boolean;
}

declare const acquireVsCodeApi: () => VSCodeApi;

(() => {
  // ---- State ----
  let mermaidInitialized = false;
  let lastMermaidCode = "";
  let originalViewBox: Pick<ViewBox, "w" | "h"> | null = null;
  let currentViewBox: ViewBox | null = null;
  let zoomLevel = 1;
  let isPanning = false;
  let panStart = { x: 0, y: 0 };

  // ---- DOM Elements ----
  const container = document.getElementById("mermaid-graph") as HTMLDivElement;
  const downloadButton = document.getElementById("download-svg") as HTMLButtonElement;
  const copyButton = document.getElementById("copy-code") as HTMLButtonElement;
  const helpOverlay = document.getElementById("help-overlay") as HTMLDivElement;
  const helpButton = document.getElementById("help-button") as HTMLButtonElement;
  const closeHelpButton = document.getElementById("close-help") as HTMLButtonElement;

  // ----- Helpers -----
  function debounce<T extends any[]>(fn: (...args: T) => void, delay: number) {
    let timer: number | undefined;
    return (...args: T) => {
      if (timer !== undefined) {
        clearTimeout(timer);
      }
      timer = window.setTimeout(() => fn(...args), delay);
    };
  }

  function getSvgElement(): SVGSVGElement | null {
    return container.querySelector("svg");
  }

  function updateViewBox(vb: ViewBox): void {
    const svg = getSvgElement();
    if (!svg) {
      return;
    }
    svg.setAttribute("viewBox", `${vb.x} ${vb.y} ${vb.w} ${vb.h}`);
  }

  function resetPanZoom(): void {
    originalViewBox = null;
    currentViewBox = null;
    zoomLevel = 1;
    isPanning = false;
    container.style.cursor = "default";
  }

  function removeMermaidErrorContainers(): void {
    // Select all DIVs with an id beginning with "dmermaid-"
    const errorDivs = document.querySelectorAll<HTMLDivElement>('div[id^="dmermaid-"]');
    errorDivs.forEach((div) => div.remove());
  }

  // ----- Mermaid Initialization -----
  function initMermaid(): void {
    if (mermaidInitialized) {
      return;
    }
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
  async function renderDiagram(diagramCode: string, resetZoom: boolean): Promise<void> {
    if (!diagramCode) {
      container.innerHTML = "<div>Could not load diagram.</div>";
      resetPanZoom();
      return;
    }

    removeMermaidErrorContainers();
    initMermaid();
    container.textContent = "Rendering...";
    lastMermaidCode = diagramCode;

    try {
      const id = `mermaid-${Date.now()}`;
      // mermaid.render already sanitizes the svg with dompurify: https://github.com/mermaid-js/mermaid/blob/a566353030e8b5aa6379b2989aa19663d5f37bc3/packages/mermaid/src/mermaidAPI.ts#L454
      const { svg } = await mermaid.render(id, diagramCode);
      // codeql[js/unsafe-innerhtml] safe: sanitized by Mermaid’s built-in DOMPurify (securityLevel strict)
      container.innerHTML = svg;

      const svgElement = getSvgElement();
      const viewBoxAttribute = svgElement?.getAttribute("viewBox");
      if (!viewBoxAttribute) {
        throw new Error("No viewBox on SVG");
      }

      const [, , widthString, heightString] = viewBoxAttribute.split(" ");
      const width = Number(widthString);
      const height = Number(heightString);

      originalViewBox = { w: width, h: height };

      if (!currentViewBox || resetZoom) {
        currentViewBox = { x: 0, y: 0, w: width, h: height };
        zoomLevel = 1;
      }
      updateViewBox(currentViewBox);
      container.style.cursor = "grab";
      console.log("Render complete");
    } catch (err: any) {
      console.error("Render error:", err);
      container.textContent = `Error rendering diagram: ${err.message}`;
      resetPanZoom();
    }
  }

  const debouncedRender = debounce(renderDiagram, 150);

  // ----- Event Handlers -----
  function handleMessage(event: MessageEvent<WebviewMessage>): void {
    const msg = event.data;
    if (msg.type === "update") {
      debouncedRender(msg.text, msg.resetZoom);
    }
  }

  function handleWheel(e: WheelEvent): void {
    const svgElement = getSvgElement();
    if (!svgElement || !currentViewBox || !originalViewBox) return;
    e.preventDefault();

    const rect = svgElement.getBoundingClientRect();
    if (!rect.width || !rect.height) return;

    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;
    const svgX = currentViewBox.x + (mouseX / rect.width) * currentViewBox.w;
    const svgY = currentViewBox.y + (mouseY / rect.height) * currentViewBox.h;

    const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
    const newZoom = Math.min(5, Math.max(0.2, zoomLevel * factor));
    if (newZoom === zoomLevel) return;

    const newW = originalViewBox.w / newZoom;
    const newH = originalViewBox.h / newZoom;
    const newX = svgX - (mouseX / rect.width) * newW;
    const newY = svgY - (mouseY / rect.height) * newH;

    zoomLevel = newZoom;
    currentViewBox = { x: newX, y: newY, w: newW, h: newH };
    updateViewBox(currentViewBox);
  }

  function handleMouseDown(e: MouseEvent): void {
    if (e.button !== 0 || !currentViewBox) return;
    const svgEl = getSvgElement();
    if (!svgEl) return;
    container.style.cursor = "grabbing";
    isPanning = true;
    panStart = { x: e.clientX, y: e.clientY };
  }

  function handleMouseMove(e: MouseEvent): void {
    if (!isPanning || !currentViewBox) return;
    e.preventDefault();
    const svgElement = getSvgElement();
    if (!svgElement) {
      isPanning = false;
      return;
    }

    const rect = svgElement.getBoundingClientRect();
    if (!rect.width || !rect.height) return;

    const dx = e.clientX - panStart.x;
    const dy = e.clientY - panStart.y;
    const scale = Math.max(currentViewBox.w / rect.width, currentViewBox.h / rect.height);

    currentViewBox.x -= dx * scale;
    currentViewBox.y -= dy * scale;
    updateViewBox(currentViewBox);

    panStart = { x: e.clientX, y: e.clientY };
  }

  function handleMouseUp(e: MouseEvent): void {
    if (e.button === 0) {
      isPanning = false;
      container.style.cursor = "grab";
    }
  }

  function handleDoubleClick(): void {
    if (!originalViewBox || !currentViewBox) return;
    currentViewBox = { x: 0, y: 0, w: originalViewBox.w, h: originalViewBox.h };
    zoomLevel = 1;
    updateViewBox(currentViewBox);
  }

  function handleDownload(): void {
    const svgEl = getSvgElement();
    if (!svgEl || !originalViewBox) return;

    const clone = svgEl.cloneNode(true) as SVGSVGElement;
    clone.setAttribute("viewBox", `0 0 ${originalViewBox.w} ${originalViewBox.h}`);
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

  function handleCopy(): void {
    const text = copyButton.textContent ?? "";
    navigator.clipboard
      .writeText(lastMermaidCode)
      .then(() => {
        copyButton.textContent = "Copied!";
        setTimeout(() => {
          copyButton.textContent = text;
        }, 2000);
      })
      .catch((e) => {
        console.error(e);
        copyButton.textContent = "Copy failed";
        setTimeout(() => {
          copyButton.textContent = text;
        }, 2000);
      });
  }

  function handleHelpOpen(): void {
    helpOverlay.style.visibility = "visible";
  }

  function handleHelpClose(): void {
    helpOverlay.style.visibility = "hidden";
  }

  function postWebviewLoaded(): void {
    const vscode = acquireVsCodeApi();
    vscode.postMessage({ type: "webviewLoaded" });
  }

  function attachEvents(): void {
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

  function init(): void {
    attachEvents();
    initMermaid();
    postWebviewLoaded();
  }

  init();
})();
