import * as vscode from "vscode";
import { ExecuteCommandRequest } from "vscode-languageclient/node";
import { getLanguageClient } from "./languageServer";

/** The INTERLIS version the language server fully supports. */
const supportedVersion = 2.4;

async function requestVersion(uri: string): Promise<number | null> {
  try {
    return await getLanguageClient().sendRequest(ExecuteCommandRequest.type, {
      command: "getInterlisVersion",
      arguments: [{ uri }],
    });
  } catch (err) {
    console.error("Failed to get INTERLIS version:", err);
    return null;
  }
}

/**
 * Shows the INTERLIS version of the active file, as the language server reads it from the header, in a language
 * status item (the `{}` popup next to the language mode in the status bar), and notes there when the version is only
 * partly supported. Must be initialized after the language server started, so that its listeners run after the
 * client's and a request always sees the latest text.
 */
export function initializeVersionStatus(context: vscode.ExtensionContext) {
  const item = vscode.languages.createLanguageStatusItem("interlis.version", { scheme: "file", language: "INTERLIS2" });
  item.name = "INTERLIS Version";
  item.text = "INTERLIS";

  // Responses can arrive out of order while typing; only the latest request updates the item.
  let latestRequest = 0;

  const update = async () => {
    const document = vscode.window.activeTextEditor?.document;
    if (document?.languageId !== "INTERLIS2") {
      return;
    }

    const request = ++latestRequest;
    const version = await requestVersion(document.uri.toString());
    if (request !== latestRequest) {
      return;
    }

    if (version === null) {
      item.text = "INTERLIS version unknown";
      item.detail = undefined;
    } else {
      item.text = `INTERLIS ${version}`;
      item.detail =
        version === supportedVersion
          ? undefined
          : `limited support, language features target INTERLIS ${supportedVersion}`;
    }
  };

  context.subscriptions.push(
    item,
    vscode.window.onDidChangeActiveTextEditor(update),
    vscode.workspace.onDidChangeTextDocument((event) => {
      if (event.document === vscode.window.activeTextEditor?.document) {
        update();
      }
    })
  );

  update();
}
