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

  // Register the colorChange command to add/update color tags above classes
const colorChangeCommand = vscode.commands.registerCommand(
  "interlis.diagram.colorChange",
  (changes, editor: TextEditor) => {
      if (!editor || editor.document.languageId !== "INTERLIS2") return;

    editor.edit(editBuilder => {
      for (const change of changes) {
        const { className, color } = change; // Expecting { className: string, color: string }
        const doc = editor.document;
        const text = doc.getText();
        const classRegex = new RegExp(`(^|\\n)(\\s*)CLASS\\s+${className}\\b`, "i");
        const match = classRegex.exec(text);

        if (match) {
          const classLine = doc.positionAt(match.index + match[1].length);
          const colorTag = `!!@ geow.uml.color = "${color}"\n`;

          // Check if a color tag already exists above the class
          const prevLine = classLine.line > 0 ? doc.lineAt(classLine.line - 1) : null;
          if (prevLine && prevLine.text.trim().startsWith("!!@ geow.uml.color")) {
            // Replace existing color tag
            console.log("Replacing existing color tag");
            editBuilder.replace(prevLine.range, colorTag.trim());
          } else {
            // Insert new color tag above the class
            console.log("Adding new color tag");
            editBuilder.insert(new vscode.Position(classLine.line, 0), colorTag);
          }
        }
      }
    });
    }
  );

  // Register the nodeChange command
  const nodeChangeCommand = vscode.commands.registerTextEditorCommand(
    "interlis.diagram.nodeChange",
     (editor, edit, changes) => {
      console.log("Node changes received:", changes);
    }
  );

  context.subscriptions.push(markdownCommand);
  context.subscriptions.push(showDiagramCommand);
  context.subscriptions.push(implementationProvider);
  context.subscriptions.push(colorChangeCommand);
  context.subscriptions.push(nodeChangeCommand);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await stopLanguageServer();
}
