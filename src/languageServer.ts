import { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";
import { cleanupTempDir, getServerExecutable } from "./utils";
import * as vscode from "vscode";

let client: LanguageClient;

type SupportedUiLanguage = "de" | "fr" | "it" | "en";

function normalizeUiLanguage(lang: string | undefined): SupportedUiLanguage {
  const tag = (lang ?? "").toLowerCase().split("-")[0];
  return tag === "de" || tag === "fr" || tag === "it" || tag === "en" ? tag : "de";
}

export async function startLanguageServer(context: vscode.ExtensionContext) {
  cleanupTempDir();

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
      uiLanguage: normalizeUiLanguage(vscode.env.language),
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
