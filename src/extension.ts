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

const tempdir = path.join(os.tmpdir(), "InterlisLanguageSupport");
let client: LanguageClient | undefined;

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

export async function activate(context: vscode.ExtensionContext) {
  if (fs.existsSync(tempdir)) {
    fs.rmdirSync(tempdir, { recursive: true });
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

  const command = vscode.commands.registerTextEditorCommand("interlis.generateMarkdown", (textEditor) => {
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
  context.subscriptions.push(command);
}

export async function deactivate() {
  console.log("INTERLIS Plugin deactivated.");
  await client?.stop();
}
