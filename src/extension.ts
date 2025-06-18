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
    async (changes, editor: TextEditor) => {
      if (!editor || editor.document.languageId !== "INTERLIS2") return;

      const editSuccess = await editor.edit((editBuilder) => {
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
            let lineToCheck = classLine.line - 1;
            let colorTagExists = false;

            // Look at up to 2 lines before the class definition to find color or position tags
            for (let i = 0; i < 2; i++) {
              if (lineToCheck < 0) break;

              const checkLine = doc.lineAt(lineToCheck);
              if (checkLine.text.trim().startsWith("!!@ geow.uml.color")) {
                // Replace existing color tag
                console.log("Replacing existing color tag");
                editBuilder.replace(checkLine.range, colorTag.trim());
                colorTagExists = true;
                break;
              }

              if (!checkLine.text.trim().startsWith("!!@")) {
                // Stop looking if we hit a non-tag line
                break;
              }

              lineToCheck--;
            }

            if (!colorTagExists) {
              // Insert new color tag above the class
              console.log("Adding new color tag");
              const insertLine = classLine.line;
              editBuilder.insert(new vscode.Position(insertLine, 0), colorTag);
            }
          }
        }
      });

      // Save the document if edit was successful
      if (editSuccess) {
        await editor.document.save();
      }
    }
  );

  // Register the nodesChangeCommand
  const nodesChangeCommand = vscode.commands.registerCommand(
    "interlis.diagram.nodesChange",
    async (changes, editor: TextEditor) => {
      if (!editor || editor.document.languageId !== "INTERLIS2") return;

      const editSuccess = await editor.edit((editBuilder) => {
        for (const change of changes) {
          const { className, position } = change; // Expecting { className: string, position: { x: number, y: number } }
          if (!position) return;
          const doc = editor.document;
          const text = doc.getText();
          const classRegex = new RegExp(`(^|\\n)(\\s*)CLASS\\s+${className}\\b`, "i");
          const match = classRegex.exec(text);

          if (match) {
            const classLine = doc.positionAt(match.index + match[1].length);
            const positionTag = `!!@ geow.uml.position = "{\\"x\\":${parseInt(position.x)},\\"y\\":${parseInt(
              position.y
            )}}"\n`;
            // Check if a position tag already exists above the class
            let lineToCheck = classLine.line - 1;
            let positionTagExists = false;

            // Look at up to 2 lines before the class definition to find position or color tags
            for (let i = 0; i < 2; i++) {
              if (lineToCheck < 0) break;

              const checkLine = doc.lineAt(lineToCheck);
              if (checkLine.text.trim().startsWith("!!@ geow.uml.position")) {
                // Replace existing position tag
                console.log("Replacing existing position tag");
                editBuilder.replace(checkLine.range, positionTag.trim());
                positionTagExists = true;
                break;
              }

              if (!checkLine.text.trim().startsWith("!!@")) {
                // Stop looking if we hit a non-tag line
                break;
              }

              lineToCheck--;
            }

            if (!positionTagExists) {
              // Insert new position tag above the class (and below any existing color tag)
              console.log("Adding new position tag");
              const insertLine = classLine.line;
              editBuilder.insert(new vscode.Position(insertLine, 0), positionTag);
            }
          }
        }
      });

      // Save the document if edit was successful
      if (editSuccess) {
        await editor.document.save();
      }
    }
  );

  context.subscriptions.push(markdownCommand);
  context.subscriptions.push(showDiagramCommand);
  context.subscriptions.push(implementationProvider);
  context.subscriptions.push(colorChangeCommand);
  context.subscriptions.push(nodesChangeCommand);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await stopLanguageServer();
}
