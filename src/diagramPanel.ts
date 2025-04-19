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
  }
}

export function initializeDiagramPanel(context: vscode.ExtensionContext) {
  resetPanelState();

  updateDiagramVisibility(context);

  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(() => updateDiagramVisibility(context)),
    vscode.workspace.onDidCloseTextDocument(() => updateDiagramVisibility(context))
  );
}
