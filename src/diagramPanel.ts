import * as vscode from "vscode";
import { getWebviewHTML } from "./webviewContent";

let panel: vscode.WebviewPanel | undefined;
let closedThisSession = false;

export function resetPanelState() {
  closedThisSession = false;
}

export function showDiagramPanel(context: vscode.ExtensionContext) {
  const column = vscode.window.activeTextEditor ? vscode.ViewColumn.Beside : vscode.ViewColumn.One;

  if (panel) {
    panel.reveal(column);
    return;
  }

  closedThisSession = false;

  panel = vscode.window.createWebviewPanel("INTERLISDiagramPanel", "INTERLIS Diagram Panel", column, {
    enableScripts: true,
    localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "out")],
  });

  panel.webview.html = getWebviewHTML(panel.webview, context.extensionUri);
  panel.onDidDispose(
    () => {
      panel = undefined;
      closedThisSession = true;
    },
    null,
    context.subscriptions
  );
}
