import * as vscode from "vscode";
import { getWebviewHTML } from "./contentProvider";
import { getLanguageClient } from "./languageServer";
import { ExecuteCommandRequest } from "vscode-languageclient/node";

let diagramPanel: vscode.WebviewPanel | undefined;
let hasUserClosedPanel = false;
let updateTimer: NodeJS.Timeout | undefined;

export function resetPanelState() {
  hasUserClosedPanel = false;
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
  const column = vscode.window.activeTextEditor ? vscode.ViewColumn.Beside : vscode.ViewColumn.One;

  if (diagramPanel) {
    diagramPanel.reveal(column, true);
    return;
  }

  hasUserClosedPanel = false;

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

  const editor = vscode.window.activeTextEditor;
  if (editor && editor.document.languageId === "INTERLIS2") {
    const uri = editor.document.uri.toString();
    requestDiagram(uri).then((mermaidDsl) => {
      postMessage("update", mermaidDsl);
    });
  }

  diagramPanel.onDidDispose(
    () => {
      diagramPanel = undefined;
      hasUserClosedPanel = true;
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

  // debounce: wait 300ms after the last change before sending
  clearTimeout(updateTimer);
  updateTimer = setTimeout(() => {
    const uri = e.document.uri.toString();
    requestDiagram(uri).then((mermaidDsl) => {
      postMessage("update", mermaidDsl);
    });
  }, 300);
}

function closeDiagramPanel() {
  if (diagramPanel) {
    diagramPanel.dispose();
  }
}

export function updateDiagramVisibility(context: vscode.ExtensionContext) {
  const hasAnyIliOpen = vscode.window.visibleTextEditors.some((e) => e.document.languageId === "INTERLIS2");

  if (!hasAnyIliOpen) {
    closeDiagramPanel();
    hasUserClosedPanel = false;
  } else if (!diagramPanel && !hasUserClosedPanel) {
    showDiagramPanel(context);

    const editor = vscode.window.activeTextEditor;
    if (editor && editor.document.languageId === "INTERLIS2") {
      postMessage("update", editor.document.getText());
    }
  }
}

export function initializeDiagramPanel(context: vscode.ExtensionContext) {
  resetPanelState();
  updateDiagramVisibility(context);

  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidCloseTextDocument(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidChangeTextDocument((e) => handleTextChange(e))
  );
}
