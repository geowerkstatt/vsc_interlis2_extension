import * as vscode from "vscode";
import { TextEditor } from "vscode";
import { getLanguageClient, startLanguageServer, stopLanguageServer } from "./languageServer";
import { initializeDiagramPanel, showDiagramPanel } from "./diagramPanel";
import { generateMarkdown } from "./markdown";
import { ModelImplementationProvider } from "./ModelImplementationProvider";

export async function activate(context: vscode.ExtensionContext) {
  await startLanguageServer(context);
  initializeDiagramPanel(context);

  const implementationProvider = vscode.languages.registerImplementationProvider(
    "INTERLIS2",
    new ModelImplementationProvider()
  );

  const markdownCommand = vscode.commands.registerTextEditorCommand(
    "interlis.generateMarkdown",
    (textEditor: TextEditor) => {
      vscode.window.withProgress({ location: vscode.ProgressLocation.Notification }, async (progress) =>
        generateMarkdown(progress, textEditor, getLanguageClient())
      );
    }
  );

  const showDiagramCommand = vscode.commands.registerCommand("interlis.showDiagramView", () =>
    showDiagramPanel(context)
  );

  context.subscriptions.push(markdownCommand);
  context.subscriptions.push(showDiagramCommand);
  context.subscriptions.push(implementationProvider);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await stopLanguageServer();
}
