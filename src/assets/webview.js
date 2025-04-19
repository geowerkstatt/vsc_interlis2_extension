const vscode = acquireVsCodeApi();

console.log("Webview main.js script loaded.");

setTimeout(() => {
  if (typeof mermaid !== "undefined") {
    console.log("Webview: Mermaid library seems to be loaded (via main.js).");
  } else {
    console.error("Webview: Mermaid library WAS NOT loaded (checked via main.js).");
  }
}, 500);
