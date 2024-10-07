import * as vscode from "vscode";
import {
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

const tempdir = path.join(os.tmpdir(), "InterlisLanguageSupport");
const debugServerPath =
  "language-server/Geowerkstatt.Interlis.LanguageServer/bin/Debug/net8.0/Geowerkstatt.Interlis.LanguageServer.exe";
let client: LanguageClient | undefined;

export async function activate(context: vscode.ExtensionContext) {
  if (fs.existsSync(tempdir)) {
    fs.rmdirSync(tempdir, { recursive: true });
  }
  const disposable = vscode.languages.registerImplementationProvider("INTERLIS2", new ModelImplementationProvider());
  context.subscriptions.push(disposable);

  const serverExecutable = context.asAbsolutePath(debugServerPath);

  const serverOptions: ServerOptions = {
    command: serverExecutable,
    transport: TransportKind.stdio,
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "INTERLIS2" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.ili"),
    },
  };

  client = new LanguageClient("INTERLIS2LanguageServer", "INTERLIS2 Language Server", serverOptions, clientOptions);
  await client.start();

  const command = vscode.commands.registerTextEditorCommand("interlis.generateMarkdown", async (textEditor) => {
    vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
      },
      async (progress) => {
        progress.report({ message: "Generating markdown..." });

        const markdown: string | null = await client?.sendRequest(ExecuteCommandRequest.type, {
          command: "generateMarkdown",
          arguments: [{ uri: textEditor.document.uri.toString() }],
        });

        if (markdown) {
          const document = await vscode.workspace.openTextDocument({ content: markdown, language: "markdown" });
          await vscode.window.showTextDocument(document);
        }
      }
    );
  });
  context.subscriptions.push(command);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await client?.stop();
}
