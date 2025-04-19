import * as os from "node:os";
import * as path from "node:path";
import * as fs from "node:fs";

export const tempDir = path.join(os.tmpdir(), "InterlisLanguageSupport");

export function cleanupTempDir() {
  if (fs.existsSync(tempDir)) {
    fs.rmSync(tempDir, { recursive: true, force: true });
  }
}

