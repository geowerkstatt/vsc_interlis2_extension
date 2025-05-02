import * as vscode from "vscode";
import { getWebviewHTML } from "./contentProvider";
import { getLanguageClient } from "./languageServer";
import { ExecuteCommandRequest } from "vscode-languageclient/node";

let diagramPanel: vscode.WebviewPanel | undefined;
let hasUserClosedPanel = false;
let isAutoClosing = false;
let closeTimer: NodeJS.Timeout | undefined;

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
      arguments: [{ uri }],
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

      if (message.type === "webviewLoaded") {
        const editor = vscode.window.activeTextEditor;

        if (editor?.document.languageId === "INTERLIS2") {
          const uri = editor.document.uri.toString();
          requestDiagram(uri).then((mermaidDsl) => {
            diagramPanel?.webview.postMessage({ type: "update", text: mermaidDsl });
          });
        }
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

function postMessage(type: string, text: string) {
  diagramPanel?.webview.postMessage({
    type: type,
    text: text,
  });
}

function handleTextChange(e: vscode.TextDocumentChangeEvent) {
  if (e.document.languageId !== "INTERLIS2") {
    return;
  }

  if (!diagramPanel || !diagramPanel.visible) {
    return;
  }
  debouncedRequestDiagram(e.document.uri.toString());
}

const debouncedAutoClosePanel = debounce(autoClosePanel, 100);

const debouncedRequestDiagram = debounce((uri: string) => {
  requestDiagram(uri).then((mermaidDsl) => {
    diagramPanel?.webview.postMessage({ type: "update", text: mermaidDsl });
  });
}, 300);

function debounce<T extends any[]>(fn: (...args: T) => void, delay: number) {
  let timer: number | undefined;
  return (...args: T) => {
    if (timer !== undefined) {
      clearTimeout(timer);
    }
    timer = window.setTimeout(() => fn(...args), delay);
  };
}

export function updateDiagramVisibility(context: vscode.ExtensionContext) {
  const hasAnyIliOpen = vscode.window.visibleTextEditors.some((e) => e.document.languageId === "INTERLIS2");

  if (!hasAnyIliOpen) {
    debouncedAutoClosePanel();
  } else {
    clearTimeout(closeTimer);

    if (!diagramPanel && !hasUserClosedPanel) {
      revealDiagramPanelInternal(context);
    }
  }
}

export function initializeDiagramPanel(context: vscode.ExtensionContext) {
  updateDiagramVisibility(context);

  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(async (editor) => {
      updateDiagramVisibility(context);

      if (editor?.document.languageId !== "INTERLIS2" || !diagramPanel) {
        return;
      }

      const uri = editor.document.uri.toString();
      const mermaidDsl = await requestDiagram(uri);
      postMessage("update", mermaidDsl);
    }),
    vscode.workspace.onDidCloseTextDocument(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidChangeTextDocument((e) => handleTextChange(e))
  );
}
