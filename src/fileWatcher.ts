import * as vscode from "vscode";
import { handleInterlisInActiveTextEditor } from "./diagramPanel";

export function registerFileWatcher(context: vscode.ExtensionContext) {
  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(() => handleInterlisInActiveTextEditor(context))
  );
  handleInterlisInActiveTextEditor(context);
}
