import { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";
import { cleanupTempDir, getServerExecutable } from "./utils";
import * as vscode from "vscode";

let client: LanguageClient;

export async function startLanguageServer(context: vscode.ExtensionContext) {
  cleanupTempDir();

  const workspaceFolders = vscode.workspace.workspaceFolders;
  const workspaceRoot = workspaceFolders && workspaceFolders.length > 0 ? workspaceFolders[0].uri.fsPath : undefined;

  const serverOptions: ServerOptions = {
    run: getServerExecutable(context, false),
    debug: getServerExecutable(context, true),
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "INTERLIS2" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.ili"),
    },
    initializationOptions: {
      workspaceRoot: workspaceRoot,
    },
  };

  client = new LanguageClient("INTERLIS2LanguageServer", "INTERLIS2 Language Server", serverOptions, clientOptions);

  await client.start();
}

export async function stopLanguageServer() {
  if (client) {
    await client.stop();
  }
}

export function getLanguageClient(): LanguageClient {
  return client;
}
