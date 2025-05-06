import { ExecuteCommandRequest, LanguageClient } from "vscode-languageclient/node";
import * as vscode from "vscode";
import { Progress, TextEditor } from "vscode";

export async function generateMarkdown(
  progress: Progress<{ message?: string; increment?: number }>,
  editor: TextEditor,
  client: LanguageClient
): Promise<void> {
  progress.report({ message: "Generating markdown..." });

  try {
    const markdown: string | null = await client?.sendRequest(ExecuteCommandRequest.type, {
      command: "generateMarkdown",
      arguments: [{ uri: editor.document.uri.toString() }],
    });

    if (markdown) {
      const document = await vscode.workspace.openTextDocument({ content: markdown, language: "markdown" });
      await vscode.window.showTextDocument(document);
    } else {
      // markdown is null if the file is not yet synchronized to the language server
      await vscode.window.showErrorMessage("Failed to generate documentation, please re-open the file and try again.");
    }
  } catch (error) {
    const openOutput = "Open Output";
    if ((await vscode.window.showErrorMessage("Failed to generate documentation.", openOutput)) === openOutput) {
      client?.outputChannel.show();
    }
  }
}
