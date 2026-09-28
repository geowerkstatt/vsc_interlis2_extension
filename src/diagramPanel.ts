import * as vscode from "vscode";
import { getWebviewHTML } from "./contentProvider";
import { getLanguageClient } from "./languageServer";
import { generateMarkdown } from "./markdown";
import { ExecuteCommandRequest } from "vscode-languageclient/node";

interface Debounced<T extends any[]> {
  run: (...args: T) => void;
  cancel: () => void;
}

const AUTO_LANGUAGE = "auto";

let diagramPanel: vscode.WebviewPanel | undefined;
let isAutoOpening = false;
let isAutoClosing = false;
let closeTimer: NodeJS.Timeout | undefined;
let currentLanguage = AUTO_LANGUAGE;
let currentIliUri: string | undefined;
/** Files of every model in the diagram shown, including the imported ones, in the form of `vscode.Uri.toString()`. */
let shownFiles = new Set<string>();

function autoClosePanel() {
  if (diagramPanel) {
    isAutoClosing = true;
    diagramPanel.dispose();
    isAutoClosing = false;
  }
}

function readConfiguredLanguage(): string {
  const value = vscode.workspace.getConfiguration("interlis.documentation").get<string>("language");
  return value && value.length > 0 ? value : AUTO_LANGUAGE;
}

/** Asks the language server for the graph JSON of the file; empty when it could not produce one. */
async function requestDiagram(uri: string): Promise<string> {
  const request = { command: "generateGraph", arguments: [{ uri, language: currentLanguage }] };
  try {
    const result = await getLanguageClient().sendRequest(ExecuteCommandRequest.type, request);
    return result ?? "";
  } catch (err) {
    console.error("Failed to generate diagram:", err);
    return "";
  }
}

/** The files the models of a graph were read from; empty when the text is not a graph. */
function filesOfGraph(text: string): Set<string> {
  const files = new Set<string>();
  try {
    const graph = JSON.parse(text) as { groups?: { uri?: string }[] };
    for (const group of graph.groups ?? []) {
      if (group.uri) {
        files.add(vscode.Uri.parse(group.uri).toString());
      }
    }
  } catch {
    // The server returns an empty string when it could not produce a graph.
  }
  return files;
}

/**
 * Whether the document is one of the files the diagram shows: the file itself (also while its
 * graph is empty because of an error) or the file of an imported model.
 */
function isShown(document: vscode.TextDocument): boolean {
  const key = document.uri.toString();
  return key === currentIliUri || shownFiles.has(key);
}

function refreshDiagram(resetZoom: boolean) {
  const uri = currentIliUri;
  if (!uri) {
    return;
  }
  requestDiagram(uri).then((text) => {
    // The diagram may have switched to another file while the server was busy.
    if (uri !== currentIliUri) {
      return;
    }
    shownFiles = filesOfGraph(text);
    diagramPanel?.webview.postMessage({ type: "update", text, resetZoom });
  });
}

/**
 * Opens an INTERLIS file at the given zero-based line (triggered by clicking a box, frame or
 * association line in the graph). Definitions of imported models name the file they were read
 * from; everything else lives in the current file.
 */
async function revealLine(uri: unknown, line: unknown) {
  const target = typeof uri === "string" && uri.length > 0 ? uri : currentIliUri;
  if (!target || typeof line !== "number" || !Number.isInteger(line) || line < 0) {
    return;
  }
  try {
    const document = await vscode.workspace.openTextDocument(vscode.Uri.parse(target));
    const range = document.lineAt(Math.min(line, document.lineCount - 1)).range;
    // Compare canonical forms: the server writes the file URI differently than VS Code (e.g. "c:" vs "c%3A").
    const key = document.uri.toString();
    const visible = vscode.window.visibleTextEditors;
    const existingEditor = visible.find((e) => e.document.uri.toString() === key);
    // Never open the file in the panel's own group (the default when the panel is active): that would
    // hide the panel. Fall back to the group of another INTERLIS editor, then to a group beside the panel.
    const viewColumn =
      existingEditor?.viewColumn ??
      visible.find((e) => e.document.languageId === "INTERLIS2")?.viewColumn ??
      (diagramPanel?.active ? vscode.ViewColumn.Beside : undefined);
    const editor = await vscode.window.showTextDocument(document, {
      viewColumn,
      preserveFocus: false,
      preview: false,
      selection: range,
    });
    editor.revealRange(range, vscode.TextEditorRevealType.InCenter);
  } catch (err) {
    console.error("Failed to reveal definition:", err);
  }
}

/**
 * Shows the diagram of `uri` (the editor whose title-bar button was clicked), or of the active
 * INTERLIS editor when the command comes from elsewhere. This is the only way the diagram switches
 * to another file: opening or activating an editor never changes it.
 */
