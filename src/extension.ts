import * as vscode from "vscode";
import { TextEditor } from "vscode";
import { getLanguageClient, startLanguageServer, stopLanguageServer } from "./languageServer";
import { handleInterlisInActiveTextEditor, showDiagramPanel } from "./diagramPanel";
import { generateMarkdown } from "./markdown";

export async function activate(context: vscode.ExtensionContext) {
  await startLanguageServer(context);

  const markdownCommand = vscode.commands.registerTextEditorCommand(
    "interlis.generateMarkdown",
    (textEditor: TextEditor) => {
      vscode.window.withProgress({ location: vscode.ProgressLocation.Notification }, async (progress) =>
        generateMarkdown(progress, textEditor, getLanguageClient())
      );
    }
  );

  const showDiagramCommand = vscode.commands.registerCommand(
    'interlis.showDiagramView',
    () => showDiagramPanel(context)
  );

  const iliFileFocusChangeListener = vscode.window.onDidChangeActiveTextEditor(() => {
    handleInterlisInActiveTextEditor(context);
  });

  context.subscriptions.push(markdownCommand);
  context.subscriptions.push(showDiagramCommand);
  context.subscriptions.push(iliFileFocusChangeListener);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await stopLanguageServer();
}
