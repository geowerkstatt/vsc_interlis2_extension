import * as vscode from "vscode";
import { getWebviewHTML } from "./contentProvider";
import { getLanguageClient } from "./languageServer";
import { ExecuteCommandRequest } from "vscode-languageclient/node";

interface Debounced<T extends any[]> {
  run: (...args: T) => void;
  cancel: () => void;
}

let diagramPanel: vscode.WebviewPanel | undefined;
let hasUserClosedPanel = false;
let isAutoClosing = false;
let closeTimer: NodeJS.Timeout | undefined;
let lastSentMermaidDsl: string | undefined;
let orientation = "LR";
let currentIliUri: string | undefined;
let currentEditor: vscode.TextEditor | undefined;

function autoClosePanel() {
  if (diagramPanel) {
    isAutoClosing = true;
    diagramPanel.dispose();
    isAutoClosing = false;
  }
}

async function requestDiagram(uri: string): Promise<string> {
  try {
    const result = await getLanguageClient().sendRequest(ExecuteCommandRequest.type, {
      command: "generateDiagram",
      arguments: [{ uri, orientation }],
    });
    return result ?? "";
  } catch (err) {
    console.error("Failed to generate diagram:", err);
    return "";
  }
}

export function showDiagramPanel(context: vscode.ExtensionContext) {
  hasUserClosedPanel = false;
  revealDiagramPanelInternal(context);
}

function revealDiagramPanelInternal(context: vscode.ExtensionContext) {
  const column = vscode.ViewColumn.Beside;

  if (diagramPanel) {
    diagramPanel.reveal(column, true);
    return;
  }

  diagramPanel = vscode.window.createWebviewPanel(
    "INTERLISDiagramPanel",
    "INTERLIS Diagram Panel",
    {
      viewColumn: column,
      preserveFocus: true,
    },
    {
      enableScripts: true,
      localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "out")],
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
      if (message.type === "nodesChange") {
        if (!message.changes || !Array.isArray(message.changes)) {
          console.warn("Ignoring invalid nodesChange message:", message);
          return;
        }
        // Handle nodes change
        console.log("onNodesChange:", message.changes);
        vscode.commands.executeCommand("interlis.diagram.nodesChange", message.changes, currentEditor);
      } else if (message.type === "colorChange") {
        if (!message.changes || !Array.isArray(message.changes)) {
          console.warn("Ignoring invalid colorChange message:", message);
          return;
        }
        // Handle color change
        vscode.commands.executeCommand("interlis.diagram.colorChange", message.changes, currentEditor);
      } else if (message.type === "webviewLoaded") {
        const editor = vscode.window.activeTextEditor;

        if (editor?.document.languageId === "INTERLIS2") {
          currentEditor = editor;
          currentIliUri = editor.document.uri.toString();
          orientation = message.orientation || "LR";
          requestDiagram(currentIliUri).then((mermaidDsl) => {
            lastSentMermaidDsl = mermaidDsl;
            diagramPanel?.webview.postMessage({ type: "update", text: mermaidDsl, resetZoom: true });
          });
        }
      } else if (message.type === "orientation" && currentIliUri) {
        orientation = message.orientation || "LR";
        requestDiagram(currentIliUri).then((mermaidDsl) => {
          lastSentMermaidDsl = mermaidDsl;
          diagramPanel?.webview.postMessage({ type: "update", text: mermaidDsl, resetZoom: true });
        });
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
      if (!isAutoClosing) {
        hasUserClosedPanel = true;
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

function postMessage(type: string, text: string, resetZoom: boolean) {
  diagramPanel?.webview.postMessage({
    type: type,
    text: text,
    resetZoom: resetZoom,
  });
}

function handleTextChange(e: vscode.TextDocumentChangeEvent) {
  if (e.document.languageId !== "INTERLIS2") {
    return;
  }

  if (!diagramPanel || !diagramPanel.visible) {
    return;
  }
  debouncedRequestDiagram.run(e.document.uri.toString());
}

const debouncedAutoClosePanel = debounce(autoClosePanel, 100);

const debouncedRequestDiagram = debounce((uri: string) => {
  requestDiagram(uri).then((mermaidDsl) => {
    lastSentMermaidDsl = mermaidDsl;
    diagramPanel?.webview.postMessage({ type: "update", text: mermaidDsl, resetZoom: false });
  });
}, 300);

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

    if (!diagramPanel && !hasUserClosedPanel) {
      revealDiagramPanelInternal(context);
    }
  }
}

export function initializeDiagramPanel(context: vscode.ExtensionContext) {
  updateDiagramVisibility(context);

  context.subscriptions.push(
    // Replace this entire event handler with your new code block
    vscode.window.onDidChangeActiveTextEditor(async (editor) => {
      updateDiagramVisibility(context);

      if (editor?.document.languageId === "INTERLIS2") {
        // Update our tracked editor whenever an INTERLIS file becomes active
        currentEditor = editor;
        currentIliUri = editor.document.uri.toString();
        
        if (!diagramPanel) {
          return;
        }

        const mermaidDsl = await requestDiagram(currentIliUri);
        postMessage("update", mermaidDsl, mermaidDsl !== lastSentMermaidDsl);
        lastSentMermaidDsl = mermaidDsl;
      }
    }),
    vscode.workspace.onDidCloseTextDocument(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidChangeTextDocument((e) => handleTextChange(e))
  );
}