export function showDiagramPanel(context: vscode.ExtensionContext, uri?: vscode.Uri) {
  isAutoOpening = true;
  const editor = vscode.window.activeTextEditor;
  const target =
    uri?.toString() ?? (editor?.document.languageId === "INTERLIS2" ? editor.document.uri.toString() : undefined);
  const switching = target !== undefined && target !== currentIliUri;
  if (target) {
    currentIliUri = target;
  }
  const existed = diagramPanel !== undefined;
  revealDiagramPanelInternal(context);
  if (existed && switching) {
    refreshDiagram(true);
  }
}

function revealDiagramPanelInternal(context: vscode.ExtensionContext) {
  const column = vscode.ViewColumn.Beside;

  if (diagramPanel) {
    diagramPanel.reveal(column, true);
    return;
  }

  currentLanguage = readConfiguredLanguage();

  diagramPanel = vscode.window.createWebviewPanel(
    "INTERLISDiagramPanel",
    "INTERLIS Panel",
    {
      viewColumn: column,
      preserveFocus: true,
    },
    {
      enableScripts: true,
      localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "out")],
      // The layout, filters and bends live in the webview; keep them while another tab covers the panel.
      retainContextWhenHidden: true,
    }
  );

  diagramPanel.webview.html = getWebviewHTML(diagramPanel.webview, context.extensionUri);

  diagramPanel.webview.onDidReceiveMessage(
    (message) => {
      // Guard Clause: Validate basic message structure and type
      if (!message || typeof message !== "object" || typeof message.type !== "string") {
        console.warn("Ignoring invalid message from webview:", message);
        return;
      }

      if (message.type === "webviewLoaded") {
        // Opened by the command, the file is already chosen; opened automatically, it is the active one.
        const editor = vscode.window.activeTextEditor;
        if (!currentIliUri && editor?.document.languageId === "INTERLIS2") {
          currentIliUri = editor.document.uri.toString();
        }
        diagramPanel?.webview.postMessage({ type: "init", language: currentLanguage });
        refreshDiagram(true);
      } else if (message.type === "language") {
        currentLanguage = message.language || AUTO_LANGUAGE;
        refreshDiagram(false);
      } else if (message.type === "reveal") {
        void revealLine(message.uri, message.line);
      } else if (message.type === "generateMarkdown") {
        if (!currentIliUri) {
          return;
        }
        const uri = currentIliUri;
        vscode.window.withProgress({ location: vscode.ProgressLocation.Notification }, async (progress) =>
          generateMarkdown(progress, uri, getLanguageClient(), currentLanguage)
        );
      } else {
        console.warn(`Ignoring unknown message type from webview: ${message.type}`);
      }
    },
    null,
    context.subscriptions
  );

  diagramPanel.onDidDispose(
    () => {
      diagramPanel = undefined;
      currentIliUri = undefined;
      shownFiles = new Set();
      if (!isAutoClosing) {
        isAutoOpening = false;
      }
    },
    null,
    context.subscriptions
  );

  diagramPanel.onDidChangeViewState(
    (e) => {
      if (e.webviewPanel.active) {
        vscode.commands.executeCommand("workbench.action.focusActiveEditorGroup");
      }
    },
    null,
    context.subscriptions
  );
}

function handleTextChange(e: vscode.TextDocumentChangeEvent) {
  if (e.document.languageId !== "INTERLIS2") {
    return;
  }

  // Only an edit of a file the diagram shows (the file itself or one of its imports) refreshes it;
  // editing any other file leaves the diagram alone.
  if (!diagramPanel || !diagramPanel.visible || !currentIliUri || !isShown(e.document)) {
    return;
  }
  debouncedRefreshDiagram.run();
}

const debouncedAutoClosePanel = debounce(autoClosePanel, 100);

const debouncedRefreshDiagram = debounce(() => refreshDiagram(false), 300);

function debounce<T extends any[]>(fn: (...args: T) => void, delay: number): Debounced<T> {
  let timer: NodeJS.Timeout | undefined;
  return {
    run: (...args: T) => {
      if (timer) {
        clearTimeout(timer);
      }
      timer = setTimeout(() => fn(...args), delay);
    },
    cancel: () => {
      if (timer) {
        clearTimeout(timer);
      }
      timer = undefined;
    },
  };
}

export function updateDiagramVisibility(context: vscode.ExtensionContext) {
  const hasAnyIliOpen = vscode.window.visibleTextEditors.some((e) => e.document.languageId === "INTERLIS2");

  if (!hasAnyIliOpen) {
    clearTimeout(closeTimer);
    debouncedAutoClosePanel.run();
  } else {
    clearTimeout(closeTimer);
    debouncedAutoClosePanel.cancel();
    closeTimer = undefined;

    if (!diagramPanel && isAutoOpening) {
      revealDiagramPanelInternal(context);
    }
  }
}

export function initializeDiagramPanel(context: vscode.ExtensionContext, configuration: vscode.WorkspaceConfiguration) {
  isAutoOpening = configuration.get("autoOpenDiagramView", false);
  updateDiagramVisibility(context);

  context.subscriptions.push(
    // Switching editors only opens or closes the panel; the diagram changes with the command alone.
    vscode.window.onDidChangeActiveTextEditor(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidCloseTextDocument(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidChangeTextDocument((e) => handleTextChange(e))
  );
}
