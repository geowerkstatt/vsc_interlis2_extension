import * as vscode from "vscode";
import * as fs from "fs";
import * as crypto from "node:crypto";

function getNonce(): string {
  return crypto.randomBytes(16).toString("base64");
}

export function getWebviewHTML(webview: vscode.Webview, extensionURI: vscode.Uri): string {
  const nonce = getNonce();

  const mermaidLibUriStr = "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js";

  const scriptPathOnDisk = vscode.Uri.joinPath(extensionURI, "out", "assets", "webview.js");
  const webviewScriptUri = webview.asWebviewUri(scriptPathOnDisk);

  const cspSource = webview.cspSource;
  const contentSecurityPolicy = `
    default-src 'none';
    style-src ${cspSource} 'unsafe-inline';
    img-src ${cspSource} data;
    script-src 'nonce-${nonce}' ${webview.cspSource};
    connect-src 'none';
  `;

  const cssPath = vscode.Uri.joinPath(extensionURI, "out", "assets", "webview.css");
  const css = fs.readFileSync(cssPath.fsPath, "utf8");

  const htmlFilePath = vscode.Uri.joinPath(extensionURI, "out", "assets", "webview.html");
  let htmlContent = "";
  try {
    htmlContent = fs.readFileSync(htmlFilePath.fsPath, "utf8");
  } catch (err) {
    console.log(err);
  }

  htmlContent = htmlContent
    .replace(/__CSP__/g, contentSecurityPolicy)
    .replace(/__NONCE__/g, nonce)
    .replace(/__MERMAID_URI__/g, mermaidLibUriStr)
    .replace(/__STYLE__/g, css)
    .replace(/__WEBVIEW_SCRIPT_URI__/g, webviewScriptUri.toString());

  return htmlContent;
}
