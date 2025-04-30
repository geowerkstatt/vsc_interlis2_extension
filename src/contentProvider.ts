import * as vscode from "vscode";
import * as fs from "fs";
import * as crypto from "node:crypto";

const MERMAID_VERSION = "11.6.0";

function getNonce(): string {
  return crypto.randomBytes(16).toString("base64");
}

function buildCSP(nonce: string, cspSource: string): string {
  return [
    "default-src 'none';",
    `style-src ${cspSource} 'unsafe-inline';`,
    `img-src ${cspSource} data:;`,
    `script-src 'nonce-${nonce}' ${cspSource};`,
    "connect-src 'none';",
  ].join("\n");
}

function readAsset(extensionUri: vscode.Uri, ...pathSegments: string[]): string {
  const fileUri = vscode.Uri.joinPath(extensionUri, ...pathSegments);
  return fs.readFileSync(fileUri.fsPath, "utf8");
}

export function getWebviewHTML(webview: vscode.Webview, extensionUri: vscode.Uri): string {
  const nonce = getNonce();
  const csp = buildCSP(nonce, webview.cspSource);

  const mermaidUri = `https://cdn.jsdelivr.net/npm/mermaid@${MERMAID_VERSION}/dist/mermaid.min.js`;
  const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(extensionUri, "out", "assets", "webview.js")).toString();
  const style = readAsset(extensionUri, "out", "assets", "webview.css");
  let html = readAsset(extensionUri, "out", "assets", "webview.html");

  const replacements: Record<string, string> = {
    __CSP__: csp,
    __NONCE__: nonce,
    __MERMAID_URI__: mermaidUri,
    __STYLE__: style,
    __WEBVIEW_SCRIPT_URI__: scriptUri,
  };

  for (const [placeholder, value] of Object.entries(replacements)) {
    html = html.replace(new RegExp(placeholder, "g"), value);
  }

  return html;
}
