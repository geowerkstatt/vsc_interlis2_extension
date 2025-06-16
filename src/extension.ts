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

  const lintAllCommand = vscode.commands.registerCommand("interlis.lintAll", async () => {
    const files = await vscode.workspace.findFiles("**/*.ili");
    const client = getLanguageClient();

    for (const file of files) {
      await vscode.workspace.openTextDocument(file);
      // Diagnostics will be triggered by the language server
    }

    vscode.window.showInformationMessage(`Linted ${files.length} INTERLIS2 files.`);
  });

  const fixAllCommand = vscode.commands.registerCommand("interlis.fixAll", async () => {
    const files = await vscode.workspace.findFiles("**/*.ili");
    let totalEdits = 0;

    for (const file of files) {
      const doc = await vscode.workspace.openTextDocument(file);
      const diagnostics = vscode.languages.getDiagnostics(file);

      for (const diagnostic of diagnostics) {
        const codeActions = await vscode.commands.executeCommand<vscode.CodeAction[]>(
          "vscode.executeCodeActionProvider",
          file,
          diagnostic.range,
          vscode.CodeActionKind.QuickFix.value
        );

        if (codeActions) {
          for (const action of codeActions) {
            if (action.edit) {
              await vscode.workspace.applyEdit(action.edit);
              totalEdits++;
            }
          }
        }
      }
    }

    vscode.window.showInformationMessage(`Applied ${totalEdits} fixes to INTERLIS2 files.`);
  });

  context.subscriptions.push(markdownCommand);
  context.subscriptions.push(showDiagramCommand);
  context.subscriptions.push(implementationProvider);
  context.subscriptions.push(lintAllCommand);
  context.subscriptions.push(fixAllCommand);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await stopLanguageServer();
}
