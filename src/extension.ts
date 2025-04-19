import * as vscode from "vscode";
import {
  Executable,
  ExecuteCommandRequest,
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";
import { ModelImplementationProvider } from "./ModelImplementationProvider";
import * as fs from "fs";
import path = require("path");
import os = require("os");
import { getWebviewHTML } from "./webviewContent";

const tempdir = path.join(os.tmpdir(), "InterlisLanguageSupport");
let client: LanguageClient | undefined;
let diagramPanel: vscode.WebviewPanel | undefined;
let userHasClosedPanelThisSession = false;

function getRuntimeId(): string {
  switch (process.platform) {
    case "win32":
      return "win-x64";
    case "darwin":
      return "osx-x64";
    case "linux":
      return "linux-x64";
    default:
      throw new Error(`Unsupported platform: ${process.platform}`);
  }
}

function getServerOptions(context: vscode.ExtensionContext, debug: boolean): Executable {
  const executableExtension = process.platform === "win32" ? ".exe" : "";
  const bundledServerPath = `language-server/bin/${getRuntimeId()}/Geowerkstatt.Interlis.LanguageServer${executableExtension}`;
  const debugServerPath = `language-server/src/Geowerkstatt.Interlis.LanguageServer/bin/Debug/net8.0/Geowerkstatt.Interlis.LanguageServer${executableExtension}`;

  const absoluteDebugServerPath = context.asAbsolutePath(debugServerPath);
  if (debug && fs.existsSync(absoluteDebugServerPath)) {
    return {
      command: absoluteDebugServerPath,
      transport: TransportKind.stdio,
    };
  } else {
    return {
      command: context.asAbsolutePath(bundledServerPath),
      transport: TransportKind.stdio,
    };
  }
}

function createAndShowDiagramPanel(context: vscode.ExtensionContext) {
  const column = vscode.window.activeTextEditor ? vscode.ViewColumn.Beside : vscode.ViewColumn.One;

  if (diagramPanel) {
    diagramPanel.reveal(column);
    return;
  }

  userHasClosedPanelThisSession = false;

  diagramPanel = vscode.window.createWebviewPanel("interlisDiagramView", "INTERLIS Diagram", column, {
    enableScripts: true,
    localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "out")],
  });

  diagramPanel.webview.html = getWebviewHTML(diagramPanel.webview, context.extensionUri);

  diagramPanel.onDidDispose(
    () => {
      diagramPanel = undefined;
      userHasClosedPanelThisSession = true; // Set flag when closed
    },
    null,
    context.subscriptions // Manage disposal
  );
}

function checkAndHandleInitialEditor(context: vscode.ExtensionContext) {
  const activeEditor = vscode.window.activeTextEditor;
  if (activeEditor && activeEditor.document.languageId === "INTERLIS2") {
    if (!diagramPanel && !userHasClosedPanelThisSession) {
      createAndShowDiagramPanel(context);
    }
  }
}

export async function activate(context: vscode.ExtensionContext) {
  userHasClosedPanelThisSession = false;

  if (fs.existsSync(tempdir)) {
    fs.rmSync(tempdir, { recursive: true, force: true });
  }
  const disposable = vscode.languages.registerImplementationProvider("INTERLIS2", new ModelImplementationProvider());
  context.subscriptions.push(disposable);

  const serverOptions: ServerOptions = {
    run: getServerOptions(context, false),
    debug: getServerOptions(context, true),
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "INTERLIS2" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.ili"),
    },
  };

  client = new LanguageClient("INTERLIS2LanguageServer", "INTERLIS2 Language Server", serverOptions, clientOptions);
  await client.start();

  const markdownCommand = vscode.commands.registerTextEditorCommand("interlis.generateMarkdown", (textEditor) => {
    vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
      },
      async (progress) => {
        progress.report({ message: "Generating markdown..." });

        try {
          const markdown: string | null = await client?.sendRequest(ExecuteCommandRequest.type, {
            command: "generateMarkdown",
            arguments: [{ uri: textEditor.document.uri.toString() }],
          });

          if (markdown) {
            const document = await vscode.workspace.openTextDocument({ content: markdown, language: "markdown" });
            await vscode.window.showTextDocument(document);
          } else {
            // markdown is null if the file is not yet synchronized to the language server
            await vscode.window.showErrorMessage(
              "Failed to generate documentation, please re-open the file and try again."
            );
          }
        } catch (error) {
          const openOutput = "Open Output";
          if ((await vscode.window.showErrorMessage("Failed to generate documentation.", openOutput)) === openOutput) {
            client?.outputChannel.show();
          }
        }
      }
    );
  });

  const showDiagramCommand = vscode.commands.registerCommand("interlis.showDiagramView", () => {
    createAndShowDiagramPanel(context);
  });

  const iliFileFocusChangeListener = vscode.window.onDidChangeActiveTextEditor((editor) => {
    if (editor && editor.document.languageId === "INTERLIS2") {
      if (!diagramPanel && !userHasClosedPanelThisSession) {
        createAndShowDiagramPanel(context);
      }
    }
  });

  context.subscriptions.push(markdownCommand);
  context.subscriptions.push(showDiagramCommand);
  context.subscriptions.push(iliFileFocusChangeListener);
  checkAndHandleInitialEditor(context);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await client?.stop();
}
