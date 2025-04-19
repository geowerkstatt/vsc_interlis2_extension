import * as os from "node:os";
import * as path from "node:path";
import * as fs from "node:fs";
import * as vscode from "vscode";
import { Executable, TransportKind } from "vscode-languageclient/node";

export const tempDir = path.join(os.tmpdir(), "InterlisLanguageSupport");

export function cleanupTempDir() {
  if (fs.existsSync(tempDir)) {
    fs.rmSync(tempDir, { recursive: true, force: true });
  }
}

export function ensureDirExists(dirPath: string) {
  if (!fs.existsSync(dirPath)) {
    fs.mkdirSync(dirPath, { recursive: true });
  }
}

export function getRuntimeId(): string {
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

export function getServerExecutable(context: vscode.ExtensionContext, debug: boolean): Executable {
  const exeExt = process.platform === "win32" ? ".exe" : "";
  const serverName = `Geowerkstatt.Interlis.LanguageServer${exeExt}`;
  const runPath = `language-server/bin/${getRuntimeId()}/${serverName}`;
  const debugPath = `language-server/src/Geowerkstatt.Interlis.LanguageServer/bin/Debug/net8.0/${serverName}`;
  const absoluteDebug = context.asAbsolutePath(debugPath);

  const command = debug && fs.existsSync(absoluteDebug) ? absoluteDebug : context.asAbsolutePath(runPath);
  return { command: command, transport: TransportKind.stdio };
}
