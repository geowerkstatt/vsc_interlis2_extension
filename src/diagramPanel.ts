import * as vscode from "vscode";
import { getWebviewHTML } from "./contentProvider";

let diagramPanel: vscode.WebviewPanel | undefined;
let hasUserClosedPanel = false;

export function resetPanelState() {
  hasUserClosedPanel = false;
}

export function showDiagramPanel(context: vscode.ExtensionContext) {
  const column = vscode.window.activeTextEditor ? vscode.ViewColumn.Beside : vscode.ViewColumn.One;

  if (diagramPanel) {
    diagramPanel.reveal(column);
    return;
  }

  hasUserClosedPanel = false;

  diagramPanel = vscode.window.createWebviewPanel("INTERLISDiagramPanel", "INTERLIS Diagram Panel", column, {
    enableScripts: true,
    localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "out")],
  });

  diagramPanel.webview.html = getWebviewHTML(diagramPanel.webview, context.extensionUri);
  diagramPanel.onDidDispose(
    () => {
      diagramPanel = undefined;
      hasUserClosedPanel = true;
    },
    null,
    context.subscriptions
  );
}

export function handleInterlisInActiveTextEditor(context: vscode.ExtensionContext) {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    return;
  }
  if (editor.document.languageId !== "INTERLIS2") {
    return;
  }
  if (diagramPanel) {
    return;
  }
  if (hasUserClosedPanel) {
    return;
  }
  showDiagramPanel(context);
}

export function initializeDiagramPanel(context: vscode.ExtensionContext) {
  handleInterlisInActiveTextEditor(context);
  const listener = vscode.window.onDidChangeActiveTextEditor(() => {
    handleInterlisInActiveTextEditor(context);
  });
  context.subscriptions.push(listener);
  resetPanelState();
}
