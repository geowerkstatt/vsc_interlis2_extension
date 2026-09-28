import { createCytoscapeRenderer, ZOOM_STEP } from "./cytoscapeRenderer";
import { createFilterTree } from "./filterTree";

interface VSCodeApi {
  postMessage(message: unknown): void;
}

type IncomingMessage = { type: "init"; language: string } | { type: "update"; text: string; resetZoom: boolean };

declare const acquireVsCodeApi: () => VSCodeApi;

(() => {
  const vscode = acquireVsCodeApi();

  // ---- DOM Elements ----
  const graphContainer = document.getElementById("graph") as HTMLDivElement;
  const resetLayoutButton = document.getElementById("reset-layout") as HTMLButtonElement;
  const settleLayoutButton = document.getElementById("settle-layout") as HTMLButtonElement;
  const filterToggle = document.getElementById("toggle-filters") as HTMLButtonElement;
  const filterPopover = document.getElementById("filter-popover") as HTMLElement;
  const filterTree = document.getElementById("filter-tree") as HTMLDivElement;
  const resetFiltersButton = document.getElementById("reset-filters") as HTMLButtonElement;
  const downloadButton = document.getElementById("download-svg") as HTMLButtonElement;
  const generateMarkdownButton = document.getElementById("generate-markdown") as HTMLButtonElement;
  const languageSelect = document.getElementById("language") as HTMLSelectElement;
  const zoomInButton = document.getElementById("zoom-in") as HTMLButtonElement;
  const zoomOutButton = document.getElementById("zoom-out") as HTMLButtonElement;
  const zoomFitButton = document.getElementById("zoom-fit") as HTMLButtonElement;
  const helpOverlay = document.getElementById("help-overlay") as HTMLDivElement;
  const helpButton = document.getElementById("help-button") as HTMLButtonElement;
  const closeHelpButton = document.getElementById("close-help") as HTMLButtonElement;

  // ---- Renderer ----
  const renderer = createCytoscapeRenderer(graphContainer, {
    onReveal: (location) => vscode.postMessage({ type: "reveal", uri: location.uri, line: location.line }),
  });

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

  const debouncedRender = debounce((text: string, resetZoom: boolean) => {
    void renderer.render(text, resetZoom);
  }, 150);

  // ----- Event Handlers -----
  function handleMessage(event: MessageEvent<IncomingMessage>): void {
    const msg = event.data;
    if (msg.type === "init") {
      languageSelect.value = msg.language;
    } else if (msg.type === "update") {
      debouncedRender(msg.text, msg.resetZoom);
    }
  }

  function handleDownload(): void {
    const svg = renderer.getExportSvg();
    if (!svg) return;

    const svgString = new XMLSerializer().serializeToString(svg);
    const blob = new Blob([svgString], { type: "image/svg+xml" });
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = "diagram.svg";
    link.click();

    URL.revokeObjectURL(url);
  }

  function handleFitView(): void {
    renderer.fitToView();
  }

  function handleResetLayout(): void {
    void renderer.resetLayout();
  }

  function handleSettleLayout(): void {
    void renderer.settleLayout();
  }

  function handleLanguageChange(): void {
    vscode.postMessage({ type: "language", language: languageSelect.value });
  }

  function setFiltersOpen(open: boolean): void {
    filterPopover.hidden = !open;
    filterToggle.setAttribute("aria-expanded", open ? "true" : "false");
  }

  function handleFilterToggle(): void {
    setFiltersOpen(filterPopover.hidden);
  }

  /** The popover stays open while the diagram is used, and closes on a click anywhere else or with Escape. */
  function handleDocumentPointerDown(event: PointerEvent): void {
    if (filterPopover.hidden) return;
    const target = event.target as Node | null;
    if (target && (filterPopover.contains(target) || filterToggle.contains(target))) return;
    setFiltersOpen(false);
  }

  function handleKeyDown(event: KeyboardEvent): void {
    if (event.key !== "Escape") return;
    if (!filterPopover.hidden) {
      setFiltersOpen(false);
      filterToggle.focus();
    } else if (helpOverlay.style.visibility === "visible") {
      handleHelpClose();
    }
  }

  function handleGenerateMarkdown(): void {
    vscode.postMessage({ type: "generateMarkdown" });
  }

  function handleHelpOpen(): void {
    helpOverlay.style.visibility = "visible";
  }

  function handleHelpClose(): void {
    helpOverlay.style.visibility = "hidden";
  }

  function postWebviewLoaded(): void {
    vscode.postMessage({ type: "webviewLoaded" });
  }

  function attachEvents(): void {
    window.addEventListener("message", handleMessage);
    resetLayoutButton.addEventListener("click", handleResetLayout);
    settleLayoutButton.addEventListener("click", handleSettleLayout);
    filterToggle.addEventListener("click", handleFilterToggle);
    document.addEventListener("pointerdown", handleDocumentPointerDown);
    document.addEventListener("keydown", handleKeyDown);
    downloadButton.addEventListener("click", handleDownload);
    generateMarkdownButton.addEventListener("click", handleGenerateMarkdown);
    languageSelect.addEventListener("change", handleLanguageChange);
    zoomInButton.addEventListener("click", () => renderer.zoomBy(ZOOM_STEP));
    zoomOutButton.addEventListener("click", () => renderer.zoomBy(1 / ZOOM_STEP));
    zoomFitButton.addEventListener("click", handleFitView);
    helpButton.addEventListener("click", handleHelpOpen);
    closeHelpButton.addEventListener("click", handleHelpClose);
  }

  function init(): void {
    attachEvents();
    const filters = createFilterTree(filterTree, (next) => renderer.setFilters(next));
    resetFiltersButton.addEventListener("click", () => filters.reset());
    postWebviewLoaded();
  }

  init();
})();
